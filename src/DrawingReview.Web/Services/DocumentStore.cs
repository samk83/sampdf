using System.Collections.Concurrent;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Pdfium;

namespace DrawingReview.Web.Services;

/// <summary>올린 파일 하나: 원래 이름과 바이트, 열린 문서, 엔진 호출을 한 번에 하나씩 하기 위한 잠금.</summary>
public sealed class StoredDocument : IDisposable
{
    public required string Id { get; init; }
    /// <summary>올린 사람 (Owners). 다른 주인의 요청에는 없는 파일처럼 보인다.</summary>
    public required string Owner { get; init; }
    public required string Name { get; init; }
    public required byte[] Bytes { get; init; }
    public required bool IsImage { get; init; }
    public required IPdfDocument Document { get; init; }
    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>PDF 엔진 문서는 여러 스레드에서 동시에 쓸 수 없으므로 이 잠금 안에서만 다룬다. 잡을 때는 EnterAsync.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>보관소에서 빠져 문서가 닫혔다. 잠금을 기다리던 요청은 이것을 보고 "없는 파일"로 끝낸다.</summary>
    public bool Closed { get; private set; }

    /// <summary>잠금을 잡는다. 기다리는 사이 파일이 닫혔으면 잠금을 돌려주고 false.</summary>
    public async Task<bool> EnterAsync(CancellationToken cancel = default)
    {
        await Gate.WaitAsync(cancel);
        if (!Closed) return true;
        Gate.Release();
        return false;
    }

    /// <summary>쓰는 요청이 끝난 뒤 문서를 닫는다 (잠금은 남겨 두어 기다리던 요청이 멈추지 않게).</summary>
    internal async Task CloseWhenFreeAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (Closed) return;
            Closed = true;
            Document.Dispose();
        }
        finally { Gate.Release(); }
    }

    public void Dispose()
    {
        if (Closed) return;
        Closed = true;
        Document.Dispose();
    }
}

/// <summary>작업 중에 파일이 닫혔다 (지움·30분 삭제). API는 404로 답한다.</summary>
public sealed class DocumentClosedException() : Exception("The file is no longer open. Open it again.");

/// <summary>웹 모드: 서버 메모리가 다 찼다. API는 503으로 답한다.</summary>
public sealed class ServerFullException() : Exception("The server is busy right now. Try again in a few minutes.");

/// <summary>
/// 올린 파일 보관소 (메모리, 데이터베이스 없음). 주인마다 최근 파일 몇 개만 둔다.
/// 업로드·내보내기 중 ASP.NET·PDF 엔진이 임시 파일을 잠깐 쓸 수 있지만 처리가 끝나면 지운다.
/// 원본 파일은 바뀌지 않는다: 바이트를 복사해 두고, 내려받기용 PDF는 그 복사본으로 새로 만든다.
/// 웹 모드에서는 ExpirySweeper가 오래 안 쓴 파일을 지운다.
/// </summary>
public sealed class DocumentStore(PdfiumEngine engine, TimeProvider time) : IDisposable
{
    /// <summary>주인 한 명이 동시에 열어 둘 수 있는 파일 수.</summary>
    public const int MaxDocuments = 24;
    /// <summary>서버 전체에 열어 둘 수 있는 파일 수. 넘으면 새 파일을 받지 않는다 (다른 사람 파일을 닫지 않음).</summary>
    public const int MaxTotalDocuments = 200;
    /// <summary>서버 전체 파일 바이트 한도 (웹 모드 메모리 보호, HostingOptions.MaxTotalMb). PC 모드는 제한 없음.</summary>
    public long MaxTotalBytes { get; init; } = long.MaxValue;
    private long _totalBytes;
    private readonly ConcurrentDictionary<string, StoredDocument> _docs = new();

    public static bool IsSupported(string fileName) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || PdfiumEngine.IsImageFile(fileName);

    /// <summary>파일을 연다. PDF가 아니면 그림으로 열어 PDF로 바꾼다. 암호가 걸린 PDF는 거부한다.</summary>
    public StoredDocument Add(string fileName, byte[] bytes, string owner = Owners.Local)
    {
        if (!IsSupported(fileName)) throw new NotSupportedException("Only PDF, PNG, JPG, TIFF and BMP files are supported.");
        Trim(owner, incoming: 1);
        if (_docs.Count >= MaxTotalDocuments || Interlocked.Read(ref _totalBytes) + bytes.Length > MaxTotalBytes) throw new ServerFullException();
        var isImage = PdfiumEngine.IsImageFile(fileName);
        var doc = Open(bytes, fileName, isImage);
        if (doc.Security.NeedsPassword)
        {
            doc.Dispose();
            throw new InvalidOperationException("Password-protected PDFs are not supported. Remove the password first.");
        }
        var stored = new StoredDocument
        {
            Id = Guid.NewGuid().ToString("N"), Owner = owner, Name = Path.GetFileName(fileName), Bytes = bytes, IsImage = isImage, Document = doc,
            LastUsed = time.GetUtcNow(),
        };
        _docs[stored.Id] = stored;
        Interlocked.Add(ref _totalBytes, bytes.Length);
        return stored;
    }

    /// <summary>같은 바이트로 새 문서를 연다 (내려받기용 사본에 표시를 넣을 때).</summary>
    public IPdfDocument OpenCopy(StoredDocument d) => Open(d.Bytes, d.Name, d.IsImage);

    private IPdfDocument Open(byte[] bytes, string fileName, bool isImage) =>
        isImage ? engine.OpenImage(bytes, Path.GetExtension(fileName).TrimStart('.')) : engine.Open(bytes);

    /// <summary>그 주인의 파일. 번호가 맞아도 주인이 다르면 null.</summary>
    public StoredDocument? Get(string id, string owner = Owners.Local)
    {
        if (!_docs.TryGetValue(id, out var d) || d.Owner != owner) return null;
        d.LastUsed = time.GetUtcNow();
        return d;
    }

    public bool Remove(string id, string owner = Owners.Local)
    {
        if (Get(id, owner) is null || !_docs.TryRemove(id, out var d)) return false;
        Close(d);
        return true;
    }

    /// <summary>cutoff 전부터 안 쓴 파일을 지운다 (웹 모드, ExpirySweeper). 지운 수를 돌려준다.</summary>
    public int RemoveIdle(DateTimeOffset cutoff)
    {
        var removed = 0;
        foreach (var old in _docs.Values.Where(d => d.LastUsed < cutoff).ToList())
            if (_docs.TryRemove(old.Id, out var d)) { Close(d); removed++; }
        return removed;
    }

    public int Count => _docs.Count;

    /// <summary>그 주인의 오래 안 쓴 파일부터 닫아, 새 파일까지 MaxDocuments 개가 되게 한다.</summary>
    private void Trim(string owner, int incoming)
    {
        foreach (var old in _docs.Values.Where(d => d.Owner == owner).OrderByDescending(d => d.LastUsed).Skip(MaxDocuments - incoming).ToList())
            if (_docs.TryRemove(old.Id, out var d)) Close(d);
    }

    private void Close(StoredDocument d)
    {
        Interlocked.Add(ref _totalBytes, -d.Bytes.Length);
        // 다른 요청이 쓰는 중이면 끝난 뒤에 닫는다
        _ = d.CloseWhenFreeAsync();
    }

    public void Dispose()
    {
        foreach (var d in _docs.Values) d.Dispose();
        _docs.Clear();
    }
}
