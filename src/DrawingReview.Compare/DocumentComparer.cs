using DrawingReview.Compare.Model;
using DrawingReview.Engine.Documents;

namespace DrawingReview.Compare;

/// <summary>페이지 하나의 변경 개수 요약.</summary>
public sealed record PageSummary(int Total, int Added, int Removed, int Modified, int Text, int Graphic)
{
    public static PageSummary Of(IReadOnlyList<ChangeRegion> regions) => new(
        regions.Count,
        regions.Count(r => r.Kind == ChangeKind.Added),
        regions.Count(r => r.Kind == ChangeKind.Removed),
        regions.Count(r => r.Kind == ChangeKind.Modified),
        regions.Count(r => r.Category == ChangeCategory.Text),
        regions.Count(r => r.Category == ChangeCategory.Graphic));

    public static readonly PageSummary Empty = new(0, 0, 0, 0, 0, 0);
}

/// <summary>
/// 페이지 짝 하나의 결과. 한쪽에만 있는 페이지는 비교하지 않고 표시만 한다:
/// OldPage만 있으면 개정본에서 빠진 페이지, NewPage만 있으면 새로 생긴 페이지.
/// </summary>
public sealed record PageComparison(int? OldPage, int? NewPage, CompareResult? Result, PageSummary Summary)
{
    public bool IsAddedPage => OldPage == null && NewPage != null;
    public bool IsRemovedPage => OldPage != null && NewPage == null;
}

public sealed record DocumentCompareResult(IReadOnlyList<PageComparison> Pages, IReadOnlyList<string> Warnings)
{
    public PageSummary Total => new(
        Pages.Sum(p => p.Summary.Total), Pages.Sum(p => p.Summary.Added), Pages.Sum(p => p.Summary.Removed),
        Pages.Sum(p => p.Summary.Modified), Pages.Sum(p => p.Summary.Text), Pages.Sum(p => p.Summary.Graphic));
}

/// <summary>
/// 문서 전체 비교: 페이지 번호 순서대로 짝을 지어 페이지마다 비교한다 (웹 비교 화면용).
/// 페이지 수가 다르면 남는 페이지를 "추가된 페이지 / 빠진 페이지"로 알린다.
/// 엔진 호출은 호출한 스레드에서 순서대로 한다.
/// </summary>
public static class DocumentComparer
{
    public static DocumentCompareResult Compare(IPdfDocument oldDoc, IPdfDocument newDoc, CompareSettings? settings = null,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken cancel = default)
    {
        int oldCount = oldDoc.PageCount, newCount = newDoc.PageCount;
        var total = Math.Max(oldCount, newCount);
        var pages = new List<PageComparison>(total);
        var warnings = new List<string>();
        if (oldCount != newCount)
            warnings.Add(newCount > oldCount
                ? $"The revision has {newCount - oldCount} more page(s) ({oldCount} → {newCount}). Extra pages are marked as added."
                : $"The revision has {oldCount - newCount} fewer page(s) ({oldCount} → {newCount}). Missing pages are marked as removed.");

        for (int i = 0; i < total; i++)
        {
            cancel.ThrowIfCancellationRequested();
            if (i >= oldCount) pages.Add(new PageComparison(null, i, null, PageSummary.Empty));
            else if (i >= newCount) pages.Add(new PageComparison(i, null, null, PageSummary.Empty));
            else
            {
                var result = PageComparer.Compare(PageSource.From(oldDoc, i), PageSource.From(newDoc, i), settings);
                pages.Add(new PageComparison(i, i, result, PageSummary.Of(result.Regions)));
                warnings.AddRange(result.Warnings.Select(w => $"Page {i + 1}: {w}"));
            }
            progress?.Report((i + 1, total));
        }
        return new DocumentCompareResult(pages, warnings);
    }
}
