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

    public RenderedImage RenderPage(int pageIndex, double zoom, PageRect? clip = null, bool includeMarkups = true)
    {
        var page = GetPage(pageIndex);
        var matrix = new Matrix((float)zoom, (float)zoom);
        var pix = page.GetPixmap(matrix: matrix, clip: clip is { } c ? new IRect(ToMu(c)) : null!, alpha: false, annots: includeMarkups);
        return new RenderedImage(pix.W, pix.H, pix.Stride, pix.Samples, pix.X, pix.Y, zoom);
    }

    public string AddMarkup(int pageIndex, MarkupDraft draft, string? id = null) =>
        MuPdfMarkupWriter.Add(_doc, GetPage(pageIndex), draft, id);

    public bool ReplaceMarkup(int pageIndex, string markupId, MarkupDraft draft)
    {
        var page = GetPage(pageIndex);
        if (FindAnnot(page, markupId) is not { } old) return false;
        var status = MuPdfMarkupReader.ReadStatus(_doc, old.Xref);
        var locked = MuPdfMarkupReader.ReadLocked(old);
        var created = old.Info.CreationDate;
        page.DeleteAnnot(old);
        page = GetPage(pageIndex);
        MuPdfMarkupWriter.Add(_doc, page, draft, markupId);
        if (FindAnnot(GetPage(pageIndex), markupId) is { } fresh)
        {
            if (!string.IsNullOrEmpty(created)) _doc.XrefSetKey(fresh.Xref, "CreationDate", MuPdfMarkupWriter.PdfString(created));
            if (status != null) _doc.XrefSetKey(fresh.Xref, MuPdfMarkupReader.StatusKey, MuPdfMarkupWriter.PdfString(status));
            if (locked) fresh.SetFlags(fresh.Flags | MuPdfMarkupReader.LockedFlag);
        }
        return true;
    }

    public bool SetMarkupStatus(int pageIndex, string markupId, string? status)
    {
        if (FindAnnot(GetPage(pageIndex), markupId) is not { } annot) return false;
        _doc.XrefSetKey(annot.Xref, MuPdfMarkupReader.StatusKey, status == null ? "null" : MuPdfMarkupWriter.PdfString(status));
        _doc.XrefSetKey(annot.Xref, "M", MuPdfMarkupWriter.PdfString(MuPdfMarkupWriter.PdfDate(DateTimeOffset.Now)));
        return true;
    }

    public bool SetMarkupLocked(int pageIndex, string markupId, bool locked)
    {
        if (FindAnnot(GetPage(pageIndex), markupId) is not { } annot) return false;
        var flags = locked ? annot.Flags | MuPdfMarkupReader.LockedFlag : annot.Flags & ~MuPdfMarkupReader.LockedFlag;
        annot.SetFlags(flags);
        return true;
    }

    private static string IdOf(Annot annot) => string.IsNullOrEmpty(annot.Info.Id) ? $"xref:{annot.Xref}" : annot.Info.Id;

    private static Annot? FindAnnot(Page page, string markupId) =>
        page.GetAnnots().FirstOrDefault(a => a.Type.Item2 != "Popup" && IdOf(a) == markupId);

    public IReadOnlyList<MarkupInfo> GetMarkups(int pageIndex)
    {
        var page = GetPage(pageIndex);
        var result = new List<MarkupInfo>();
        foreach (var annot in page.GetAnnots())
        {
            var subtype = annot.Type.Item2;
            if (subtype is "Popup") continue; // 다른 주석의 말풍선 창. 별도 항목으로 보이지 않게 한다.
            var info = annot.Info;
            var kind = ClassifyKind(annot, subtype);
            result.Add(new MarkupInfo(
                Id: IdOf(annot),
                PageIndex: pageIndex,
                Kind: kind,
                PdfSubtype: subtype,
                Bounds: FromUnrotated(page, annot.Rect),
                Author: NullIfEmpty(info.Title),
                Subject: NullIfEmpty(info.Subject),
                Comment: NullIfEmpty(info.Content),
                ModifiedDate: NullIfEmpty(info.ModDate),
                Draft: MuPdfMarkupReader.ReadDraft(_doc, page, annot, kind, info),
                Status: MuPdfMarkupReader.ReadStatus(_doc, annot.Xref),
                IsLocked: MuPdfMarkupReader.ReadLocked(annot),
                CreatedDate: NullIfEmpty(info.CreationDate)));
        }
        return result;
    }

    public IReadOnlyList<MarkupInfo> GetAllMarkups() =>
        Enumerable.Range(0, PageCount).SelectMany(GetMarkups).ToList();

    public bool DeleteMarkup(int pageIndex, string markupId)
    {
        var page = GetPage(pageIndex);
        if (FindAnnot(page, markupId) is not { } annot) return false;
        page.DeleteAnnot(annot);
        return true;
    }

    public IReadOnlyList<OutlineItem> GetOutline()
    {
        try
        {
            var toc = _doc.GetToc(simple: true);
            // 제목은 PDF 객체에서 직접 읽는다 (GetToc 제목은 Windows에서 한글이 깨짐 — MuPdfOutlineTitles 참고).
            // 개수가 다르면(특이한 목차 구조) GetToc 제목을 그대로 쓴다.
            var titles = MuPdfOutlineTitles.Read(_doc);
            var useTitles = titles != null && titles.Count == toc.Count;
            var list = new List<OutlineItem>();
            for (int i = 0; i < toc.Count; i++)
            {
                var t = toc[i];
                list.Add(new OutlineItem(t.Item1, useTitles ? titles![i] : t.Item2 ?? "", t.Item3 - 1)); // MuPDF 페이지 번호는 1부터
            }
            return list;
        }
        catch (Exception)
        {
            return []; // 손상된 목차는 무시하고 문서는 그대로 보여준다
        }
    }

    public IReadOnlyList<TextWord> GetWords(int pageIndex)
    {
        var page = GetPage(pageIndex);
        return page.GetTextWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.word))
            .Select(w => new TextWord(w.word, FromUnrotated(page, new Rect(w.x0, w.y0, w.x1, w.y1))))
            .ToList();
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

    public void ExportFlattened(string path)
    {
        var bytes = _doc.Write(garbage: false, deflate: true);
        var pages = PageCount;
        SafeFileSaver.Save(
            path,
            writeTemp: temp =>
            {
                using var copy = new Document(stream: bytes, fileType: "pdf");
                copy.Bake(annots: true, widgets: true);
                copy.Save(temp, garbage: 3, deflate: 1);
            },
            verifyTemp: temp =>
            {
                using var check = new Document(temp);
                if (check.PageCount != pages)
                    throw new IOException($"Flatten verification failed: expected {pages} pages, found {check.PageCount}.");
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
