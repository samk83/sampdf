using System.Globalization;
using System.Text.RegularExpressions;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using MuPDF.NET;

namespace DrawingReview.Engine.MuPdf;

/// <summary>
/// 저장된 PDF 주석 → 편집 가능한 MarkupDraft. 다시 그리기(이동·크기·속성 변경)와 되돌리기에 쓴다.
/// 좌표는 MuPDF.NET의 Vertices 속성 대신 PDF 원본 배열(/L, /Vertices, /InkList, /CL, /Rect, /RD)을 읽어
/// PDF 좌표 → 보이는 좌표로 직접 바꾼다 (회전 페이지에서 Vertices 값이 일관되지 않음 — 0단계 검증).
/// </summary>
internal static partial class MuPdfMarkupReader
{
    public const string StatusKey = "DRStatus";
    public const int LockedFlag = 128; // PDF 주석 플래그 비트 8 (Locked)

    public static MarkupDraft? ReadDraft(Document doc, Page page, Annot annot, MarkupKind kind, AnnotInfo info)
    {
        var x = annot.Xref;
        var style = ReadStyle(doc, x, kind);
        MarkupDraft? draft = kind switch
        {
            MarkupKind.TextBox => new TextBoxDraft(InnerRect(doc, page, x), info.Content ?? ""),
            MarkupKind.Callout => ReadCallout(doc, page, x, info.Content ?? ""),
            MarkupKind.Line or MarkupKind.Arrow => ReadLine(doc, page, x, kind == MarkupKind.Arrow),
            MarkupKind.Rectangle => new RectangleDraft(InnerRect(doc, page, x)),
            MarkupKind.Ellipse => new EllipseDraft(InnerRect(doc, page, x)),
            MarkupKind.Cloud or MarkupKind.Polygon => ReadPolygon(doc, page, x, kind),
            MarkupKind.Ink => ReadInk(doc, page, x),
            MarkupKind.Highlight when annot.Type.Item2 == "Square" => new HighlightDraft(InnerRect(doc, page, x)),
            _ => null,
        };
        if (draft == null) return null;
        var isText = kind is MarkupKind.TextBox or MarkupKind.Callout;
        return draft with
        {
            Style = style,
            Author = NullIfEmpty(info.Title),
            Subject = NullIfEmpty(info.Subject),
            Comment = isText ? null : NullIfEmpty(info.Content),
        };
    }

    public static string? ReadStatus(Document doc, int xref)
    {
        var (type, value) = doc.XrefGetKey(xref, StatusKey);
        return type == "string" && value.Length > 0 ? value : null;
    }

    public static bool ReadLocked(Annot annot) => (annot.Flags & LockedFlag) != 0;

    // ---- 도형별 읽기 ----

    private static CalloutDraft? ReadCallout(Document doc, Page page, int x, string text)
    {
        var box = InnerRect(doc, page, x);
        var cl = Numbers(doc, x, "CL");
        if (cl == null || cl.Length < 4) return new CalloutDraft(box, text, box.Center);
        var pts = ToVisiblePoints(page, cl);
        PagePoint? knee = pts.Count >= 3 ? pts[1] : null;
        return new CalloutDraft(box, text, pts[0], knee);
    }

    private static LineDraft? ReadLine(Document doc, Page page, int x, bool arrow)
    {
        var l = Numbers(doc, x, "L");
        if (l == null || l.Length < 4) return null;
        var pts = ToVisiblePoints(page, l);
        return new LineDraft(pts[0], pts[1], arrow);
    }

    private static CloudDraft? ReadPolygon(Document doc, Page page, int x, MarkupKind kind)
    {
        var v = Numbers(doc, x, "Vertices");
        if (v == null || v.Length < 6) return null;
        var intensity = kind == MarkupKind.Cloud ? Number(doc, x, "BE/I") ?? 2 : 0;
        return new CloudDraft(ToVisiblePoints(page, v), intensity);
    }

    private static InkDraft? ReadInk(Document doc, Page page, int x)
    {
        var (type, value) = doc.XrefGetKey(x, "InkList");
        if (type != "array") return null;
        var strokes = new List<IReadOnlyList<PagePoint>>();
        foreach (Match m in InnerArray().Matches(value))
        {
            var nums = ParseNumbers(m.Groups[1].Value);
            if (nums.Length >= 2) strokes.Add(ToVisiblePoints(page, nums));
        }
        return strokes.Count > 0 ? new InkDraft(strokes) : null;
    }

    /// <summary>/Rect에서 /RD(안쪽 여백)를 뺀 실제 도형·글상자 영역 (보이는 좌표).</summary>
    private static PageRect InnerRect(Document doc, Page page, int x)
    {
        var r = Numbers(doc, x, "Rect") ?? [0, 0, 0, 0];
        var rd = Numbers(doc, x, "RD") ?? [0, 0, 0, 0];
        double x0 = Math.Min(r[0], r[2]) + rd[0], y0 = Math.Min(r[1], r[3]) + rd[1];
        double x1 = Math.Max(r[0], r[2]) - rd[2], y1 = Math.Max(r[1], r[3]) - rd[3];
        var pts = ToVisiblePoints(page, [x0, y0, x1, y1]);
        return PageRect.FromPoints(pts);
    }

    private static List<PagePoint> ToVisiblePoints(Page page, double[] pdfXy)
    {
        var m = page.TransformationMatrix * page.RotationMatrix;
        var list = new List<PagePoint>(pdfXy.Length / 2);
        for (int i = 0; i + 1 < pdfXy.Length; i += 2)
        {
            var p = new Point((float)pdfXy[i], (float)pdfXy[i + 1]) * m;
            list.Add(new PagePoint(p.X, p.Y));
        }
        return list;
    }

    // ---- 스타일 읽기 ----

    private static MarkupStyle ReadStyle(Document doc, int x, MarkupKind kind)
    {
        var style = MarkupStyle.Default;
        if (Color(doc, x, "C") is { } stroke) style = style with { StrokeColor = stroke };
        if (Color(doc, x, "IC") is { } fill) style = style with { FillColor = fill };
        if ((Number(doc, x, "BS/W") ?? Numbers(doc, x, "Border")?.ElementAtOrDefault(2)) is { } w) style = style with { LineWidth = w };
        if (Number(doc, x, "CA") is { } ca) style = style with { Opacity = ca };

        if (kind is MarkupKind.TextBox or MarkupKind.Callout)
        {
            var (_, da) = doc.XrefGetKey(x, "DA");
            if (DaRgb().Match(da) is { Success: true } rgb)
                style = style with { TextColor = new RgbColor(N(rgb.Groups[1].Value), N(rgb.Groups[2].Value), N(rgb.Groups[3].Value)) };
            else if (DaGray().Match(da) is { Success: true } gray)
                style = style with { TextColor = new RgbColor(N(gray.Groups[1].Value), N(gray.Groups[1].Value), N(gray.Groups[1].Value)) };
            if (DaFont().Match(da) is { Success: true } font) style = style with { FontSize = N(font.Groups[1].Value) };
            // 글상자의 테두리·지시선 색은 글자색과 같고(MuPDF 동작), FreeText의 /C는 배경(채움) 색이다.
            style = style with { StrokeColor = style.TextColor, FillColor = Color(doc, x, "C") };
        }
        if (kind == MarkupKind.Highlight) style = style with { FillColor = null, LineWidth = 0 };
        return style;
    }

    private static RgbColor? Color(Document doc, int x, string key)
    {
        var c = Numbers(doc, x, key);
        return c?.Length switch
        {
            3 => new RgbColor(c[0], c[1], c[2]),
            1 => new RgbColor(c[0], c[0], c[0]),
            4 => new RgbColor((1 - c[0]) * (1 - c[3]), (1 - c[1]) * (1 - c[3]), (1 - c[2]) * (1 - c[3])),
            _ => null,
        };
    }

    // ---- PDF 값 파싱 ----

    private static double[]? Numbers(Document doc, int x, string key)
    {
        var (type, value) = doc.XrefGetKey(x, key);
        return type == "array" ? ParseNumbers(value) : null;
    }

    private static double? Number(Document doc, int x, string key)
    {
        var (type, value) = doc.XrefGetKey(x, key);
        return type is "int" or "float" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static double[] ParseNumbers(string s) =>
        NumberToken().Matches(s).Select(m => N(m.Value)).ToArray();

    private static double N(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    [GeneratedRegex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex NumberToken();

    [GeneratedRegex(@"\[([^\[\]]*)\]")]
    private static partial Regex InnerArray();

    [GeneratedRegex(@"([-+]?[\d.]+)\s+([-+]?[\d.]+)\s+([-+]?[\d.]+)\s+rg")]
    private static partial Regex DaRgb();

    [GeneratedRegex(@"(?<![\d.])([-+]?[\d.]+)\s+g(?![a-z])")]
    private static partial Regex DaGray();

    [GeneratedRegex(@"/\S+\s+([\d.]+)\s+Tf")]
    private static partial Regex DaFont();
}
