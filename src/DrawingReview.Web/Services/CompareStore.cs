using System.Collections.Concurrent;
using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.Web.Services;

/// <summary>비교 한 번의 결과: 어떤 두 파일을 어떤 설정으로 비교했는지와 문서 전체 결과.</summary>
public sealed record CompareSession(string Id, string OriginalId, string RevisionId, bool IgnoreTitleBlock, DocumentCompareResult Result, DateTimeOffset CreatedAt);

/// <summary>
/// 비교 실행과 결과 보관. 두 문서의 잠금을 항상 같은 순서(Id 순)로 잡아 서로 기다리다 멈추는 일을 막는다.
/// </summary>
public sealed class CompareStore(DocumentStore documents)
{
    public const int MaxSessions = 12;
    private readonly ConcurrentDictionary<string, CompareSession> _sessions = new();

    public CompareSession? Get(string id) => _sessions.TryGetValue(id, out var s) ? s : null;

    public async Task<CompareSession> RunAsync(StoredDocument original, StoredDocument revision, bool ignoreTitleBlock, CancellationToken cancel)
    {
        var result = await WithBothAsync(original, revision, () => Task.Run(() =>
            DocumentComparer.Compare(original.Document, revision.Document, Settings(revision, ignoreTitleBlock), cancel: cancel), cancel));
        var session = new CompareSession(Guid.NewGuid().ToString("N"), original.Id, revision.Id, ignoreTitleBlock, result, DateTimeOffset.UtcNow);
        _sessions[session.Id] = session;
        foreach (var old in _sessions.Values.OrderByDescending(s => s.CreatedAt).Skip(MaxSessions).ToList())
            _sessions.TryRemove(old.Id, out _);
        return session;
    }

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

    /// <summary>두 문서의 잠금을 Id 순서로 잡고 작업한다. 같은 파일끼리 비교해도 한 번만 잡는다.</summary>
    public static async Task<T> WithBothAsync<T>(StoredDocument a, StoredDocument b, Func<Task<T>> work)
    {
        var first = string.CompareOrdinal(a.Id, b.Id) <= 0 ? a : b;
        var second = ReferenceEquals(first, a) ? b : a;
        await first.Gate.WaitAsync();
        var same = ReferenceEquals(first, second);
        try
        {
            if (!same) await second.Gate.WaitAsync();
            try { return await work(); }
            finally { if (!same) second.Gate.Release(); }
        }
        finally { first.Gate.Release(); }
    }

    public StoredDocument Original(CompareSession s) => documents.Get(s.OriginalId) ?? throw new KeyNotFoundException("The original file is no longer open. Open it again.");
    public StoredDocument Revision(CompareSession s) => documents.Get(s.RevisionId) ?? throw new KeyNotFoundException("The revision file is no longer open. Open it again.");
}
