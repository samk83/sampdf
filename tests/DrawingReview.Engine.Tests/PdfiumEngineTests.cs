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
