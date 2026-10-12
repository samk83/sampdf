using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.TestData;
using static DrawingReview.Engine.Tests.TestHelpers;
using Pages = DrawingReview.TestData.VirtualDrawings.PlanSetPages;

namespace DrawingReview.Engine.Tests;

/// <summary>기획서 10장: 마크업을 저장하고 다시 열었을 때 종류·위치·작성자·내용이 유지되는가.</summary>
public class MarkupRoundTripTests(PlanSetFixture fixture) : IClassFixture<PlanSetFixture>
{
    private static readonly PageRect Box = new(400, 300, 700, 420);

    public static TheoryData<string> DraftNames => new()
    {
        "TextBox", "Callout", "Line", "Arrow", "Rectangle", "Ellipse", "Cloud", "Ink", "Highlight",
    };

    private static (MarkupDraft Draft, MarkupKind Kind, PageRect Expected, double Tolerance) Make(string name)
    {
        var meta = new { Author = "Sam", Subject = "Structural", Comment = "VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM." };
        MarkupDraft draft = name switch
        {
            "TextBox" => new TextBoxDraft(Box, "한글 검토 주석 VERIFY"),
            "Callout" => new CalloutDraft(Box, "CHECK BEAM", new PagePoint(300, 500), new PagePoint(350, 450)),
            "Line" => new LineDraft(new PagePoint(400, 300), new PagePoint(700, 420)),
            "Arrow" => new LineDraft(new PagePoint(400, 300), new PagePoint(700, 420), ArrowAtEnd: true),
            "Rectangle" => new RectangleDraft(Box),
            "Ellipse" => new EllipseDraft(Box),
            "Cloud" => CloudDraft.FromRect(Box),
            "Ink" => new InkDraft([[new(400, 300), new(500, 380), new(600, 330), new(700, 420)]]),
            "Highlight" => new HighlightDraft(Box) { Style = MarkupStyle.Highlighter },
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        draft = draft with { Author = meta.Author, Subject = meta.Subject, Comment = meta.Comment };
        var kind = name switch
        {
            "TextBox" => MarkupKind.TextBox,
            "Callout" => MarkupKind.Callout,
            "Line" => MarkupKind.Line,
            "Arrow" => MarkupKind.Arrow,
            "Rectangle" => MarkupKind.Rectangle,
            "Ellipse" => MarkupKind.Ellipse,
            "Cloud" => MarkupKind.Cloud,
            "Ink" => MarkupKind.Ink,
            _ => MarkupKind.Highlight,
        };
        // 지시선은 글상자 밖(화살촉 300,500)까지 영역이 넓어진다.
        var expected = name == "Callout" ? new PageRect(300, 300, 700, 500) : Box;
        // 구름 테두리 볼록함, 선 굵기만큼 영역이 커질 수 있다.
        var tolerance = name switch { "Cloud" => 30, "Ink" => 12, "Arrow" => 15, _ => 8 };
        return (draft, kind, expected, tolerance);
    }

    [Theory]
    [MemberData(nameof(DraftNames))]
    public void Markup_survives_save_and_reopen(string name)
    {
        var (draft, kind, expected, tolerance) = Make(name);
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(Pages.FloorPlan, draft);

        using var reopened = SaveAndReopen(doc, dir);
        var markup = Assert.Single(reopened.GetMarkups(Pages.FloorPlan), m => m.Id == id);

        Assert.Equal(kind, markup.Kind);
        Assert.Equal("Sam", markup.Author);
        Assert.Equal("Structural", markup.Subject);
        if (draft is TextBoxDraft t) Assert.Equal(t.Text, markup.Comment);
        else if (draft is CalloutDraft c) Assert.Equal(c.Text, markup.Comment);
        else Assert.Equal(draft.Comment, markup.Comment);
        Assert.NotNull(markup.ModifiedDate);
        AssertBoundsNear(expected, markup.Bounds, tolerance);
    }

    public static TheoryData<int, string> RotatedCases()
    {
        var data = new TheoryData<int, string>();
        foreach (var page in new[] { Pages.Rotated90, Pages.Rotated180, Pages.Rotated270, Pages.CroppedA3 })
            foreach (var name in new[] { "TextBox", "Callout", "Rectangle", "Cloud", "Ink", "Highlight", "Arrow" })
                data.Add(page, name);
        return data;
    }

    /// <summary>기획서 13장: 회전·크롭 페이지에서도 주석 위치가 유지된다.</summary>
    [Theory]
    [MemberData(nameof(RotatedCases))]
    public void Markup_position_is_kept_on_rotated_and_cropped_pages(int pageIndex, string name)
    {
        var (draft, _, expected, tolerance) = Make(name);
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(pageIndex, draft);

        using var reopened = SaveAndReopen(doc, dir);
        var markup = Assert.Single(reopened.GetMarkups(pageIndex), m => m.Id == id);
        AssertBoundsNear(expected, markup.Bounds, tolerance);
    }

    [Fact]
    public void Markup_ids_are_unique_across_pages_and_sessions()
    {
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box));
        doc.AddMarkup(Pages.Rotated90, new RectangleDraft(Box));
        using var reopened = SaveAndReopen(doc, dir, "first.pdf");
        reopened.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box));
        using var again = SaveAndReopen(reopened, dir, "second.pdf");

        var ids = again.GetAllMarkups().Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Delete_removes_only_the_target_markup()
    {
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var keep = doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box));
        var remove = doc.AddMarkup(Pages.FloorPlan, new EllipseDraft(Box));

        Assert.True(doc.DeleteMarkup(Pages.FloorPlan, remove));
        Assert.False(doc.DeleteMarkup(Pages.FloorPlan, remove));

        using var reopened = SaveAndReopen(doc, dir);
        var ids = reopened.GetMarkups(Pages.FloorPlan).Select(m => m.Id).ToList();
        Assert.Contains(keep, ids);
        Assert.DoesNotContain(remove, ids);
    }

    [Fact]
    public void Korean_comment_text_round_trips()
    {
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(Pages.FloorPlan, new RectangleDraft(Box) { Comment = "신설 트러스와 기존 보 간격 확인", Author = "김샘" });
        using var reopened = SaveAndReopen(doc, dir);
        var m = Assert.Single(reopened.GetMarkups(Pages.FloorPlan), x => x.Id == id);
        Assert.Equal("신설 트러스와 기존 보 간격 확인", m.Comment);
        Assert.Equal("김샘", m.Author);
    }
}
