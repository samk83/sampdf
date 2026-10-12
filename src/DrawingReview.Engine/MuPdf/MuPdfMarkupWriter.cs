using System.Globalization;
using DrawingReview.Engine.Markups;
using MuPDF.NET;
using static DrawingReview.Engine.MuPdf.MuPdfCoordinates;

namespace DrawingReview.Engine.MuPdf;

/// <summary>MarkupDraft → 표준 PDF 주석. 외부 뷰어용 외형(/AP)도 MuPDF가 함께 만든다.</summary>
internal static class MuPdfMarkupWriter
{
    /// <summary>
    /// 표준 PDF 하위 유형만으로 구분되지 않는 도구를 표시하는 앱 전용 키. 다른 뷰어는 모르는 키를 무시한다.
    /// </summary>
    public const string KindKey = "DRKind";

    public static string Add(Document doc, Page page, MarkupDraft draft, string? id = null)
    {
        var s = draft.Style;
        Annot annot = draft switch
        {
            TextBoxDraft d => AddFreeText(page, d.Rect, d.Text, s),
            CalloutDraft d => AddFreeText(page, d.TextRect, d.Text, s),
            LineDraft d => page.AddLineAnnot(ToMu(d.Start), ToMu(d.End)),
            RectangleDraft d => page.AddRectAnnot(ToMu(d.Rect)),
            EllipseDraft d => page.AddCircleAnnot(ToMu(d.Rect)),
            CloudDraft d => page.AddPolygonAnnot(d.Vertices.Select(ToMu).ToArray()),
            InkDraft d => page.AddInkAnnot(d.Strokes.Select(st => st.Select(p => ToUnrotated(page, p)).ToArray()).ToArray()),
            // 영역 하이라이트: 반투명 채움 사각형 + Multiply 혼합 (도면 선이 비쳐 보이도록).
            HighlightDraft d => page.AddRectAnnot(ToMu(d.Rect)),
            _ => throw new NotSupportedException($"Unsupported markup draft: {draft.GetType().Name}"),
        };

        var isFreeText = draft is TextBoxDraft or CalloutDraft;
        if (!isFreeText)
        {
            // 글상자는 생성 시 글자색을 지정한다. 나머지는 선 색·채움 색·선 굵기를 여기서 설정한다.
            if (draft is HighlightDraft)
            {
                annot.SetColors(stroke: s.StrokeColor.ToArray(), fill: s.StrokeColor.ToArray());
                annot.SetBorder(null!, width: 0);
                doc.XrefSetKey(annot.Xref, KindKey, "/Highlight");
            }
            else
            {
                annot.SetColors(stroke: s.StrokeColor.ToArray(), fill: s.FillColor?.ToArray()!);
                annot.SetBorder(null!, width: (float)s.LineWidth,
                    clouds: draft is CloudDraft c ? (float)c.Intensity : -1);
            }
            if (draft is LineDraft { ArrowAtEnd: true })
                annot.SetLineEnds(PdfLineEnding.PDF_ANNOT_LE_NONE, PdfLineEnding.PDF_ANNOT_LE_CLOSED_ARROW);
        }

        else
        {
            // 글상자 테두리(=지시선) 굵기. MuPDF는 지시선을 글자색으로 그린다.
            doc.XrefSetKey(annot.Xref, "BS", FormattableString.Invariant($"<< /W {s.LineWidth:0.###} >>"));
            if (draft is CalloutDraft callout)
                ApplyCallout(doc, page, annot.Xref, callout);
            else
                // MuPDF.NET은 지시선이 없어도 /CL을 써 넣는다. 일반 글상자에는 필요 없으므로 사전에서 지운다.
                annot.ToPdfAnnot().pdf_annot_obj().pdf_dict_dels("CL");
            annot = page.LoadAnnot(annot.Xref);
        }

        var now = PdfDate(DateTimeOffset.Now);
        var content = draft switch
        {
            TextBoxDraft d => d.Text,
            CalloutDraft d => d.Text,
            _ => draft.Comment,
        };
        annot.SetInfo(content: content ?? "", title: draft.Author ?? "", subject: draft.Subject ?? "",
            creationDate: now, modDate: now);
        annot.Update(opacity: (float)s.Opacity, blendMode: draft is HighlightDraft ? "Multiply" : null!);

        // MuPDF 기본 /NM(fitz-A0 …)은 페이지마다 겹칠 수 있으므로 문서 전체에서 고유한 ID로 바꾼다.
        id ??= Guid.NewGuid().ToString("N");
        doc.XrefSetKey(annot.Xref, "NM", PdfString(id));
        return id;
    }

    private static Annot AddFreeText(Page page, Geometry.PageRect rect, string text, MarkupStyle s)
    {
        // FreeText는 회전 전 좌표를 받는다. rotate에 페이지 회전을 넘겨야 화면에서 글자가 똑바로 보인다.
        // borderColor는 richtext 모드에서만 허용되는데, richtext는 한글 글꼴 전체(약 20MB)를 넣으므로 쓰지 않는다.
        return page.AddFreeTextAnnot(
            ToUnrotated(page, rect), text,
            fontSize: (float)s.FontSize,
            textColor: s.TextColor.ToArray(),
            fillColor: s.FillColor?.ToArray()!,
            rotate: page.Rotation);
    }

    /// <summary>
    /// 지시선(/IT /FreeTextCallout, /CL)을 PDF 사전에 직접 쓴다.
    /// MuPDF.NET의 callout 인자는 CropBox가 있는 페이지에서 지시선 위치를 잘못 계산하므로(0단계 검증) 쓰지 않는다.
    /// /Rect는 글상자+지시선 전체, /RD는 그 안에서 글상자까지의 여백이다.
    /// </summary>
    private static void ApplyCallout(Document doc, Page page, int xref, CalloutDraft d)
    {
        var r = d.TextRect;
        var reference = d.Knee ?? d.ArrowTip;
        var attach = new Geometry.PagePoint(Math.Clamp(reference.X, r.X0, r.X1), Math.Clamp(reference.Y, r.Y0, r.Y1));
        // PDF 규격: /CL 의 첫 점이 화살촉, 마지막 점이 글상자에 닿는 점.
        var visible = new List<Geometry.PagePoint> { d.ArrowTip };
        if (d.Knee is { } knee) visible.Add(knee);
        visible.Add(attach);
        var pts = visible.Select(p => ToPdfSpace(page, p)).ToList();

        var box = ParseRect(doc.XrefGetKey(xref, "Rect").Item2);
        double ux0 = Math.Min(box[0], pts.Min(p => p.X)), uy0 = Math.Min(box[1], pts.Min(p => p.Y));
        double ux1 = Math.Max(box[2], pts.Max(p => p.X)), uy1 = Math.Max(box[3], pts.Max(p => p.Y));

        doc.XrefSetKey(xref, "IT", "/FreeTextCallout");
        doc.XrefSetKey(xref, "LE", "/OpenArrow");
        doc.XrefSetKey(xref, "CL", "[" + string.Join(" ", pts.Select(p => Num(p.X) + " " + Num(p.Y))) + "]");
        doc.XrefSetKey(xref, "Rect", $"[{Num(ux0)} {Num(uy0)} {Num(ux1)} {Num(uy1)}]");
        doc.XrefSetKey(xref, "RD", $"[{Num(box[0] - ux0)} {Num(box[1] - uy0)} {Num(ux1 - box[2])} {Num(uy1 - box[3])}]");
    }

    private static double[] ParseRect(string pdfArray) =>
        pdfArray.Trim('[', ']', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>PDF 문자열 리터럴. 괄호·역슬래시를 이스케이프한다.</summary>
    public static string PdfString(string s) =>
        "(" + s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ")";

    public static string PdfDate(DateTimeOffset t)
    {
        var offset = t.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        return "D:" + t.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            + $"{sign}{Math.Abs(offset.Hours):00}'{Math.Abs(offset.Minutes):00}'";
    }
}
