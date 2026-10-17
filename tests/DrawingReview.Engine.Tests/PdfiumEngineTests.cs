using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.TestData;
using static DrawingReview.Engine.Tests.TestHelpers;
using Pages = DrawingReview.TestData.VirtualDrawings.PlanSetPages;

namespace DrawingReview.Engine.Tests;

/// <summary>PDFium + PDFsharp 엔진 고유 동작: 증분 저장, 암호 문서 편집, 그림 열기, 세로 글자.</summary>
public class PdfiumEngineTests(PlanSetFixture fixture) : IClassFixture<PlanSetFixture>
{
    private static readonly PageRect Box = new(400, 300, 700, 420);

    [Fact]
    public void Saved_file_is_an_incremental_update_that_keeps_the_original_bytes()
    {
        using var dir = new TempDir();
        var path = dir.File("plan.pdf");
        File.WriteAllBytes(path, fixture.PlanSet);
        using (var doc = PdfEngine.Open(path))
        {
            doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box));
            doc.SaveTo(path);
        }
        var saved = File.ReadAllBytes(path);
        Assert.True(saved.Length > fixture.PlanSet.Length);
        Assert.True(saved.AsSpan(0, fixture.PlanSet.Length).SequenceEqual(fixture.PlanSet), "original bytes must be kept as-is");
        Assert.EndsWith("%%EOF\n", System.Text.Encoding.ASCII.GetString(saved[^8..]));
    }

    [Fact]
    public void Repeated_edits_renders_and_saves_keep_every_markup()
    {
        using var dir = new TempDir();
        var path = dir.File("plan.pdf");
        File.WriteAllBytes(path, fixture.PlanSet);

        using (var doc = PdfEngine.Open(path))
        {
            var first = doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box));
            doc.RenderPage(Pages.FloorPlan, 0.25);                       // 편집 → 그리기 (증분 1)
            doc.AddMarkup(Pages.Rotated90, new EllipseDraft(Box));
            doc.SetMarkupStatus(Pages.FloorPlan, first, ReviewStatus.Resolved); // 같은 객체를 두 번 바꿈
            doc.SaveTo(path);                                             // 증분 2
        }
        using (var doc = PdfEngine.Open(path))
        {
            Assert.Equal(ReviewStatus.Resolved, Assert.Single(doc.GetMarkups(Pages.FloorPlan), m => m.Kind == MarkupKind.Rectangle).Status);
            doc.AddMarkup(Pages.FloorPlan, new LineDraft(new(10, 10), new(100, 100), ArrowAtEnd: true));
            var rect = doc.GetMarkups(Pages.FloorPlan).Single(m => m.Kind == MarkupKind.Rectangle);
            Assert.True(doc.DeleteMarkup(Pages.FloorPlan, rect.Id));
            doc.SaveTo(path);                                             // 증분 3 (이미 증분된 파일 위에)
        }
        using var reopened = PdfEngine.Open(path);
        var floor = reopened.GetMarkups(Pages.FloorPlan);
        Assert.DoesNotContain(floor, m => m.Kind == MarkupKind.Rectangle);
        Assert.Contains(floor, m => m.Kind == MarkupKind.Arrow);
        Assert.Contains(reopened.GetMarkups(Pages.Rotated90), m => m.Kind == MarkupKind.Ellipse);
        // 증분이 쌓인 파일도 PDFsharp가 제대로 읽는다 (평탄화는 사본을 PDFsharp로 다시 연다)
        var flat = dir.File("flat.pdf");
        reopened.ExportFlattened(flat);
        using var flattened = PdfEngine.Open(flat);
        Assert.Empty(flattened.GetMarkups(Pages.FloorPlan));
    }

    [Fact]
    public void Encrypted_document_can_be_edited_and_saved_with_the_same_password()
    {
        using var dir = new TempDir();
        var path = dir.File("locked.pdf");
        PdfTools.SaveEncrypted(fixture.PlanSet, path, ownerPassword: "owner", userPassword: "user");

        using (var doc = PdfEngine.Open(path))
        {
            Assert.True(doc.Authenticate("user"));
            Assert.True(doc.Security.IsEncrypted);
            doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box));
            Assert.NotEmpty(doc.RenderPagePng(Pages.FloorPlan, 0.2));
            doc.SaveTo(path);
        }
        using var reopened = PdfEngine.Open(path);
        Assert.True(reopened.Security.NeedsPassword, "the saved file must still ask for the password");
        Assert.False(reopened.Authenticate("wrong"));
        Assert.True(reopened.Authenticate("user"));
        Assert.Contains(reopened.GetMarkups(Pages.FloorPlan), m => m.Kind == MarkupKind.Rectangle);
    }

    [Fact]
    public void Png_and_jpeg_open_as_one_page_documents_sized_at_96_dpi()
    {
        using var sheet = PdfEngine.Open(ComparePairs.Sheet());
        var png = sheet.RenderPagePng(0, 150 / 72.0, includeMarkups: false);
        var pngImage = PngImage.Decode(png);

        using var pngDoc = ((Pdfium.PdfiumEngine)PdfEngine).OpenImage(png, "png");
        Assert.Equal(1, pngDoc.PageCount);
        var info = pngDoc.GetPageInfo(0);
        Assert.Equal(pngImage.Width * 72.0 / 96, info.Width, 1);
        Assert.Equal(pngImage.Height * 72.0 / 96, info.Height, 1);
        // 그림 내용이 실제로 들어갔다: 제목란 테두리(검정 선)가 보인다
        var render = PngImage.Decode(pngDoc.RenderPagePng(0, 96 / 72.0));
        Assert.Equal(pngImage.Width, render.Width);
        var dark = Enumerable.Range(0, render.Width).Count(x => render.GetPixel(x, render.Height / 2).R < 100);
        Assert.True(dark > 5, "rendered image should contain the drawing's dark lines");

        using var mat = OpenCvSharp.Cv2.ImDecode(png, OpenCvSharp.ImreadModes.Color);
        OpenCvSharp.Cv2.ImEncode(".jpg", mat, out var jpeg);
        using var jpgDoc = ((Pdfium.PdfiumEngine)PdfEngine).OpenImage(jpeg, "jpg");
        Assert.Equal(1, jpgDoc.PageCount);
        Assert.Equal(info.Width, jpgDoc.GetPageInfo(0).Width, 1);
        var jpgRender = PngImage.Decode(jpgDoc.RenderPagePng(0, 96 / 72.0));
        var jpgDark = Enumerable.Range(0, jpgRender.Width).Count(x => jpgRender.GetPixel(x, jpgRender.Height / 2).R < 100);
        Assert.InRange(jpgDark, dark - 10, dark + 10);
    }

    [Fact]
    public void Vertical_text_on_rotated_pages_is_read_as_words()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        foreach (var page in new[] { Pages.Rotated90, Pages.Rotated270 })
        {
            var words = doc.GetWords(page).Select(w => w.Text).ToList();
            Assert.Contains("EXISTING", words);
            Assert.Contains("W12x26", words);
            Assert.DoesNotContain("E", words);
        }
    }

    [Fact]
    public void Markup_ids_are_stable_across_save_and_reopen()
    {
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(Pages.FloorPlan, CloudDraft.FromRect(Box));
        using var reopened = SaveAndReopen(doc, dir);
        var cloud = Assert.Single(reopened.GetMarkups(Pages.FloorPlan), m => m.Kind == MarkupKind.Cloud);
        Assert.Equal(id, cloud.Id);
        Assert.True(reopened.SetMarkupLocked(Pages.FloorPlan, id, true));
        Assert.True(reopened.GetMarkups(Pages.FloorPlan).Single(m => m.Id == id).IsLocked);
    }
}

/// <summary>증분 저장이 xref 스트림(PDF 1.5) 원본과 끝에 줄바꿈이 없는 원본에서도 동작하는지.</summary>
public class IncrementalWriterTests
{
    private static readonly PageRect Box = new(100, 100, 300, 200);

    /// <summary>xref 스트림(압축 없음, /W [1 4 2])을 쓰는 가장 작은 한 장짜리 PDF를 손으로 만든다.</summary>
    private static byte[] XrefStreamPdf(bool trailingNewline)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 500 400] /Contents 4 0 R >>",
            "<< /Length 23 >>\nstream\n0 0 1 RG 10 10 m 490 390 l S\nendstream",
            "<< /Title (xref stream sample) >>",
        };
        var ms = new MemoryStream();
        void W(string s) => ms.Write(System.Text.Encoding.ASCII.GetBytes(s));
        W("%PDF-1.5\n%âãÏÓ\n".Replace("âãÏÓ", "")); // 헤더 (이진 표시 줄은 생략)
        var offsets = new List<long>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(ms.Position);
            W($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xrefNumber = objects.Length + 1;
        var xrefOffset = ms.Position;
        var rows = new MemoryStream();
        void Row(int type, long offset, int gen)
        {
            rows.WriteByte((byte)type);
            rows.WriteByte((byte)(offset >> 24)); rows.WriteByte((byte)(offset >> 16)); rows.WriteByte((byte)(offset >> 8)); rows.WriteByte((byte)offset);
            rows.WriteByte((byte)(gen >> 8)); rows.WriteByte((byte)gen);
        }
        Row(0, 0, 65535);
        foreach (var o in offsets) Row(1, o, 0);
        Row(1, xrefOffset, 0);
        W($"{xrefNumber} 0 obj\n<< /Type /XRef /Size {xrefNumber + 1} /W [1 4 2] /Root 1 0 R /Info 5 0 R /Length {rows.Length} >>\nstream\n");
        rows.Position = 0; rows.CopyTo(ms);
        W("\nendstream\nendobj\n");
        W($"startxref\n{xrefOffset}\n%%EOF");
        if (trailingNewline) W("\n");
        return ms.ToArray();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Xref_stream_original_gets_an_xref_stream_update_that_keeps_info_and_markups(bool trailingNewline)
    {
        var original = XrefStreamPdf(trailingNewline);
        using var dir = new TempDir();
        var path = dir.File("xs.pdf");
        File.WriteAllBytes(path, original);

        using (var doc = PdfEngine.Open(path))
        {
            Assert.Equal(1, doc.PageCount);
            doc.AddMarkup(0, new RectangleDraft(Box));
            doc.SaveTo(path);
        }
        var saved = File.ReadAllBytes(path);
        Assert.True(saved.AsSpan(0, original.Length).SequenceEqual(original));
        var tail = System.Text.Encoding.Latin1.GetString(saved, original.Length, saved.Length - original.Length);
        Assert.Contains("/Type /XRef", tail);                 // 같은 종류(xref 스트림)로 덧붙였다
        Assert.DoesNotContain("\ntrailer", tail);
        Assert.Contains("/Info 5 0 R", tail);                 // 원본 트레일러의 /Info를 잇는다
        Assert.Matches(@"/Prev \d+", tail);

        using var reopened = PdfEngine.Open(path);
        Assert.Contains(reopened.GetMarkups(0), m => m.Kind == MarkupKind.Rectangle);
        // 두 번째 증분도 쌓인다
        reopened.AddMarkup(0, new EllipseDraft(Box));
        var second = dir.File("xs2.pdf");
        reopened.SaveTo(second);
        using var third = PdfEngine.Open(second);
        Assert.Equal(2, third.GetMarkups(0).Count);
    }

    [Fact]
    public void Table_original_without_info_does_not_invent_an_info_object()
    {
        using var dir = new TempDir();
        using var doc = new PdfSharp.Pdf.PdfDocument();
        var page = PdfSketch.NewPage(doc, 400, 300);
        var bytes = PdfTools.ToBytes(doc);
        var path = dir.File("plain.pdf");
        File.WriteAllBytes(path, bytes);
        using (var opened = PdfEngine.Open(path))
        {
            opened.AddMarkup(0, new RectangleDraft(Box));
            opened.SaveTo(path);
        }
        var saved = File.ReadAllBytes(path);
        var tail = System.Text.Encoding.Latin1.GetString(saved, bytes.Length, saved.Length - bytes.Length);
        var originalHasInfo = System.Text.Encoding.Latin1.GetString(bytes).Contains("/Info ");
        Assert.Equal(originalHasInfo, tail.Contains("/Info "));
        using var reopened = PdfEngine.Open(path);
        Assert.Single(reopened.GetMarkups(0));
    }

    [Fact]
    public void Owner_password_only_document_stays_encrypted_after_save()
    {
        using var dir = new TempDir();
        var fixture = new PlanSetFixture();
        var path = dir.File("owner.pdf");
        PdfTools.SaveEncrypted(fixture.PlanSet, path, ownerPassword: "owner", userPassword: "");
        using (var doc = PdfEngine.Open(path))
        {
            Assert.False(doc.Security.NeedsPassword);
            Assert.True(doc.Security.IsEncrypted);
            doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box));
            doc.SaveTo(path);
        }
        using var reopened = PdfEngine.Open(path);
        Assert.True(reopened.Security.IsEncrypted, "the saved file must still be encrypted");
        Assert.False(reopened.Security.NeedsPassword);
        Assert.Contains(reopened.GetMarkups(Pages.FloorPlan), m => m.Kind == MarkupKind.Rectangle);
    }
}
