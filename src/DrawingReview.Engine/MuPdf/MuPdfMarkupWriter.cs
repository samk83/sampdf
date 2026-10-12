using System.Globalization;
using DrawingReview.Engine.Markups;
using MuPDF.NET;
using static DrawingReview.Engine.MuPdf.MuPdfCoordinates;

namespace DrawingReview.Engine.MuPdf;

/// <summary>MarkupDraft → 표준 PDF 주석. 외부 뷰어용 외형(/AP)도 MuPDF가 함께 만든다.</summary>
internal static class MuPdfMarkupWriter
{
    public static string Add(Document doc, Page page, MarkupDraft draft)
    {
        var s = draft.Style;
        Annot annot = draft switch
        {
            TextBoxDraft d => AddFreeText(page, d.Rect, d.Text, s, callout: null),
            CalloutDraft d => AddFreeText(page, d.TextRect, d.Text, s, CalloutPoints(page, d)),
            LineDraft d => page.AddLineAnnot(ToMu(d.Start), ToMu(d.End)),
            RectangleDraft d => page.AddRectAnnot(ToMu(d.Rect)),
            EllipseDraft d => page.AddCircleAnnot(ToMu(d.Rect)),
            CloudDraft d => page.AddPolygonAnnot(d.Vertices.Select(ToMu).ToArray()),
            InkDraft d => page.AddInkAnnot(d.Strokes.Select(st => st.Select(p => ToUnrotated(page, p)).ToArray()).ToArray()),
            HighlightDraft d => page.AddHighlightAnnot(ToUnrotated(page, d.Rect)),
            _ => throw new NotSupportedException($"Unsupported markup draft: {draft.GetType().Name}"),
        };

        var isFreeText = draft is TextBoxDraft or CalloutDraft;
        if (!isFreeText)
        {
            // 글상자는 생성 시 글자색을 지정한다. 나머지는 선 색·채움 색·선 굵기를 여기서 설정한다.
            if (draft is HighlightDraft)
                annot.SetColors(stroke: s.StrokeColor.ToArray());
            else
            {
                annot.SetColors(stroke: s.StrokeColor.ToArray(), fill: s.FillColor?.ToArray()!);
                annot.SetBorder(null!, width: (float)s.LineWidth,
                    clouds: draft is CloudDraft c ? (float)c.Intensity : -1);
            }
            if (draft is LineDraft { ArrowAtEnd: true })
                annot.SetLineEnds(PdfLineEnding.PDF_ANNOT_LE_NONE, PdfLineEnding.PDF_ANNOT_LE_CLOSED_ARROW);
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
        annot.Update(opacity: (float)s.Opacity);

        // MuPDF 기본 /NM(fitz-A0 …)은 페이지마다 겹칠 수 있으므로 문서 전체에서 고유한 ID로 바꾼다.
        var id = Guid.NewGuid().ToString("N");
        doc.XrefSetKey(annot.Xref, "NM", $"({id})");
        if (draft is TextBoxDraft)
        {
            // MuPDF.NET은 지시선이 없어도 /CL을 써 넣는다. 일반 글상자에는 필요 없으므로 지운다.
            doc.XrefSetKey(annot.Xref, "CL", "null");
        }
        return id;
    }

    private static Annot AddFreeText(Page page, Geometry.PageRect rect, string text, MarkupStyle s, Point[]? callout)
    {
        // FreeText는 회전 전 좌표를 받는다. rotate에 페이지 회전을 넘겨야 화면에서 글자가 똑바로 보인다.
        // borderColor는 richtext 모드에서만 허용되는데, richtext는 한글 글꼴 전체(약 20MB)를 넣으므로 쓰지 않는다.
        return page.AddFreeTextAnnot(
            ToUnrotated(page, rect), text,
            fontSize: (float)s.FontSize,
            textColor: s.TextColor.ToArray(),
            fillColor: s.FillColor?.ToArray()!,
            callout: callout!,
            lineEnd: PdfLineEnding.PDF_ANNOT_LE_OPEN_ARROW,
            rotate: page.Rotation);
    }

    private static Point[] CalloutPoints(Page page, CalloutDraft d)
    {
        // PDF 규격: /CL 의 첫 점이 화살촉, 마지막 점이 글상자에 닿는 점.
        var r = d.TextRect;
        var reference = d.Knee ?? d.ArrowTip;
        var attach = new Geometry.PagePoint(
            Math.Clamp(reference.X, r.X0, r.X1),
            Math.Clamp(reference.Y, r.Y0, r.Y1));
        var points = new List<Geometry.PagePoint> { d.ArrowTip };
        if (d.Knee is { } knee) points.Add(knee);
        points.Add(attach);
        return points.Select(p => ToUnrotated(page, p)).ToArray();
    }

    private static string PdfDate(DateTimeOffset t)
    {
        var offset = t.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        return "D:" + t.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            + $"{sign}{Math.Abs(offset.Hours):00}'{Math.Abs(offset.Minutes):00}'";
    }
}
