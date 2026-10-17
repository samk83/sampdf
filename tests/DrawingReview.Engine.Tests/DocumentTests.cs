using System.Diagnostics;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Persistence;
using DrawingReview.TestData;
using Xunit.Abstractions;
using static DrawingReview.Engine.Tests.TestHelpers;
using Pages = DrawingReview.TestData.VirtualDrawings.PlanSetPages;

namespace DrawingReview.Engine.Tests;

public class DocumentTests(PlanSetFixture fixture, ITestOutputHelper output) : IClassFixture<PlanSetFixture>
{
    [Fact]
    public void Page_info_reports_visible_size_after_rotation_and_crop()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        Assert.Equal(Pages.Count, doc.PageCount);

        var rotated = doc.GetPageInfo(Pages.Rotated90);
        Assert.Equal(90, rotated.Rotation);
        Assert.Equal(VirtualDrawings.ArchDWidth, rotated.Width, 1);   // 세로 용지가 가로로 보인다
        Assert.Equal(VirtualDrawings.ArchDHeight, rotated.Height, 1);

        var cropped = doc.GetPageInfo(Pages.CroppedA3);
        Assert.True(cropped.Width < VirtualDrawings.A3Width);
        Assert.True(cropped.Height < VirtualDrawings.A3Height);
    }

    /// <summary>렌더링 결과에서 빨간 사각형이 "보이는 위치"에 실제로 그려졌는지 픽셀로 확인.</summary>
    [Theory]
    [InlineData(Pages.FloorPlan)]
    [InlineData(Pages.Rotated90)]
    [InlineData(Pages.Rotated180)]
    [InlineData(Pages.Rotated270)]
    [InlineData(Pages.CroppedA3)]
    public void Rendered_markup_appears_at_visible_position(int pageIndex)
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var rect = new PageRect(200, 150, 320, 230);
        doc.AddMarkup(pageIndex, new RectangleDraft(rect)
        {
            Style = MarkupStyle.Default with { StrokeColor = RgbColor.Red, FillColor = RgbColor.Red, LineWidth = 2 },
        });

        var png = doc.RenderPagePng(pageIndex, zoom: 0.5);
        var pix = PngImage.Decode(png);
        var info = doc.GetPageInfo(pageIndex);
        Assert.InRange(pix.Width, (int)(info.Width * 0.5) - 1, (int)(info.Width * 0.5) + 1);

        bool IsRed(double x, double y)
        {
            var p = pix.GetPixel((int)(x * 0.5), (int)(y * 0.5));
            return p.R > 200 && p.G < 80 && p.B < 80;
        }
        Assert.True(IsRed(rect.Center.X, rect.Center.Y), "markup center should be red");
        Assert.False(IsRed(rect.X0 - 40, rect.Y0 - 40), "outside the markup should not be red");
    }

    [Fact]
    public void Render_can_exclude_markups()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var rect = new PageRect(200, 150, 320, 230);
        doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(rect) { Style = MarkupStyle.Default with { FillColor = RgbColor.Red } });
        var pix = PngImage.Decode(doc.RenderPagePng(Pages.FloorPlan, 0.5, includeMarkups: false));
        var p = pix.GetPixel(130, 95);
        Assert.False(p.R > 200 && p.G < 80, "markup must not be drawn when excluded");
    }

    [Fact]
    public void Text_search_finds_english_and_korean_in_visible_coordinates()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        Assert.NotEmpty(doc.SearchText(Pages.KoreanSpec, "치수"));
        Assert.NotEmpty(doc.SearchText(Pages.KoreanSpec, "CLEARANCE"));

        foreach (var page in new[] { Pages.FloorPlan, Pages.Rotated90, Pages.Rotated180, Pages.Rotated270, Pages.CroppedA3 })
        {
            var info = doc.GetPageInfo(page);
            var hit = Assert.Single(doc.SearchText(page, "TRUSS"));
            Assert.True(new PageRect(0, 0, info.Width, info.Height).Contains(hit), $"page {page}: hit {hit} outside page");
        }
        Assert.Empty(doc.SearchText(Pages.FloorPlan, ""));
    }

    [Fact]
    public void Saving_over_the_opened_file_works_because_source_is_not_locked()
    {
        using var dir = new TempDir();
        var path = dir.File("plan.pdf");
        File.WriteAllBytes(path, fixture.PlanSet);

        using (var doc = PdfEngine.Open(path))
        {
            doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(new PageRect(10, 10, 50, 50)));
            doc.SaveTo(path);
        }
        using var reopened = PdfEngine.Open(path);
        Assert.Contains(reopened.GetMarkups(Pages.FloorPlan), m => m.Kind == MarkupKind.Rectangle);
        Assert.Empty(Directory.GetFiles(dir.Path, "*.saving"));
    }

    [Fact]
    public void Encrypted_document_requires_password()
    {
        using var dir = new TempDir();
        var path = dir.File("locked.pdf");
        PdfTools.SaveEncrypted(fixture.PlanSet, path, ownerPassword: "owner", userPassword: "user");

        using var doc = PdfEngine.Open(path);
        Assert.True(doc.Security.NeedsPassword);
        Assert.NotEmpty(doc.Security.Warnings());
        Assert.False(doc.Authenticate("wrong"));
        Assert.True(doc.Authenticate("user"));
        Assert.Equal(Pages.Count, doc.PageCount);
    }

    [Fact]
    public void Plain_document_has_no_security_warnings()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        Assert.Empty(doc.Security.Warnings());
    }

    /// <summary>기획서 13장 성능 목표 측정용. 리눅스 CI 기준 참고치이며 Windows 기준 PC에서 다시 잰다.</summary>
    [Fact]
    public void Dense_vector_sheet_opens_and_renders_in_reasonable_time()
    {
        var bytes = VirtualDrawings.CreateDenseVectorSheet();
        var sw = Stopwatch.StartNew();
        using var doc = PdfEngine.Open(bytes);
        var info = doc.GetPageInfo(0);
        var fit = doc.RenderPagePng(0, 1600 / info.Width);
        var openAndFit = sw.ElapsedMilliseconds;
        sw.Restart();
        var detail = doc.RenderPagePng(0, 4, new PageRect(500, 500, 900, 800));
        var tile = sw.ElapsedMilliseconds;
        output.WriteLine($"dense sheet {bytes.Length / 1024} KB: open+fit render {openAndFit} ms, 400% tile {tile} ms");
        Assert.NotEmpty(fit);
        Assert.NotEmpty(detail);
        Assert.True(openAndFit < 15000, $"open+fit took {openAndFit} ms");
    }
}

public class SafeFileSaverTests
{
    [Fact]
    public void Failed_verification_keeps_original_and_removes_temp()
    {
        using var dir = new TempDir();
        var target = dir.File("doc.pdf");
        File.WriteAllText(target, "ORIGINAL");

        Assert.Throws<IOException>(() => SafeFileSaver.Save(target,
            temp => File.WriteAllText(temp, "BROKEN"),
            _ => throw new IOException("verification failed")));

        Assert.Equal("ORIGINAL", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void Failed_write_keeps_original()
    {
        using var dir = new TempDir();
        var target = dir.File("doc.pdf");
        File.WriteAllText(target, "ORIGINAL");

        Assert.Throws<InvalidOperationException>(() => SafeFileSaver.Save(target,
            temp => { File.WriteAllText(temp, "PART"); throw new InvalidOperationException("disk full"); },
            _ => { }));

        Assert.Equal("ORIGINAL", File.ReadAllText(target));
        Assert.Single(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void Empty_temp_file_is_rejected()
    {
        using var dir = new TempDir();
        var target = dir.File("doc.pdf");
        File.WriteAllText(target, "ORIGINAL");
        Assert.Throws<IOException>(() => SafeFileSaver.Save(target, temp => File.WriteAllText(temp, ""), _ => { }));
        Assert.Equal("ORIGINAL", File.ReadAllText(target));
    }

    [Fact]
    public void Successful_save_replaces_target()
    {
        using var dir = new TempDir();
        var target = dir.File("sub/doc.pdf");
        SafeFileSaver.Save(target, temp => File.WriteAllText(temp, "NEW"), _ => { });
        Assert.Equal("NEW", File.ReadAllText(target));
    }
}

public class RenderTests(PlanSetFixture fixture) : IClassFixture<PlanSetFixture>
{
    [Fact]
    public void Raw_render_reports_size_origin_and_region()
    {
        using var doc = TestHelpers.PdfEngine.Open(fixture.PlanSet);
        var full = doc.RenderPage(Pages.FloorPlan, 0.25);
        Assert.Equal(full.Width * 3, full.Stride);
        Assert.Equal(full.Height * full.Stride, full.Pixels.Length);
        Assert.Equal(0, full.OriginX);
        Assert.InRange(full.Width, 647, 649);

        var clip = doc.RenderPage(Pages.FloorPlan, 2, new PageRect(100, 50, 300, 150));
        Assert.Equal(200, clip.OriginX);
        Assert.Equal(100, clip.OriginY);
        Assert.InRange(clip.Width, 399, 401);
        Assert.InRange(clip.Region.X0, 99.9, 100.1);
        Assert.InRange(clip.Region.Y1, 149.5, 150.6);
    }
}

public class FlattenTests(PlanSetFixture fixture) : IClassFixture<PlanSetFixture>
{
    [Fact]
    public void Flattened_copy_has_markups_in_content_but_no_annotations()
    {
        using var dir = new TempDir();
        using var doc = TestHelpers.PdfEngine.Open(fixture.PlanSet);
        var rect = new PageRect(200, 150, 320, 230);
        doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(rect) { Style = MarkupStyle.Default with { FillColor = RgbColor.Red } });
        var flat = dir.File("flat.pdf");
        doc.ExportFlattened(flat);

        using var reopened = TestHelpers.PdfEngine.Open(flat);
        Assert.Equal(doc.PageCount, reopened.PageCount);
        Assert.DoesNotContain(reopened.GetMarkups(Pages.FloorPlan), m => m.Kind == MarkupKind.Rectangle);
        // 주석 없이 렌더링해도 빨간 사각형이 보인다 = 페이지 내용으로 들어감
        var pix = PngImage.Decode(reopened.RenderPagePng(Pages.FloorPlan, 0.5, includeMarkups: false));
        var p = pix.GetPixel(130, 95);
        Assert.True(p.R > 200 && p.G < 80, "flattened markup should be part of page content");
        // 열린 문서에는 주석이 그대로 남아 있다
        Assert.Contains(doc.GetMarkups(Pages.FloorPlan), m => m.Kind == MarkupKind.Rectangle);
    }
}

public class OutlineTests(PlanSetFixture fixture) : IClassFixture<PlanSetFixture>
{
    [Fact]
    public void Bookmarks_are_read_with_levels_and_zero_based_pages()
    {
        using var doc = TestHelpers.PdfEngine.Open(fixture.PlanSet);
        var outline = doc.GetOutline();
        Assert.Equal(6, outline.Count);
        Assert.Equal(new OutlineItem(1, "Architectural", 0), outline[0]);
        Assert.Equal(new OutlineItem(2, "A-102 Roof Framing Plan", 1), outline[2]);
        Assert.Equal("General Notes / 일반 사항", outline[5].Title);
    }

    [Fact]
    public void Words_are_reported_with_positions()
    {
        using var doc = TestHelpers.PdfEngine.Open(fixture.PlanSet);
        var words = doc.GetWords(Pages.FloorPlan);
        Assert.Contains(words, w => w.Text == "TRUSS");
        Assert.All(words, w => Assert.True(w.Bounds.Width > 0));
    }
}
