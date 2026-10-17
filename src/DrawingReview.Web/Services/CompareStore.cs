using System.Collections.Concurrent;
using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.Web.Services;

/// <summary>비교 한 번의 결과: 어떤 두 파일을 어떤 설정으로 비교했는지와 문서 전체 결과.</summary>
public sealed record CompareSession(string Id, string Owner, string OriginalId, string RevisionId, bool IgnoreTitleBlock, DocumentCompareResult Result, DateTimeOffset CreatedAt)
{
    public DateTimeOffset LastUsed { get; set; } = CreatedAt;
}

/// <summary>
/// 비교 실행과 결과 보관 (주인마다 따로). 두 문서의 잠금을 항상 같은 순서(Id 순)로 잡아 서로 기다리다 멈추는 일을 막는다.
/// 동시에 도는 비교 수는 HostingOptions.MaxConcurrentCompares로 제한한다 (웹 모드의 메모리 보호).
/// </summary>
public sealed class CompareStore(DocumentStore documents, HostingOptions options, TimeProvider time)
{
    public const int MaxSessions = 12;
    /// <summary>서버 전체 비교 결과 수. 넘으면 가장 오래 안 쓴 것부터 지운다.</summary>
    public const int MaxTotalSessions = 200;
    private readonly ConcurrentDictionary<string, CompareSession> _sessions = new();
    private readonly SemaphoreSlim _running = new(Math.Max(1, options.IsWeb ? options.MaxConcurrentCompares : int.MaxValue / 2));

    public CompareSession? Get(string id, string owner = Owners.Local)
    {
        if (!_sessions.TryGetValue(id, out var s) || s.Owner != owner) return null;
        s.LastUsed = time.GetUtcNow();
        // 결과를 보는 동안 두 파일도 "쓰는 중"으로 (30분 삭제에서 지켜 줌)
        documents.Get(s.OriginalId, owner);
        documents.Get(s.RevisionId, owner);
        return s;
    }

    public async Task<CompareSession> RunAsync(StoredDocument original, StoredDocument revision, bool ignoreTitleBlock, CancellationToken cancel)
    {
        if (original.Owner != revision.Owner) throw new InvalidOperationException("Both files must belong to the same user.");
        await _running.WaitAsync(cancel);
        DocumentCompareResult result;
        try
        {
            result = await WithBothAsync(original, revision, () => Task.Run(() =>
                DocumentComparer.Compare(original.Document, revision.Document, Settings(revision, ignoreTitleBlock), cancel: cancel), cancel), cancel);
        }
        finally { _running.Release(); }
        var session = new CompareSession(Guid.NewGuid().ToString("N"), revision.Owner, original.Id, revision.Id, ignoreTitleBlock, result, time.GetUtcNow());
        _sessions[session.Id] = session;
        foreach (var old in _sessions.Values.Where(s => s.Owner == session.Owner).OrderByDescending(s => s.CreatedAt).Skip(MaxSessions).ToList())
            _sessions.TryRemove(old.Id, out _);
        foreach (var old in _sessions.Values.OrderByDescending(s => s.LastUsed).Skip(MaxTotalSessions).ToList())
            _sessions.TryRemove(old.Id, out _);
        return session;
    }

    /// <summary>cutoff 전부터 안 쓴 비교 결과를 지운다 (웹 모드, ExpirySweeper).</summary>
    public int RemoveIdle(DateTimeOffset cutoff)
    {
        var removed = 0;
        foreach (var old in _sessions.Values.Where(s => s.LastUsed < cutoff).ToList())
            if (_sessions.TryRemove(old.Id, out _)) removed++;
        return removed;
    }

    public int Count => _sessions.Count;

    /// <summary>
    /// 비교 설정. 제목란 무시: 일반적인 도면 제목란 위치(오른쪽 아래, 가로 30%·세로 18%)를 뺀다 (데스크톱 비교와 같은 규칙).
    /// 페이지마다 크기가 다를 수 있어 첫 페이지 기준이 아니라 가장 큰 페이지 기준으로 잡는다.
    /// </summary>
    private static CompareSettings Settings(StoredDocument revision, bool ignoreTitleBlock)
    {
        var s = new CompareSettings();
        if (!ignoreTitleBlock) return s;
        var info = Enumerable.Range(0, revision.Document.PageCount).Select(revision.Document.GetPageInfo).MaxBy(p => p.Width * p.Height)!;
        return s with { IgnoreRegions = [new PageRect(info.Width * 0.70, info.Height * 0.82, info.Width, info.Height)] };
    }

    /// <summary>
    /// 두 문서의 잠금을 Id 순서로 잡고 작업한다. 같은 파일끼리 비교해도 한 번만 잡는다.
    /// 기다리는 사이 파일이 닫히면 DocumentClosedException (API는 404).
    /// </summary>
    public static async Task<T> WithBothAsync<T>(StoredDocument a, StoredDocument b, Func<Task<T>> work, CancellationToken cancel = default)
    {
        var first = string.CompareOrdinal(a.Id, b.Id) <= 0 ? a : b;
        var second = ReferenceEquals(first, a) ? b : a;
        if (!await first.EnterAsync(cancel)) throw new DocumentClosedException();
        var same = ReferenceEquals(first, second);
        try
        {
            if (!same && !await second.EnterAsync(cancel)) throw new DocumentClosedException();
            try { return await work(); }
            finally { if (!same) second.Gate.Release(); }
        }
        finally { first.Gate.Release(); }
    }

    /// <summary>비교한 원본 파일. 그 사이 닫혔으면 null.</summary>
    public StoredDocument? Original(CompareSession s) => documents.Get(s.OriginalId, s.Owner);
    /// <summary>비교한 리비전 파일. 그 사이 닫혔으면 null.</summary>
    public StoredDocument? Revision(CompareSession s) => documents.Get(s.RevisionId, s.Owner);
}
