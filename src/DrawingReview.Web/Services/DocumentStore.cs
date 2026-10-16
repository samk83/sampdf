using System.Collections.Concurrent;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.MuPdf;

namespace DrawingReview.Web.Services;

/// <summary>올린 파일 하나: 원래 이름과 바이트, 열린 문서, 엔진 호출을 한 번에 하나씩 하기 위한 잠금.</summary>
public sealed class StoredDocument : IDisposable
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required byte[] Bytes { get; init; }
    public required bool IsImage { get; init; }
    public required IPdfDocument Document { get; init; }
    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>MuPDF 문서는 여러 스레드에서 동시에 쓸 수 없으므로 이 잠금 안에서만 다룬다.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public void Dispose()
    {
        Document.Dispose();
        Gate.Dispose();
    }
}

/// <summary>
/// 올린 파일 보관소 (메모리). 이 프로그램은 내 PC에서 혼자 쓰므로 데이터베이스 없이 최근 파일 몇 개만 둔다.
/// 원본 파일은 바뀌지 않는다: 바이트를 복사해 두고, 내려받기용 PDF는 그 복사본으로 새로 만든다.
/// </summary>
public sealed class DocumentStore(MuPdfEngine engine) : IDisposable
{
    public const int MaxDocuments = 24;
    private readonly ConcurrentDictionary<string, StoredDocument> _docs = new();

    public static bool IsSupported(string fileName) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || MuPdfEngine.IsImageFile(fileName);

    /// <summary>파일을 연다. PDF가 아니면 그림으로 열어 PDF로 바꾼다. 암호가 걸린 PDF는 거부한다.</summary>
    public StoredDocument Add(string fileName, byte[] bytes)
    {
        if (!IsSupported(fileName)) throw new NotSupportedException("Only PDF, PNG, JPG, TIFF and BMP files are supported.");
        var isImage = MuPdfEngine.IsImageFile(fileName);
        var doc = Open(bytes, fileName, isImage);
        if (doc.Security.NeedsPassword)
        {
            doc.Dispose();
            throw new InvalidOperationException("Password-protected PDFs are not supported. Remove the password first.");
        }
        var stored = new StoredDocument { Id = Guid.NewGuid().ToString("N"), Name = Path.GetFileName(fileName), Bytes = bytes, IsImage = isImage, Document = doc };
        _docs[stored.Id] = stored;
        Trim();
        return stored;
    }

    /// <summary>같은 바이트로 새 문서를 연다 (내려받기용 사본에 표시를 넣을 때).</summary>
    public IPdfDocument OpenCopy(StoredDocument d) => Open(d.Bytes, d.Name, d.IsImage);

    private IPdfDocument Open(byte[] bytes, string fileName, bool isImage) =>
        isImage ? engine.OpenImage(bytes, Path.GetExtension(fileName).TrimStart('.')) : engine.Open(bytes);

    public StoredDocument? Get(string id)
    {
        if (!_docs.TryGetValue(id, out var d)) return null;
        d.LastUsed = DateTimeOffset.UtcNow;
        return d;
    }

    public bool Remove(string id)
    {
        if (!_docs.TryRemove(id, out var d)) return false;
        DisposeWhenFree(d);
        return true;
    }

    /// <summary>오래 안 쓴 파일부터 정리해 메모리를 아낀다.</summary>
    private void Trim()
    {
        foreach (var old in _docs.Values.OrderByDescending(d => d.LastUsed).Skip(MaxDocuments).ToList())
            if (_docs.TryRemove(old.Id, out var d)) DisposeWhenFree(d);
    }

    private static void DisposeWhenFree(StoredDocument d)
    {
        // 다른 요청이 쓰는 중이면 끝난 뒤에 닫는다
        _ = Task.Run(async () =>
        {
            await d.Gate.WaitAsync();
            d.Document.Dispose();
        });
    }

    public void Dispose()
    {
        foreach (var d in _docs.Values) d.Dispose();
        _docs.Clear();
    }
}
