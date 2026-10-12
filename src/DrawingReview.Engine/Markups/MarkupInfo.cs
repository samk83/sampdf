using DrawingReview.Engine.Geometry;

namespace DrawingReview.Engine.Markups;

public enum MarkupKind
{
    TextBox,
    Callout,
    Line,
    Arrow,
    Rectangle,
    Ellipse,
    Cloud,
    Polygon,
    Ink,
    Highlight,
    /// <summary>링크·스탬프·폼 필드 등 이 앱이 아직 편집하지 않는 주석. 읽기만 하고 보존한다.</summary>
    Other,
}

/// <summary>PDF에서 읽어온 주석 한 개의 요약 (Review List 행의 원천 데이터).</summary>
public sealed record MarkupInfo(
    string Id,
    int PageIndex,
    MarkupKind Kind,
    string PdfSubtype,
    PageRect Bounds,
    string? Author,
    string? Subject,
    string? Comment,
    string? ModifiedDate);
