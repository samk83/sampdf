using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;
using DrawingReview.Web.Services;

namespace DrawingReview.Web.Api;

// 화면(웹)으로 보내는 자료 모양. 좌표는 모두 "보이는 페이지 좌표"(pt, 왼쪽 위 원점).

public sealed record RectDto(double X0, double Y0, double X1, double Y1)
{
    public static RectDto Of(PageRect r) => new(Math.Round(r.X0, 2), Math.Round(r.Y0, 2), Math.Round(r.X1, 2), Math.Round(r.Y1, 2));
}

public sealed record PageSizeDto(double Width, double Height);

public sealed record FileDto(string Id, string Name, bool IsImage, IReadOnlyList<PageSizeDto> Pages)
{
    public static FileDto Of(StoredDocument d) => new(d.Id, d.Name, d.IsImage,
        Enumerable.Range(0, d.Document.PageCount).Select(i => d.Document.GetPageInfo(i)).Select(p => new PageSizeDto(p.Width, p.Height)).ToList());
}

public sealed record SummaryDto(int Total, int Added, int Removed, int Modified, int Text, int Graphic)
{
    public static SummaryDto Of(PageSummary s) => new(s.Total, s.Added, s.Removed, s.Modified, s.Text, s.Graphic);
}

/// <summary>변경 하나. Kind: added|removed|modified, Category: text|graphic.</summary>
public sealed record ChangeDto(string Id, int Number, string Kind, string Category, string Confidence, RectDto Bounds, RectDto BoundsInOld,
    IReadOnlyList<RectDto> Highlights, string Description)
{
    public static string IdOf(int pairIndex, int number) => $"p{pairIndex}-c{number}";

    public static ChangeDto Of(int pairIndex, ChangeRegion r) => new(IdOf(pairIndex, r.Number), r.Number,
        r.Kind.ToString().ToLowerInvariant(), r.Category.ToString().ToLowerInvariant(), r.Confidence.ToString().ToLowerInvariant(),
        RectDto.Of(r.Bounds), RectDto.Of(r.BoundsInOld), r.TextHighlights.Select(RectDto.Of).ToList(), r.Description);
}

/// <summary>페이지 짝 하나. 한쪽 페이지 번호가 null이면 추가되었거나 빠진 페이지.</summary>
public sealed record PagePairDto(int Index, int? OldPage, int? NewPage, bool IsAddedPage, bool IsRemovedPage, string? Alignment,
    SummaryDto Summary, IReadOnlyList<ChangeDto> Changes);

public sealed record CompareDto(string Id, string OriginalId, string RevisionId, SummaryDto Total, IReadOnlyList<PagePairDto> Pages, IReadOnlyList<string> Warnings)
{
    public static CompareDto Of(CompareSession s) => new(s.Id, s.OriginalId, s.RevisionId, SummaryDto.Of(s.Result.Total),
        s.Result.Pages.Select((p, i) => new PagePairDto(i, p.OldPage, p.NewPage, p.IsAddedPage, p.IsRemovedPage,
            p.Result is { } r ? $"{r.Alignment.Method} (quality {r.Alignment.Quality:0.00})" : null,
            SummaryDto.Of(p.Summary),
            p.Result?.Regions.Select(c => ChangeDto.Of(i, c)).ToList() ?? [])).ToList(),
        s.Result.Warnings);
}

public sealed record CompareRequest(string OriginalId, string RevisionId, bool IgnoreTitleBlock = false);

/// <summary>내려받기 요청: △ 리비전 번호와 화면에서 지운 변경 ID들.</summary>
public sealed record ExportRequest(string RevisionLabel = "1", IReadOnlyList<string>? Excluded = null);
