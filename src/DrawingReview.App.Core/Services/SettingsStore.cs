using System.Text.Json;
using System.Text.Json.Serialization;
using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.Tools;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Persistence;

namespace DrawingReview.App.Core.Services;

/// <summary>프로그램을 다시 켜도 기억할 개인 설정.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string? Author { get; set; }
    public Dictionary<ToolKind, ToolPreset> Tools { get; set; } = new();
    public List<RgbColor> RecentColors { get; set; } = [];
    public bool KeepToolActive { get; set; }
    public bool IsLeftPanelVisible { get; set; } = true;
    public bool IsRightPanelVisible { get; set; } = true;
    public bool IsReviewListVisible { get; set; } = true;
    public List<string> RecentFiles { get; set; } = [];
    public List<ViewModels.SavedToolSet> SavedToolSets { get; set; } = [];
}

/// <summary>
/// 설정을 JSON 파일로 읽고 쓴다 (Windows: %APPDATA%\DrawingReview\settings.json).
/// 파일이 없거나 깨져 있으면 기본값으로 시작한다 — 설정 문제로 프로그램이 안 켜지면 안 되기 때문이다.
/// </summary>
public sealed class SettingsStore(string path)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Path { get; } = path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, Json);
        SafeFileSaver.Save(Path, temp => File.WriteAllText(temp, json), temp => JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(temp), Json));
    }
}
