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

/// <summary>
/// PDF에서 읽어온 주석 한 개의 요약 (Review List 행의 원천 데이터).
/// Draft는 이 앱이 다시 편집할 수 있는 도구일 때만 채워진다 (Other 등은 null = 읽기 전용).
/// Status는 앱 전용 검토 상태(/DRStatus), IsLocked는 PDF 표준 잠금 플래그다.
/// </summary>
public sealed record MarkupInfo(
    string Id,
    int PageIndex,
    MarkupKind Kind,
    string PdfSubtype,
    PageRect Bounds,
    string? Author,
    string? Subject,
    string? Comment,
    string? ModifiedDate,
    MarkupDraft? Draft = null,
    string? Status = null,
    bool IsLocked = false,
    string? CreatedDate = null)
{
    public bool IsEditable => Draft != null;
}

/// <summary>검토 상태 기본값 (기획서 8장). 사용자 정의 상태는 후속.</summary>
public static class ReviewStatus
{
    public const string Open = "Open";
    public const string InProgress = "In Progress";
    public const string Resolved = "Resolved";
    public const string Reopened = "Reopened";

    public static readonly IReadOnlyList<string> All = [Open, InProgress, Resolved, Reopened];
}
