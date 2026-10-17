using System.Text;
using PdfSharp.Pdf;
using static DrawingReview.Engine.Pdf.PdfValues;

namespace DrawingReview.Engine.Pdf;

/// <summary>
/// 평탄화: 주석의 외형(/AP /N)을 페이지 내용에 그려 넣고 주석을 지운다. 폼 필드(위젯)도 같이 굽는다.
/// 외형이 없는 주석(예: 링크·말풍선)은 그릴 것이 없으므로 지우기만 한다.
/// </summary>
internal static class Flattener
{
    private const int HiddenFlag = 2, NoViewFlag = 32;

    public static void Flatten(PdfDocument doc)
    {
        for (int i = 0; i < doc.PageCount; i++) FlattenPage(doc, doc.Pages[i]);
        doc.Internals.Catalog.Elements.Remove("/AcroForm");
    }

    private static void FlattenPage(PdfDocument doc, PdfPage page)
    {
        if (Array(page.Elements.ContainsKey("/Annots") ? page.Elements["/Annots"] : null) is not { } annots) return;
        var ops = new StringBuilder();
        PdfDictionary? xobjects = null;
        var n = 0;
        foreach (var item in annots.Elements.Items)
        {
            if (Dict(item) is not { } annot) continue;
            if (Name(annot, "/Subtype") == "/Popup") continue;
            var flags = (int)(Number(annot, "/F") ?? 0);
            if ((flags & (HiddenFlag | NoViewFlag)) != 0) continue;
            if (NormalAppearance(annot) is not { } form || form.Stream == null) continue;
            if (Numbers(annot, "/Rect") is not { Length: 4 } rect || Numbers(form, "/BBox") is not { Length: 4 } bbox) continue;

            // PDF 규격 12.5.5: BBox를 Matrix로 변환한 경계를 Rect에 맞추는 행렬 A를 구해 "A cm /Fm Do"로 그린다
            var m = Numbers(form, "/Matrix") is { Length: 6 } mm ? mm : [1, 0, 0, 1, 0, 0];
            var corners = new[] { (bbox[0], bbox[1]), (bbox[2], bbox[1]), (bbox[2], bbox[3]), (bbox[0], bbox[3]) }
                .Select(p => (X: m[0] * p.Item1 + m[2] * p.Item2 + m[4], Y: m[1] * p.Item1 + m[3] * p.Item2 + m[5])).ToList();
            double tx0 = corners.Min(p => p.X), ty0 = corners.Min(p => p.Y), tx1 = corners.Max(p => p.X), ty1 = corners.Max(p => p.Y);
            double rx0 = Math.Min(rect[0], rect[2]), ry0 = Math.Min(rect[1], rect[3]), rx1 = Math.Max(rect[0], rect[2]), ry1 = Math.Max(rect[1], rect[3]);
            var sx = tx1 - tx0 > 1e-6 ? (rx1 - rx0) / (tx1 - tx0) : 1;
            var sy = ty1 - ty0 > 1e-6 ? (ry1 - ry0) / (ty1 - ty0) : 1;
            double ex = rx0 - tx0 * sx, ey = ry0 - ty0 * sy;

            if (form.Reference == null) doc.Internals.AddObject(form);
            xobjects ??= ResourceCategory(doc, PageResources(doc, page), "/XObject");
            string name;
            do name = $"/DRFlat{n++}"; while (xobjects.Elements.ContainsKey(name));
            xobjects.Elements[name] = Ref(form);
            ops.Append(Invariant($"q {Num(sx, "0.####")} 0 0 {Num(sy, "0.####")} {Num(ex)} {Num(ey)} cm {name} Do Q\n"));
        }

        if (ops.Length > 0)
        {
            // 기존 내용이 그래픽 상태를 되돌리지 않았어도 영향받지 않도록 q … Q 로 감싼다
            page.Contents.PrependContent().CreateStream(Ascii("q\n"));
            page.Contents.AppendContent().CreateStream(Ascii("Q\n" + ops));
        }
        page.Elements.Remove("/Annots");
    }

    /// <summary>/AP /N 외형 스트림. 상태별(/N << /On … /Off … >>)이면 /AS 상태 또는 첫 스트림을 고른다.</summary>
    private static PdfDictionary? NormalAppearance(PdfDictionary annot)
    {
        if (Dict(annot.Elements.ContainsKey("/AP") ? annot.Elements["/AP"] : null) is not { } ap) return null;
        if (Dict(ap.Elements.ContainsKey("/N") ? ap.Elements["/N"] : null) is not { } normal) return null;
        if (normal.Stream != null) return normal;
        var state = Name(annot, "/AS");
        if (state != null && normal.Elements.ContainsKey(state) && Dict(normal.Elements[state]) is { Stream: not null } chosen) return chosen;
        return normal.Elements.Keys.Select(k => Dict(normal.Elements[k])).FirstOrDefault(d => d?.Stream != null);
    }
}
