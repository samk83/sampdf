using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using static DrawingReview.Engine.Tests.TestHelpers;
using Pages = DrawingReview.TestData.VirtualDrawings.PlanSetPages;

namespace DrawingReview.Engine.Tests;

/// <summary>저장된 주석을 편집 가능한 Draft로 다시 읽고, 같은 ID로 바꿔 쓰는 기능.</summary>
public class MarkupEditTests(PlanSetFixture fixture) : IClassFixture<PlanSetFixture>
{
    private static readonly MarkupStyle Blue = MarkupStyle.Default with
    {
        StrokeColor = RgbColor.Blue, TextColor = RgbColor.Blue, LineWidth = 2.5, Opacity = 0.8, FontSize = 16,
    };

    public static TheoryData<int, string> Cases()
    {
        var data = new TheoryData<int, string>();
        foreach (var page in new[] { Pages.FloorPlan, Pages.Rotated90, Pages.CroppedA3, Pages.Rotated180, Pages.Rotated270 })
            foreach (var name in new[] { "TextBox", "Callout", "Line", "Arrow", "Rectangle", "Ellipse", "Cloud", "Ink", "Highlight" })
                data.Add(page, name);
        return data;
    }

    private static MarkupDraft Make(string name) => (name switch
    {
        "TextBox" => (MarkupDraft)new TextBoxDraft(new PageRect(400, 300, 700, 380), "한글 VERIFY") { Style = Blue },
        "Callout" => new CalloutDraft(new PageRect(400, 300, 700, 380), "CHECK", new PagePoint(300, 480), new PagePoint(350, 420)) { Style = Blue },
        "Line" => new LineDraft(new PagePoint(400, 300), new PagePoint(700, 420)) { Style = Blue },
        "Arrow" => new LineDraft(new PagePoint(400, 300), new PagePoint(700, 420), true) { Style = Blue },
        "Rectangle" => new RectangleDraft(new PageRect(400, 300, 700, 420)) { Style = Blue },
        "Ellipse" => new EllipseDraft(new PageRect(400, 300, 700, 420)) { Style = Blue },
        "Cloud" => new CloudDraft([new(400, 300), new(700, 310), new(650, 420), new(420, 400)], 1) { Style = Blue },
        "Ink" => new InkDraft([[new(400, 300), new(500, 380), new(600, 330)], [new(410, 410), new(700, 420)]]) { Style = Blue },
        "Highlight" => new HighlightDraft(new PageRect(400, 300, 700, 420)) { Style = MarkupStyle.Highlighter },
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    }) with { Author = "Sam", Subject = "Structural", Comment = name is "TextBox" or "Callout" ? null : "note" };

    private static void AssertNear(PagePoint e, PagePoint a, double tol = 1.0)
    {
        Assert.True(Math.Abs(e.X - a.X) <= tol && Math.Abs(e.Y - a.Y) <= tol, $"expected {e}, got {a}");
    }

    private static void AssertNear(PageRect e, PageRect a, double tol = 1.0)
    {
        AssertNear(new PagePoint(e.X0, e.Y0), new PagePoint(a.X0, a.Y0), tol);
        AssertNear(new PagePoint(e.X1, e.Y1), new PagePoint(a.X1, a.Y1), tol);
    }

    private static void AssertSameGeometry(MarkupDraft expected, MarkupDraft actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        switch (expected, actual)
        {
            case (TextBoxDraft e, TextBoxDraft a): AssertNear(e.Rect, a.Rect); Assert.Equal(e.Text, a.Text); break;
            case (CalloutDraft e, CalloutDraft a):
                AssertNear(e.TextRect, a.TextRect); Assert.Equal(e.Text, a.Text);
                AssertNear(e.ArrowTip, a.ArrowTip); AssertNear(e.Knee!.Value, a.Knee!.Value); break;
            case (LineDraft e, LineDraft a): AssertNear(e.Start, a.Start); AssertNear(e.End, a.End); Assert.Equal(e.ArrowAtEnd, a.ArrowAtEnd); break;
            case (RectangleDraft e, RectangleDraft a): AssertNear(e.Rect, a.Rect); break;
            case (EllipseDraft e, EllipseDraft a): AssertNear(e.Rect, a.Rect); break;
            case (HighlightDraft e, HighlightDraft a): AssertNear(e.Rect, a.Rect); break;
            case (CloudDraft e, CloudDraft a):
                Assert.Equal(e.Vertices.Count, a.Vertices.Count);
                for (int i = 0; i < e.Vertices.Count; i++) AssertNear(e.Vertices[i], a.Vertices[i]);
                Assert.Equal(e.Intensity, a.Intensity, 1); break;
            case (InkDraft e, InkDraft a):
                Assert.Equal(e.Strokes.Count, a.Strokes.Count);
                for (int s = 0; s < e.Strokes.Count; s++)
                {
                    Assert.Equal(e.Strokes[s].Count, a.Strokes[s].Count);
                    for (int i = 0; i < e.Strokes[s].Count; i++) AssertNear(e.Strokes[s][i], a.Strokes[s][i]);
                }
                break;
            default: Assert.Fail($"unexpected pair {expected.GetType().Name}"); break;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Saved_markup_reads_back_as_equivalent_draft(int page, string name)
    {
        var draft = Make(name);
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(page, draft);
        using var reopened = SaveAndReopen(doc, dir);
        var info = Assert.Single(reopened.GetMarkups(page), m => m.Id == id);

        Assert.True(info.IsEditable);
        var read = info.Draft!;
        AssertSameGeometry(draft, read);
        Assert.Equal("Sam", read.Author);
        Assert.Equal("Structural", read.Subject);
        Assert.Equal(draft.Comment, read.Comment);

        if (name == "Highlight")
        {
            Assert.Equal(RgbColor.Yellow, read.Style.StrokeColor);
            Assert.Equal(0.4, read.Style.Opacity, 2);
        }
        else
        {
            Assert.Equal(RgbColor.Blue, read.Style.StrokeColor);
            Assert.Equal(2.5, read.Style.LineWidth, 2);
            Assert.Equal(0.8, read.Style.Opacity, 2);
        }
        if (name is "TextBox" or "Callout")
        {
            Assert.Equal(16, read.Style.FontSize, 1);
            Assert.Equal(RgbColor.Blue, read.Style.TextColor);
        }
    }

    [Fact]
    public void Replace_keeps_id_status_lock_and_creation_date()
    {
        using var dir = new TempDir();
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(Pages.FloorPlan, Make("Rectangle"));
        Assert.True(doc.SetMarkupStatus(Pages.FloorPlan, id, ReviewStatus.InProgress));
        Assert.True(doc.SetMarkupLocked(Pages.FloorPlan, id, true));
        var before = Assert.Single(doc.GetMarkups(Pages.FloorPlan), m => m.Id == id);

        var moved = new RectangleDraft(new PageRect(800, 600, 900, 700)) { Style = Blue, Author = "Sam" };
        Assert.True(doc.ReplaceMarkup(Pages.FloorPlan, id, moved));
        Assert.False(doc.ReplaceMarkup(Pages.FloorPlan, "missing", moved));

        using var reopened = SaveAndReopen(doc, dir);
        var after = Assert.Single(reopened.GetMarkups(Pages.FloorPlan), m => m.Id == id);
        Assert.Equal(ReviewStatus.InProgress, after.Status);
        Assert.True(after.IsLocked);
        Assert.Equal(before.CreatedDate, after.CreatedDate);
        AssertNear(new PageRect(800, 600, 900, 700), ((RectangleDraft)after.Draft!).Rect);
    }

    [Fact]
    public void Status_and_lock_can_be_cleared()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(Pages.FloorPlan, Make("Ellipse"));
        doc.SetMarkupStatus(Pages.FloorPlan, id, ReviewStatus.Resolved);
        doc.SetMarkupLocked(Pages.FloorPlan, id, true);
        doc.SetMarkupStatus(Pages.FloorPlan, id, null);
        doc.SetMarkupLocked(Pages.FloorPlan, id, false);
        var m = Assert.Single(doc.GetMarkups(Pages.FloorPlan), x => x.Id == id);
        Assert.Null(m.Status);
        Assert.False(m.IsLocked);
    }

    [Fact]
    public void Add_with_explicit_id_restores_deleted_markup()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var id = doc.AddMarkup(Pages.FloorPlan, Make("Cloud"));
        var draft = Assert.Single(doc.GetMarkups(Pages.FloorPlan), m => m.Id == id).Draft!;
        doc.DeleteMarkup(Pages.FloorPlan, id);
        Assert.Equal(id, doc.AddMarkup(Pages.FloorPlan, draft, id));
        Assert.Single(doc.GetMarkups(Pages.FloorPlan), m => m.Id == id && m.Kind == MarkupKind.Cloud);
    }

    [Fact]
    public void Preexisting_foreign_note_is_read_only()
    {
        using var doc = PdfEngine.Open(fixture.PlanSet);
        var note = Assert.Single(doc.GetMarkups(Pages.FloorPlan), m => m.PdfSubtype == "Text");
        Assert.False(note.IsEditable);
    }
}
