using System.Globalization;
using System.Text.RegularExpressions;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using PdfSharp.Pdf;
using static DrawingReview.Engine.Pdf.PdfValues;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// 저장된 PDF 주석 → MarkupInfo(요약)와 편집 가능한 MarkupDraft. 다시 그리기(이동·크기·속성 변경)와 되돌리기에 쓴다.
/// 좌표는 PDF 원본 배열(/L, /Vertices, /InkList, /CL, /Rect, /RD)을 읽어 보이는 좌표로 바꾼다.
/// </summary>
internal static partial class AnnotationReader
{
    public static MarkupInfo Read(PdfDictionary annot, int pageIndex, PageGeometry g, string id)
    {
        var subtype = (Name(annot, "/Subtype") ?? "/Unknown").TrimStart('/');
        var kind = Classify(annot, subtype);
        var rect = Numbers(annot, "/Rect") ?? [0, 0, 0, 0];
        var isText = kind is MarkupKind.TextBox or MarkupKind.Callout;
        var content = Text(annot, "/Contents");
        return new MarkupInfo(
            Id: id,
            PageIndex: pageIndex,
            Kind: kind,
            PdfSubtype: subtype,
            Bounds: rect.Length >= 4 ? g.ToVisibleRect(rect[0], rect[1], rect[2], rect[3]) : new PageRect(0, 0, 0, 0),
            Author: Text(annot, "/T"),
            Subject: Text(annot, "/Subj"),
            Comment: content,
            ModifiedDate: Text(annot, "/M"),
            Draft: ReadDraft(annot, g, kind, subtype, content, isText),
            Status: ReadStatus(annot),
            IsLocked: ReadLocked(annot),
            CreatedDate: Text(annot, "/CreationDate"));
    }

    public static MarkupKind Classify(PdfDictionary annot, string subtype) => subtype switch
    {
        "FreeText" => Name(annot, "/IT") == "/FreeTextCallout" ? MarkupKind.Callout : MarkupKind.TextBox,
        "Line" => HasLineEnding(annot) ? MarkupKind.Arrow : MarkupKind.Line,
        "Square" => Name(annot, AnnotationWriter.KindKey) == "/Highlight" ? MarkupKind.Highlight : MarkupKind.Rectangle,
        "Circle" => MarkupKind.Ellipse,
        "Polygon" => (Dict(annot.Elements.ContainsKey("/BE") ? annot.Elements["/BE"] : null) is { } be && (Number(be, "/I") ?? 0) > 0)
            ? MarkupKind.Cloud : MarkupKind.Polygon,
        "Ink" => MarkupKind.Ink,
        "Highlight" => MarkupKind.Highlight,
        _ => MarkupKind.Other,
    };

    private static bool HasLineEnding(PdfDictionary annot)
    {
        if (Array(annot.Elements.ContainsKey("/LE") ? annot.Elements["/LE"] : null) is not { } le) return false;
        return le.Elements.Items.Any(item => Resolve(item) is PdfName n && n.Value != "/None");
    }

    public static string? ReadStatus(PdfDictionary annot) => Text(annot, AnnotationWriter.StatusKey);

    public static bool ReadLocked(PdfDictionary annot) => ((int)(Number(annot, "/F") ?? 0) & AnnotationWriter.LockedFlag) != 0;

    public static int Flags(PdfDictionary annot) => (int)(Number(annot, "/F") ?? 0);

    // ---- 도형별 읽기 ----

    private static MarkupDraft? ReadDraft(PdfDictionary annot, PageGeometry g, MarkupKind kind, string subtype, string? content, bool isText)
    {
        MarkupDraft? draft = kind switch
        {
            MarkupKind.TextBox => new TextBoxDraft(InnerRect(annot, g), content ?? ""),
            MarkupKind.Callout => ReadCallout(annot, g, content ?? ""),
            MarkupKind.Line or MarkupKind.Arrow => ReadLine(annot, g, kind == MarkupKind.Arrow),
            MarkupKind.Rectangle => new RectangleDraft(InnerRect(annot, g)),
            MarkupKind.Ellipse => new EllipseDraft(InnerRect(annot, g)),
            MarkupKind.Cloud or MarkupKind.Polygon => ReadPolygon(annot, g, kind),
            MarkupKind.Ink => ReadInk(annot, g),
            MarkupKind.Highlight when subtype == "Square" => new HighlightDraft(InnerRect(annot, g)),
            _ => null,
        };
        if (draft == null) return null;
        return draft with
        {
            Style = ReadStyle(annot, kind),
            Author = Text(annot, "/T"),
            Subject = Text(annot, "/Subj"),
            Comment = isText ? null : content,
        };
    }

    private static CalloutDraft ReadCallout(PdfDictionary annot, PageGeometry g, string text)
    {
        var box = InnerRect(annot, g);
        var cl = Numbers(annot, "/CL");
        if (cl == null || cl.Length < 4) return new CalloutDraft(box, text, box.Center);
        var pts = ToVisiblePoints(g, cl);
        PagePoint? knee = pts.Count >= 3 ? pts[1] : null;
        return new CalloutDraft(box, text, pts[0], knee);
    }

    private static LineDraft? ReadLine(PdfDictionary annot, PageGeometry g, bool arrow)
    {
        var l = Numbers(annot, "/L");
        if (l == null || l.Length < 4) return null;
        var pts = ToVisiblePoints(g, l);
        return new LineDraft(pts[0], pts[1], arrow);
    }

    private static CloudDraft? ReadPolygon(PdfDictionary annot, PageGeometry g, MarkupKind kind)
    {
        var v = Numbers(annot, "/Vertices");
        if (v == null || v.Length < 6) return null;
        double intensity = 0;
        if (kind == MarkupKind.Cloud)
            intensity = Dict(annot.Elements["/BE"]) is { } be ? Number(be, "/I") ?? 2 : 2;
        return new CloudDraft(ToVisiblePoints(g, v), intensity);
    }

    private static InkDraft? ReadInk(PdfDictionary annot, PageGeometry g)
    {
        if (Array(annot.Elements.ContainsKey("/InkList") ? annot.Elements["/InkList"] : null) is not { } ink) return null;
        var strokes = new List<IReadOnlyList<PagePoint>>();
        foreach (var item in ink.Elements.Items)
        {
            if (Array(item) is not { } stroke) continue;
            var nums = stroke.Elements.Items.Select(Number).Where(n => n.HasValue).Select(n => n!.Value).ToArray();
            if (nums.Length >= 2) strokes.Add(ToVisiblePoints(g, nums));
        }
        return strokes.Count > 0 ? new InkDraft(strokes) : null;
    }

    /// <summary>/Rect에서 /RD(안쪽 여백)를 뺀 실제 도형·글상자 영역 (보이는 좌표).</summary>
    private static PageRect InnerRect(PdfDictionary annot, PageGeometry g)
    {
        var r = Numbers(annot, "/Rect") is { Length: >= 4 } rr ? rr : [0, 0, 0, 0];
        var rd = Numbers(annot, "/RD") is { Length: >= 4 } dd ? dd : [0, 0, 0, 0];
        double x0 = Math.Min(r[0], r[2]) + rd[0], y0 = Math.Min(r[1], r[3]) + rd[1];
        double x1 = Math.Max(r[0], r[2]) - rd[2], y1 = Math.Max(r[1], r[3]) - rd[3];
        return g.ToVisibleRect(x0, y0, x1, y1);
    }

    private static List<PagePoint> ToVisiblePoints(PageGeometry g, double[] pdfXy)
    {
        var list = new List<PagePoint>(pdfXy.Length / 2);
        for (int i = 0; i + 1 < pdfXy.Length; i += 2) list.Add(g.ToVisible(pdfXy[i], pdfXy[i + 1]));
        return list;
    }

    // ---- 스타일 읽기 ----

    private static MarkupStyle ReadStyle(PdfDictionary annot, MarkupKind kind)
    {
        var style = MarkupStyle.Default;
        if (Color(annot, "/C") is { } stroke) style = style with { StrokeColor = stroke };
        if (Color(annot, "/IC") is { } fill) style = style with { FillColor = fill };
        var bs = Dict(annot.Elements.ContainsKey("/BS") ? annot.Elements["/BS"] : null);
        var width = (bs != null ? Number(bs, "/W") : null) ?? Numbers(annot, "/Border")?.ElementAtOrDefault(2);
        if (width is { } w) style = style with { LineWidth = w };
        if (Number(annot, "/CA") is { } ca) style = style with { Opacity = ca };

        if (kind is MarkupKind.TextBox or MarkupKind.Callout)
        {
            var da = Text(annot, "/DA") ?? "";
            if (DaRgb().Match(da) is { Success: true } rgb)
                style = style with { TextColor = new RgbColor(N(rgb.Groups[1].Value), N(rgb.Groups[2].Value), N(rgb.Groups[3].Value)) };
            else if (DaGray().Match(da) is { Success: true } gray)
                style = style with { TextColor = new RgbColor(N(gray.Groups[1].Value), N(gray.Groups[1].Value), N(gray.Groups[1].Value)) };
            if (DaFont().Match(da) is { Success: true } font) style = style with { FontSize = N(font.Groups[1].Value) };
            // 글상자의 테두리·지시선 색은 글자색과 같고, FreeText의 /C는 배경(채움) 색이다.
            style = style with { StrokeColor = style.TextColor, FillColor = Color(annot, "/C") };
        }
        if (kind == MarkupKind.Highlight) style = style with { FillColor = null, LineWidth = 0 };
        return style;
    }

    private static double N(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"([-+]?[\d.]+)\s+([-+]?[\d.]+)\s+([-+]?[\d.]+)\s+rg")]
    private static partial Regex DaRgb();

    [GeneratedRegex(@"(?<![\d.])([-+]?[\d.]+)\s+g(?![a-z])")]
    private static partial Regex DaGray();

    [GeneratedRegex(@"/\S+\s+([\d.]+)\s+Tf")]
    private static partial Regex DaFont();
}
