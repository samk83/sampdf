using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DrawingReview.App.Core.Services;

/// <summary>복구 사본 하나의 정보.</summary>
public sealed record RecoveryInfo(string OriginalPath, string RecoveryPath, DateTimeOffset SavedAt);

/// <summary>
/// 자동 저장·강제 종료 복구 (기획서 10장 5항).
/// 복구 사본은 원본과 다른 폴더(Windows: %LOCALAPPDATA%\DrawingReview\Recovery)에 두고, 원본 파일은 건드리지 않는다.
/// 실제 PDF로 저장하거나 저장하지 않고 닫기로 하면 복구 사본을 지운다.
/// </summary>
public sealed class RecoveryService(string directory)
{
    public string Directory { get; } = directory;

    private string Key(string originalPath)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(originalPath).ToUpperInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }

    public string RecoveryPathFor(string originalPath) => Path.Combine(Directory, Key(originalPath) + ".pdf");

    private string ManifestPathFor(string originalPath) => Path.Combine(Directory, Key(originalPath) + ".json");

    /// <summary>saveTo(경로)로 복구 사본을 쓰고 정보 파일을 남긴다.</summary>
    public async Task<RecoveryInfo> SaveAsync(string originalPath, Func<string, Task> saveTo)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var recovery = RecoveryPathFor(originalPath);
        await saveTo(recovery);
        var info = new RecoveryInfo(Path.GetFullPath(originalPath), recovery, DateTimeOffset.Now);
        await File.WriteAllTextAsync(ManifestPathFor(originalPath), JsonSerializer.Serialize(info));
        return info;
    }

    /// <summary>원본보다 새로운 복구 사본이 있으면 돌려준다.</summary>
    public RecoveryInfo? Find(string originalPath)
    {
        try
        {
            var manifest = ManifestPathFor(originalPath);
            if (!File.Exists(manifest)) return null;
            var info = JsonSerializer.Deserialize<RecoveryInfo>(File.ReadAllText(manifest));
            if (info == null || !File.Exists(info.RecoveryPath)) return null;
            var originalTime = File.Exists(originalPath) ? File.GetLastWriteTimeUtc(originalPath) : DateTime.MinValue;
            return info.SavedAt.UtcDateTime > originalTime ? info : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Delete(string originalPath)
    {
        foreach (var p in new[] { RecoveryPathFor(originalPath), ManifestPathFor(originalPath) })
        {
            try { if (File.Exists(p)) File.Delete(p); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* 다음 저장 때 다시 시도 */ }
        }
    }
}
