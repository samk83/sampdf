using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.MuPdf;
using DrawingReview.TestData;
using Xunit.Abstractions;
using R = DrawingReview.TestData.ComparePairs.Revision;

namespace DrawingReview.Compare.Tests;

/// <summary>글자 변경 → 하이라이트, 그림 변경 → Cloud, 모든 변경에 △ 리비전 번호.</summary>
public class RevisionMarksTests(ITestOutputHelper output)
{
    private static readonly MuPdfEngine Engine = new();

    private CompareResult Run(R revision)
    {
        using var oldDoc = Engine.Open(ComparePairs.Sheet());
        using var newDoc = Engine.Open(ComparePairs.Sheet(revision));
        var result = PageComparer.Compare(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0));
        foreach (var r in result.Regions) output.WriteLine($"  #{r.Number} {r.Category} {r.Kind} {r.Bounds} hl={r.TextHighlights.Count} {r.Description}");
        return result;
    }

    [Fact]
    public void Changed_note_is_a_text_change_with_highlight()
    {
        var r = Assert.Single(Run(new R { Note = "VERIFY CLEARANCE AT COLUMN" }).Regions);
        Assert.Equal(ChangeCategory.Text, r.Category);
        Assert.NotEmpty(r.TextHighlights);
        Assert.Contains("\"BEAM\" → \"COLUMN\"", r.Description);
    }

    [Fact]
    public void Changed_dimension_is_a_text_change()
    {
        var r = Assert.Single(Run(new R { Dimension = "3500" }).Regions);
        Assert.Equal(ChangeCategory.Text, r.Category);
        Assert.Contains("\"3000\" → \"3500\"", r.Description);
    }

    [Theory]
    [InlineData(true, false)]   // 선 추가
    [InlineData(false, true)]   // 보 삭제
    public void Line_changes_are_graphic_changes(bool addLine, bool removeBeam)
    {
        var r = Assert.Single(Run(new R { AddLine = addLine, RemoveBeam = removeBeam }).Regions);
        Assert.Equal(ChangeCategory.Graphic, r.Category);
        Assert.Empty(r.TextHighlights);
    }

    [Fact]
    public void Marks_are_written_as_standard_annotations_with_revision_delta()
    {
        var revision = new R { Note = "VERIFY CLEARANCE AT COLUMN", AddLine = true };
        var result = Run(revision);
        Assert.Equal(2, result.Regions.Count);
        using var doc = Engine.Open(ComparePairs.Sheet(revision));
        var ids = RevisionMarks.Apply(doc, 0, result.Regions, new RevisionMarkOptions { RevisionLabel = "2" });

        var path = Path.Combine(Directory.CreateTempSubdirectory("revmarks-").FullName, "marked.pdf");
        doc.SaveTo(path);
        using var reopened = Engine.Open(path);
        var marks = reopened.GetMarkups(0);
        Assert.Equal(ids.Values.Sum(v => v.Count), marks.Count);

        var text = result.Regions.Single(r => r.Category == ChangeCategory.Text);
        var graphic = result.Regions.Single(r => r.Category == ChangeCategory.Graphic);
        Assert.Equal(text.TextHighlights.Count, marks.Count(m => m.Draft is HighlightDraft));
        // 그림 변경의 Cloud 1개 (구름 강도 > 0)
        var cloud = Assert.Single(marks, m => m.Draft is CloudDraft { Intensity: > 0 });
        Assert.True(cloud.Draft is CloudDraft c && c.Vertices.Min(v => v.X) < graphic.Bounds.X0 && c.Vertices.Max(v => v.X) > graphic.Bounds.X1);
        // 변경마다 △ 삼각형 + 번호 "2"
        Assert.Equal(2, marks.Count(m => m.Draft is CloudDraft { Intensity: 0, Vertices.Count: 3 }));
        Assert.Equal(2, marks.Count(m => m.Draft is TextBoxDraft { Text: "2" }));
        // 글상자는 내용이 번호 자체라 메모가 따로 없다. 나머지는 변경 설명을 메모로 갖는다.
        Assert.All(marks.Where(m => m.Draft is not TextBoxDraft), m => Assert.StartsWith("Change ", m.Draft.Comment));

        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path)!, "marked.png"), reopened.RenderPagePng(0, 1.5));
        output.WriteLine("rendered: " + Path.Combine(Path.GetDirectoryName(path)!, "marked.png"));
    }

    [Fact]
    public void Delta_stays_inside_the_page()
    {
        var o = new RevisionMarkOptions();
        var marks = RevisionMarks.Delta(new Engine.Geometry.PageRect(1180, 0, 1191, 10), o, 1191, 842);
        var tri = (CloudDraft)marks[0];
        Assert.All(tri.Vertices, v => { Assert.InRange(v.X, 0, 1191); Assert.InRange(v.Y, 0, 842); });
    }
}

public class RevisionSamplePairTests(ITestOutputHelper output)
{
    [Fact] // samples/compare 쌍과 같은 개정: 글자 2곳(하이라이트), 그림 2곳(Cloud)
    public void Sample_pair_has_two_text_and_two_graphic_changes()
    {
        var engine = new MuPdfEngine();
        using var oldDoc = engine.Open(ComparePairs.Sheet());
        using var newDoc = engine.Open(ComparePairs.Sheet(new R { Note = "VERIFY CLEARANCE AT COLUMN", Dimension = "3500", AddLine = true, RemoveBeam = true }));
        var result = PageComparer.Compare(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0));
        foreach (var r in result.Regions) output.WriteLine($"#{r.Number} {r.Category} {r.Kind} {r.Description}");
        Assert.Equal(2, result.Regions.Count(r => r.Category == ChangeCategory.Text));
        Assert.Equal(2, result.Regions.Count(r => r.Category == ChangeCategory.Graphic));
    }
}
