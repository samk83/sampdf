using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Persistence;
using MuPDF.NET;
using static DrawingReview.Engine.MuPdf.MuPdfCoordinates;
using PageInfo = DrawingReview.Engine.Geometry.PageInfo;

namespace DrawingReview.Engine.MuPdf;

internal sealed class MuPdfDocument : IPdfDocument
{
    private readonly Document _doc;

    public MuPdfDocument(byte[] pdfBytes, string? sourcePath)
    {
        _doc = new Document(stream: pdfBytes, fileType: "pdf");
        SourcePath = sourcePath;
    }

    public string? SourcePath { get; }

    public int PageCount => _doc.PageCount;

    public DocumentSecurityInfo Security =>
        new(_doc.IsEncrypted, _doc.NeedsPass, HasSignatures());

    public bool Authenticate(string password) => _doc.Authenticate(password) > 0;

    // MuPDF.NET의 Page 객체는 문서 구조가 바뀌면 무효화될 수 있으므로 매번 새로 가져온다.
    private Page GetPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _doc.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, $"Page index must be 0..{_doc.PageCount - 1}.");
        return _doc[pageIndex];
    }

    public PageInfo GetPageInfo(int pageIndex)
    {
        var page = GetPage(pageIndex);
        var r = page.Rect;
        string? label = null;
        try { label = page.GetLabel(); } catch (Exception) { /* 페이지 라벨이 없거나 읽을 수 없으면 생략 */ }
        return new PageInfo(pageIndex, r.Width, r.Height, page.Rotation, string.IsNullOrEmpty(label) ? null : label);
    }

    public byte[] RenderPagePng(int pageIndex, double zoom, PageRect? clip = null, bool includeMarkups = true)
    {
        var page = GetPage(pageIndex);
        var matrix = new Matrix((float)zoom, (float)zoom);
        var pix = page.GetPixmap(matrix: matrix, clip: clip is { } c ? new IRect(ToMu(c)) : null!, annots: includeMarkups);
        return pix.ToBytes("png");
    }

    public string AddMarkup(int pageIndex, MarkupDraft draft) =>
        MuPdfMarkupWriter.Add(_doc, GetPage(pageIndex), draft);

    public IReadOnlyList<MarkupInfo> GetMarkups(int pageIndex)
    {
        var page = GetPage(pageIndex);
        var result = new List<MarkupInfo>();
        foreach (var annot in page.GetAnnots())
        {
            var subtype = annot.Type.Item2;
            if (subtype is "Popup") continue; // 다른 주석의 말풍선 창. 별도 항목으로 보이지 않게 한다.
            var info = annot.Info;
            result.Add(new MarkupInfo(
                Id: string.IsNullOrEmpty(info.Id) ? $"xref:{annot.Xref}" : info.Id,
                PageIndex: pageIndex,
                Kind: ClassifyKind(annot, subtype),
                PdfSubtype: subtype,
                Bounds: FromUnrotated(page, annot.Rect),
                Author: NullIfEmpty(info.Title),
                Subject: NullIfEmpty(info.Subject),
                Comment: NullIfEmpty(info.Content),
                ModifiedDate: NullIfEmpty(info.ModDate)));
        }
        return result;
    }

    public IReadOnlyList<MarkupInfo> GetAllMarkups() =>
        Enumerable.Range(0, PageCount).SelectMany(GetMarkups).ToList();

    public bool DeleteMarkup(int pageIndex, string markupId)
    {
        var page = GetPage(pageIndex);
        foreach (var annot in page.GetAnnots())
        {
            var id = string.IsNullOrEmpty(annot.Info.Id) ? $"xref:{annot.Xref}" : annot.Info.Id;
            if (id != markupId) continue;
            page.DeleteAnnot(annot);
            return true;
        }
        return false;
    }

    public IReadOnlyList<PageRect> SearchText(int pageIndex, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return [];
        var page = GetPage(pageIndex);
        // 검색 결과도 회전 전 좌표로 돌아오므로 보이는 좌표로 바꾼다.
        return page.SearchFor(needle, maxHits: 1000).Select(q => FromUnrotated(page, q.Rect)).ToList();
    }

    public void SaveTo(string path)
    {
        var expectedPages = PageCount;
        var expectedAnnots = CountAnnotations(_doc);
        SafeFileSaver.Save(
            path,
            writeTemp: temp => _doc.Save(temp, garbage: 1, deflate: 1),
            verifyTemp: temp =>
            {
                // 저장된 파일을 다시 열어 페이지 수와 주석 수가 그대로인지 확인한다.
                using var check = new Document(temp);
                if (check.PageCount != expectedPages)
                    throw new IOException($"Save verification failed: expected {expectedPages} pages, found {check.PageCount}.");
                var annots = CountAnnotations(check);
                if (annots != expectedAnnots)
                    throw new IOException($"Save verification failed: expected {expectedAnnots} annotations, found {annots}.");
            });
    }

    private static int CountAnnotations(Document doc) =>
        Enumerable.Range(0, doc.PageCount).Sum(i => doc.GetPageAnnotXrefs(i).Count);

    private static MarkupKind ClassifyKind(Annot annot, string subtype)
    {
        switch (subtype)
        {
            case "FreeText":
                var it = annot.Parent!.Parent!.XrefGetKey(annot.Xref, "IT").Item2;
                return it is "/FreeTextCallout" ? MarkupKind.Callout : MarkupKind.TextBox;
            case "Line":
                var ends = annot.LineEnds;
                return ends is { } e && (e.Item1 != (int)PdfLineEnding.PDF_ANNOT_LE_NONE || e.Item2 != (int)PdfLineEnding.PDF_ANNOT_LE_NONE)
                    ? MarkupKind.Arrow : MarkupKind.Line;
            case "Square":
                var kind = annot.Parent!.Parent!.XrefGetKey(annot.Xref, MuPdfMarkupWriter.KindKey).Item2;
                return kind is "/Highlight" ? MarkupKind.Highlight : MarkupKind.Rectangle;
            case "Circle": return MarkupKind.Ellipse;
            case "Polygon": return annot.Border?.Clouds > 0 ? MarkupKind.Cloud : MarkupKind.Polygon;
            case "Ink": return MarkupKind.Ink;
            case "Highlight": return MarkupKind.Highlight;
            default: return MarkupKind.Other;
        }
    }

    private bool HasSignatures()
    {
        try { return _doc.GetSigFlags() > 0; }
        catch (Exception) { return false; }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    public void Dispose() => _doc.Close();
}
