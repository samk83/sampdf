using DrawingReview.Engine.Geometry;

namespace DrawingReview.Engine.Markups;

/// <summary>
/// 새로 만들 마크업의 설명. 엔진 종류와 무관한 형태로 UI가 만들고,
/// PDF 엔진 어댑터가 표준 PDF 주석으로 변환해 저장한다.
/// </summary>
public abstract record MarkupDraft
{
    public MarkupStyle Style { get; init; } = MarkupStyle.Default;
    public string? Author { get; init; }
    public string? Subject { get; init; }
    public string? Comment { get; init; }
}

/// <summary>글상자 (PDF FreeText).</summary>
public sealed record TextBoxDraft(PageRect Rect, string Text) : MarkupDraft;

/// <summary>지시선이 있는 글상자 (PDF FreeText + /IT /FreeTextCallout). Knee는 꺾이는 점(선택).</summary>
public sealed record CalloutDraft(PageRect TextRect, string Text, PagePoint ArrowTip, PagePoint? Knee = null) : MarkupDraft;

/// <summary>선 또는 화살표 (PDF Line). ArrowAtEnd면 끝점에 화살촉.</summary>
public sealed record LineDraft(PagePoint Start, PagePoint End, bool ArrowAtEnd = false) : MarkupDraft;

/// <summary>사각형 (PDF Square).</summary>
public sealed record RectangleDraft(PageRect Rect) : MarkupDraft;

/// <summary>타원 (PDF Circle).</summary>
public sealed record EllipseDraft(PageRect Rect) : MarkupDraft;

/// <summary>구름 (PDF Polygon + /BE /S /C). Intensity는 구름 볼록함 정도(1~2 권장).</summary>
public sealed record CloudDraft(IReadOnlyList<PagePoint> Vertices, double Intensity = 2) : MarkupDraft
{
    public static CloudDraft FromRect(PageRect r, double intensity = 2) =>
        new([new(r.X0, r.Y0), new(r.X1, r.Y0), new(r.X1, r.Y1), new(r.X0, r.Y1)], intensity);
}

/// <summary>자유곡선 펜 (PDF Ink). 획(stroke)마다 점 목록 하나.</summary>
public sealed record InkDraft(IReadOnlyList<IReadOnlyList<PagePoint>> Strokes) : MarkupDraft;

/// <summary>
/// 영역 하이라이트. PDF에는 채움 사각형(Square) + Multiply 혼합 + 앱 전용 표시(/DRKind /Highlight)로 저장한다.
/// PDF Highlight 하위 유형은 글자 위 하이라이트용이라 도면 영역에서는 모양이 왜곡되므로 쓰지 않는다.
/// Style.StrokeColor가 채움 색이 된다. 보통 MarkupStyle.Highlighter를 쓴다.
/// </summary>
public sealed record HighlightDraft(PageRect Rect) : MarkupDraft;
