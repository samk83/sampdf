using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using static DrawingReview.Engine.Pdf.PdfValues;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// MarkupDraft → 표준 PDF 주석 사전 + 외형(/AP). 다른 뷰어가 그대로 읽을 수 있는 표준 키만 쓰고,
/// 표준 하위 유형만으로 구분되지 않는 도구는 앱 전용 키(/DRKind)로 표시한다 (다른 뷰어는 모르는 키를 무시한다).
/// </summary>
internal static class AnnotationWriter
{
    /// <summary>표준 PDF 하위 유형만으로 구분되지 않는 도구를 표시하는 앱 전용 키.</summary>
    public const string KindKey = "/DRKind";
    public const string StatusKey = "/DRStatus";
    public const int LockedFlag = 128; // PDF 주석 플래그 비트 8 (Locked)
    private const int PrintFlag = 4;

    /// <summary>주석을 만들어 페이지 /Annots에 넣고 ID를 돌려준다.</summary>
    public static string Add(PdfDocument doc, PdfPage page, PageGeometry g, MarkupDraft draft, string? id = null)
    {
        id ??= Guid.NewGuid().ToString("N");
        var s = draft.Style;
        var annot = new PdfDictionary(doc);
        annot.Elements.SetName("/Type", "/Annot");
        annot.Elements.SetName("/Subtype", draft switch
        {
            TextBoxDraft or CalloutDraft => "/FreeText",
            LineDraft => "/Line",
            RectangleDraft or HighlightDraft => "/Square",
            EllipseDraft => "/Circle",
            CloudDraft => "/Polygon",
            InkDraft => "/Ink",
            _ => throw new NotSupportedException($"Unsupported markup draft: {draft.GetType().Name}"),
        });

        var appearance = AppearanceBuilder.Build(draft, g);
        WriteGeometry(doc, annot, g, draft, appearance);
        WriteStyle(doc, annot, draft, s);

        var now = Date(DateTimeOffset.Now);
        var content = draft switch { TextBoxDraft d => d.Text, CalloutDraft d => d.Text, _ => draft.Comment };
        if (!string.IsNullOrEmpty(content)) annot.Elements["/Contents"] = String(content);
        if (!string.IsNullOrEmpty(draft.Author)) annot.Elements["/T"] = String(draft.Author);
        if (!string.IsNullOrEmpty(draft.Subject)) annot.Elements["/Subj"] = String(draft.Subject);
        annot.Elements.SetString("/CreationDate", now);
        annot.Elements.SetString("/M", now);
        annot.Elements.SetInteger("/F", PrintFlag);
        annot.Elements.SetString("/NM", id);
        annot.Elements.SetReference("/P", page);

        AddObject(doc, annot);
        WriteAppearance(doc, annot, appearance, s, draft is HighlightDraft);

        var annots = Array(page.Elements.ContainsKey("/Annots") ? page.Elements["/Annots"] : null);
        if (annots == null)
        {
            annots = new PdfArray(doc);
            page.Elements["/Annots"] = annots;
        }
        annots.Elements.Add(Ref(annot));
        TouchAnnotsOwner(doc, page);
        return id;
    }

    private static void WriteGeometry(PdfDocument doc, PdfDictionary annot, PageGeometry g, MarkupDraft draft, Appearance ap)
    {
        var (bx0, by0, bx1, by1) = ap.BBox;
        switch (draft)
        {
            case TextBoxDraft d:
                SetRect(doc, annot, g.ToPdfRect(d.Rect));
                break;
            case CalloutDraft d:
                {
                    // /Rect는 글상자+지시선 전체, /RD는 그 안에서 글상자까지의 여백 (PDF 규격)
                    var box = g.ToPdfRect(d.TextRect);
                    var cl = AppearanceBuilder.CalloutPoints(g, d).Select(g.ToPdf).ToList();
                    double x0 = Math.Min(Math.Min(box.X0, cl.Min(p => p.X)), bx0), y0 = Math.Min(Math.Min(box.Y0, cl.Min(p => p.Y)), by0);
                    double x1 = Math.Max(Math.Max(box.X1, cl.Max(p => p.X)), bx1), y1 = Math.Max(Math.Max(box.Y1, cl.Max(p => p.Y)), by1);
                    SetRect(doc, annot, (x0, y0, x1, y1));
                    annot.Elements["/RD"] = NumberArray(doc, [box.X0 - x0, box.Y0 - y0, x1 - box.X1, y1 - box.Y1]);
                    annot.Elements.SetName("/IT", "/FreeTextCallout");
                    annot.Elements.SetName("/LE", "/OpenArrow");
                    annot.Elements["/CL"] = NumberArray(doc, cl.SelectMany(p => new[] { p.X, p.Y }));
                    break;
                }
            case LineDraft d:
                {
                    var a = g.ToPdf(d.Start);
                    var b = g.ToPdf(d.End);
                    annot.Elements["/L"] = NumberArray(doc, [a.X, a.Y, b.X, b.Y]);
                    if (d.ArrowAtEnd)
                    {
                        var le = new PdfArray(doc);
                        le.Elements.Add(new PdfName("/None"));
                        le.Elements.Add(new PdfName("/ClosedArrow"));
                        annot.Elements["/LE"] = le;
                    }
                    SetRect(doc, annot, ap.BBox);
                    break;
                }
            case RectangleDraft or EllipseDraft:
                {
                    var pad = AppearanceBuilder.RectPadding(draft.Style);
                    SetRect(doc, annot, ap.BBox);
                    if (pad > 0) annot.Elements["/RD"] = NumberArray(doc, [pad, pad, pad, pad]);
                    break;
                }
            case HighlightDraft:
                SetRect(doc, annot, ap.BBox);
                break;
            case CloudDraft d:
                {
                    annot.Elements["/Vertices"] = NumberArray(doc, d.Vertices.Select(g.ToPdf).SelectMany(p => new[] { p.X, p.Y }));
                    if (d.Intensity > 0)
                    {
                        var be = new PdfDictionary(doc);
                        be.Elements.SetName("/S", "/C");
                        be.Elements.SetInteger("/I", (int)Math.Round(Math.Clamp(d.Intensity, 1, 2)));
                        annot.Elements["/BE"] = be;
                    }
                    SetRect(doc, annot, ap.BBox);
                    break;
                }
            case InkDraft d:
                {
                    var ink = new PdfArray(doc);
                    foreach (var stroke in d.Strokes)
                        ink.Elements.Add(NumberArray(doc, stroke.Select(g.ToPdf).SelectMany(p => new[] { p.X, p.Y })));
                    annot.Elements["/InkList"] = ink;
                    SetRect(doc, annot, ap.BBox);
                    break;
                }
        }
    }

    private static void WriteStyle(PdfDocument doc, PdfDictionary annot, MarkupDraft draft, MarkupStyle s)
    {
        var bs = new PdfDictionary(doc);
        bs.Elements.SetName("/Type", "/Border");
        switch (draft)
        {
            case TextBoxDraft or CalloutDraft:
                // FreeText: 글자색·크기는 /DA, 배경은 /C, 테두리(=지시선) 굵기는 /BS. 테두리 색은 글자색과 같다.
                annot.Elements.SetString("/DA", Invariant($"{Num(s.TextColor.R)} {Num(s.TextColor.G)} {Num(s.TextColor.B)} rg {StandardFonts.HelveticaName} {Num(s.FontSize)} Tf"));
                annot.Elements.SetInteger("/Q", 0);
                if (s.FillColor is { } fill) annot.Elements["/C"] = ColorArray(doc, fill);
                bs.Elements.SetReal("/W", Math.Round(s.LineWidth, 3));
                break;
            case HighlightDraft:
                // 영역 하이라이트: 반투명 채움 사각형 + Multiply 혼합 (도면 선이 비쳐 보이도록)
                annot.Elements["/C"] = ColorArray(doc, s.StrokeColor);
                annot.Elements["/IC"] = ColorArray(doc, s.StrokeColor);
                annot.Elements.SetName(KindKey, "/Highlight");
                annot.Elements.SetName("/BM", "/Multiply");
                bs.Elements.SetInteger("/W", 0);
                break;
            default:
                annot.Elements["/C"] = ColorArray(doc, s.StrokeColor);
                if (s.FillColor is { } ic) annot.Elements["/IC"] = ColorArray(doc, ic);
                bs.Elements.SetReal("/W", Math.Round(s.LineWidth, 3));
                break;
        }
        annot.Elements["/BS"] = bs;
        if (s.Opacity < 1) annot.Elements.SetReal("/CA", Math.Round(s.Opacity, 3));
    }

    private static void WriteAppearance(PdfDocument doc, PdfDictionary annot, Appearance ap, MarkupStyle s, bool multiply)
    {
        var form = Stream(doc, Ascii(ap.Content));
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements.SetInteger("/FormType", 1);
        var (x0, y0, x1, y1) = ap.BBox;
        form.Elements["/BBox"] = NumberArray(doc, [x0, y0, x1, y1]);

        var resources = new PdfDictionary(doc);
        if (ap.UsesHelvetica || ap.UsesKorean)
        {
            var fonts = new PdfDictionary(doc);
            if (ap.UsesHelvetica) fonts.Elements[StandardFonts.HelveticaName] = Ref(StandardFonts.GetOrCreateHelvetica(doc));
            if (ap.UsesKorean) fonts.Elements[StandardFonts.KoreanName] = Ref(KoreanCidFont.GetOrCreate(doc));
            resources.Elements["/Font"] = fonts;
        }
        if (ap.UsesGraphicsState)
        {
            var gs = new PdfDictionary(doc);
            gs.Elements.SetName("/Type", "/ExtGState");
            gs.Elements.SetReal("/CA", Math.Round(s.Opacity, 3));
            gs.Elements.SetReal("/ca", Math.Round(s.Opacity, 3));
            if (multiply) gs.Elements.SetName("/BM", "/Multiply");
            var states = new PdfDictionary(doc);
            states.Elements[AppearanceBuilder.GraphicsStateName] = gs;
            resources.Elements["/ExtGState"] = states;
        }
        form.Elements["/Resources"] = resources;

        var apDict = new PdfDictionary(doc);
        apDict.Elements.SetReference("/N", form);
        annot.Elements["/AP"] = apDict;
    }

    private static void SetRect(PdfDocument doc, PdfDictionary annot, (double X0, double Y0, double X1, double Y1) r) =>
        annot.Elements["/Rect"] = NumberArray(doc, [r.X0, r.Y0, r.X1, r.Y1]);

    /// <summary>/Annots 배열을 바꿨을 때: 배열이 간접 객체면 그 객체를, 아니면 페이지를 증분 저장 대상으로 기억한다.</summary>
    public static void TouchAnnotsOwner(PdfDocument doc, PdfPage page)
    {
        if (page.Elements["/Annots"] is PdfReference r && r.Value is PdfObject owner) Touch(doc, owner);
        else Touch(doc, page);
    }

    // ---- 상태·잠금 ----

    public static void SetStatus(PdfDocument doc, PdfDictionary annot, string? status)
    {
        if (status == null) annot.Elements.Remove(StatusKey);
        else annot.Elements[StatusKey] = String(status);
        annot.Elements.SetString("/M", Date(DateTimeOffset.Now));
        Touch(doc, annot);
    }

    public static void SetLocked(PdfDocument doc, PdfDictionary annot, bool locked)
    {
        var flags = (int)(Number(annot, "/F") ?? 0);
        annot.Elements.SetInteger("/F", locked ? flags | LockedFlag : flags & ~LockedFlag);
        Touch(doc, annot);
    }
}
