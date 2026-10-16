using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.Compare;

/// <summary>리비전 표시 설정.</summary>
public sealed record RevisionMarkOptions
{
    /// <summary>△ 안에 넣을 리비전 번호 (예: "1", "B").</summary>
    public string RevisionLabel { get; init; } = "1";
    public RgbColor Color { get; init; } = RgbColor.Red;
    public double LineWidth { get; init; } = 1.5;
    /// <summary>변경 영역 바깥 여백 (pt).</summary>
    public double MarginPt { get; init; } = 6;
    /// <summary>△ 한 변의 길이 (pt).</summary>
    public double DeltaSizePt { get; init; } = 20;
    /// <summary>작성자 (PDF 주석의 /T).</summary>
    public string Author { get; init; } = "";
}

/// <summary>
/// 변경 영역을 리비전 도면 위의 표준 PDF 마크업으로 바꾼다 (웹 비교 화면·PDF 내려받기 공통).
/// - 글자 변경: 바뀐 단어마다 노란 하이라이트
/// - 그림 변경: 빨간 Revision Cloud (안에 바뀐 단어가 있으면 하이라이트도 함께)
/// - Cloud에만: 오른쪽 위에 △ 리비전 기호(삼각형 + 번호) (Owner 결정: 하이라이트에는 붙이지 않음)
/// 모두 표준 주석이라 Bluebeam·Acrobat에서 열고 수정할 수 있다.
/// </summary>
public static class RevisionMarks
{
    public const string CloudSubject = "Revision cloud";
    public const string HighlightSubject = "Revision text";
    public const string DeltaSubject = "Revision delta";

    public static List<MarkupDraft> Build(ChangeRegion change, RevisionMarkOptions o, double pageWidth, double pageHeight)
    {
        var drafts = new List<MarkupDraft>();
        var note = string.IsNullOrEmpty(change.Description) ? $"Change {change.Number}: {change.Kind}" : $"Change {change.Number}: {change.Description}";

        foreach (var h in change.TextHighlights)
            drafts.Add(new HighlightDraft(Clamp(h.Inflate(1), pageWidth, pageHeight))
                { Style = MarkupStyle.Highlighter, Subject = HighlightSubject, Comment = note, Author = o.Author });

        // 글자만 바뀐 곳은 하이라이트로 끝. 그림 변경(또는 칠할 글자가 없는 변경)은 Cloud + △
        if (change.Category == ChangeCategory.Text && change.TextHighlights.Count > 0) return drafts;
        var cloud = Clamp(change.Bounds.Inflate(o.MarginPt), pageWidth, pageHeight);
        drafts.Add(CloudDraft.FromRect(cloud) with
            { Style = Stroke(o), Subject = CloudSubject, Comment = note, Author = o.Author });
        // Cloud의 볼록한 호가 영역 밖으로 약 5pt 나오므로 △는 그만큼 더 띄운다
        drafts.AddRange(Delta(cloud.Inflate(5), o, pageWidth, pageHeight, note));
        return drafts;
    }

    /// <summary>여러 변경의 표시를 리비전 도면의 한 페이지에 넣는다. 변경 번호 → 만든 마크업 ID 목록.</summary>
    public static Dictionary<int, List<string>> Apply(Engine.Documents.IPdfDocument doc, int pageIndex, IEnumerable<ChangeRegion> changes, RevisionMarkOptions o)
    {
        var info = doc.GetPageInfo(pageIndex);
        var ids = new Dictionary<int, List<string>>();
        foreach (var c in changes)
            ids[c.Number] = Build(c, o, info.Width, info.Height).Select(d => doc.AddMarkup(pageIndex, d)).ToList();
        return ids;
    }

    /// <summary>△ 리비전 기호: 영역 오른쪽 위 바깥에 삼각형(테두리만)과 가운데 번호. 페이지 밖으로 나가면 안쪽으로 옮긴다.</summary>
    public static List<MarkupDraft> Delta(PageRect anchor, RevisionMarkOptions o, double pageWidth, double pageHeight, string note = "")
    {
        // 두 글자 이상(예: "12")이면 삼각형을 조금 키워 번호가 변에 닿지 않게 한다
        var s = o.DeltaSizePt * (1 + 0.3 * Math.Max(0, o.RevisionLabel.Length - 1));
        var h = s * Math.Sqrt(3) / 2;
        var cx = Math.Clamp(anchor.X1, s / 2 + 1, pageWidth - s / 2 - 1);
        var bottom = anchor.Y0 - 2;
        if (bottom - h < 1) bottom = Math.Min(pageHeight - 1, anchor.Y1 + 2 + h); // 위쪽 공간이 없으면 아래에
        var top = bottom - h;
        var triangle = new CloudDraft([new PagePoint(cx, top), new PagePoint(cx + s / 2, bottom), new PagePoint(cx - s / 2, bottom)], Intensity: 0)
            { Style = Stroke(o), Subject = DeltaSubject, Comment = note, Author = o.Author };

        // 번호 글상자: 삼각형 무게중심에 글자 가운데가 오도록 놓는다 (테두리·배경 없음).
        // PDF 글상자는 왼쪽 정렬이라, Helvetica 글자 폭(약 0.56 × 크기)과 안쪽 여백(약 0.8pt)으로 시작점을 계산한다.
        var font = Math.Max(6, s * 0.5);
        var textWidth = font * 0.56 * Math.Max(1, o.RevisionLabel.Length);
        var cy = top + h * 2 / 3;
        var x0 = cx - textWidth / 2 - 0.8;
        var label = new TextBoxDraft(new PageRect(x0, cy - font * 0.62, x0 + textWidth + 6, cy + font * 0.62), o.RevisionLabel)
        {
            Style = MarkupStyle.Default with { StrokeColor = o.Color, TextColor = o.Color, LineWidth = 0, FontSize = font, FillColor = null },
            Subject = DeltaSubject,
            Comment = note,
            Author = o.Author,
        };
        return [triangle, label];
    }

    private static MarkupStyle Stroke(RevisionMarkOptions o) => MarkupStyle.Default with { StrokeColor = o.Color, TextColor = o.Color, LineWidth = o.LineWidth };

    private static PageRect Clamp(PageRect r, double w, double h) =>
        new(Math.Max(0, r.X0), Math.Max(0, r.Y0), Math.Min(w, r.X1), Math.Min(h, r.Y1));
}
