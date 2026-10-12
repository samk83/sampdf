using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.TestData;
using MuPDF.NET;
using static DrawingReview.Engine.Tests.TestHelpers;
using Pages = DrawingReview.TestData.VirtualDrawings.PlanSetPages;

namespace DrawingReview.Engine.Tests;

/// <summary>
/// 다른 뷰어(Acrobat, Bluebeam)가 읽을 수 있는 표준 구조로 저장되는지, PDF 원본 사전을 직접 열어 확인한다.
/// </summary>
public class PdfCompatibilityTests(PlanSetFixture fixture) : IClassFixture<PlanSetFixture>
{
    private static readonly PageRect Box = new(400, 300, 700, 420);

    private string SaveWith(TempDir dir, params MarkupDraft[] drafts)
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        foreach (var d in drafts) doc.AddMarkup(Pages.FloorPlan, d);
        var path = dir.File("compat.pdf");
        doc.SaveTo(path);
        return path;
    }

    private static string RawAnnot(Document raw, string subtype)
    {
        foreach (var x in raw.GetPageAnnotXrefs(Pages.FloorPlan))
        {
            var obj = raw.XrefObject(x.xref);
            if (obj.Contains($"/Subtype /{subtype}")) return obj;
        }
        throw new Xunit.Sdk.XunitException($"No /{subtype} annotation found");
    }

    [Fact]
    public void Every_new_markup_has_an_appearance_stream()
    {
        using var dir = new TempDir();
        var path = SaveWith(dir,
            new TextBoxDraft(Box, "TEXT"), CloudDraft.FromRect(Box), new InkDraft([[new(1, 1), new(50, 50)]]),
            new LineDraft(new(1, 1), new(90, 90), true), new HighlightDraft(Box), new EllipseDraft(Box));
        using var raw = new Document(path);
        foreach (var x in raw.GetPageAnnotXrefs(Pages.FloorPlan))
        {
            var obj = raw.XrefObject(x.xref);
            if (obj.Contains("/Subtype /Link") || obj.Contains("/Subtype /Text") || obj.Contains("/Subtype /Popup")) continue; // 기존 항목·말풍선 창
            Assert.Contains("/AP", obj);
        }
    }

    [Fact]
    public void Cloud_is_a_polygon_with_cloudy_border_effect()
    {
        using var dir = new TempDir();
        using var raw = new Document(SaveWith(dir, CloudDraft.FromRect(Box)));
        var obj = RawAnnot(raw, "Polygon");
        Assert.Contains("/BE", obj);
        Assert.Matches(@"/S\s*/C", obj);
    }

    [Fact]
    public void Callout_uses_standard_freetext_callout_keys()
    {
        using var dir = new TempDir();
        using var raw = new Document(SaveWith(dir, new CalloutDraft(Box, "CHECK", new PagePoint(300, 500))));
        var obj = RawAnnot(raw, "FreeText");
        Assert.Contains("/IT /FreeTextCallout", obj);
        Assert.Contains("/CL", obj);
    }

    [Fact]
    public void Plain_text_box_has_no_callout_line()
    {
        using var dir = new TempDir();
        using var raw = new Document(SaveWith(dir, new TextBoxDraft(Box, "TEXT")));
        var obj = RawAnnot(raw, "FreeText");
        Assert.DoesNotContain("/CL", obj);
        Assert.DoesNotContain("/FreeTextCallout", obj);
    }

    [Fact]
    public void Area_highlight_is_translucent_multiply_square()
    {
        using var dir = new TempDir();
        using var raw = new Document(SaveWith(dir, new HighlightDraft(Box) { Style = MarkupStyle.Highlighter }));
        var obj = RawAnnot(raw, "Square");
        Assert.Contains("/DRKind /Highlight", obj);
        Assert.Matches(@"/CA 0?\.4", obj);
        Assert.Contains("/BM /Multiply", obj);
    }

    [Fact]
    public void Arrow_has_closed_arrow_line_ending()
    {
        using var dir = new TempDir();
        using var raw = new Document(SaveWith(dir, new LineDraft(new(10, 10), new(200, 200), ArrowAtEnd: true)));
        Assert.Contains("/ClosedArrow", RawAnnot(raw, "Line"));
    }

    /// <summary>기획서 10장 7항: 알 수 없는 기존 주석과 링크를 조용히 삭제하지 않는다.</summary>
    [Fact]
    public void Existing_notes_and_links_are_preserved()
    {
        using var dir = new TempDir();
        var path = SaveWith(dir, new RectangleDraft(Box));
        using var raw = new Document(path);

        var floorLinks = raw[Pages.FloorPlan].GetLinks();
        Assert.Contains(floorLinks, l => l.Uri == "https://example.com/project-spec");
        var specLinks = raw[Pages.KoreanSpec].GetLinks();
        Assert.Contains(specLinks, l => l.Kind == LinkType.LINK_GOTO && l.Page == Pages.FloorPlan);

        using var doc = PdfEngine.Open(path);
        var note = Assert.Single(doc.GetMarkups(Pages.FloorPlan), m => m.PdfSubtype == "Text");
        Assert.Equal("Other Reviewer", note.Author);
        Assert.Equal(MarkupKind.Other, note.Kind);
    }

    [Fact]
    public void Saving_does_not_grow_file_unreasonably()
    {
        using var dir = new TempDir();
        var drafts = Enumerable.Range(0, 50)
            .Select(i => (MarkupDraft)new TextBoxDraft(new PageRect(50 + i * 10, 50 + i * 10, 300 + i * 10, 100 + i * 10), $"한글 주석 {i}"))
            .ToArray();
        var path = SaveWith(dir, drafts);
        var size = new System.IO.FileInfo(path).Length;
        // 한글 글꼴 전체를 넣는 방식(약 20MB)을 쓰지 않았는지 확인
        Assert.True(size < 1_000_000, $"Saved file is {size} bytes");
    }
}
