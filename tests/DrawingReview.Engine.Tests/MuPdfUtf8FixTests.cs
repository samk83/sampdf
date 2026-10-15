using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.MuPdf;

namespace DrawingReview.Engine.Tests;

/// <summary>
/// Windows 한글 깨짐 보정(MuPdfUtf8Fix)이 적용되고, 한글 읽기가 실제로 보정 콜백을 거치는지 확인한다.
/// Linux는 원래 UTF-8이라 보정 없이도 통과하므로, "콜백을 거치는지"로 Windows에서도 고쳐질 길임을 확인한다.
/// MuPDF.NET 버전을 올려 이 테스트가 실패하면 보정을 다시 살펴본다.
/// </summary>
public class MuPdfUtf8FixTests
{
    [Fact]
    public void Fix_is_applied_and_korean_reads_go_through_it()
    {
        var engine = new MuPdfEngine();
        Assert.True(MuPdfUtf8Fix.Applied, MuPdfUtf8Fix.Error);

        using var doc = engine.Open(TestData.VirtualDrawings.CreatePlanSet());
        var id = doc.AddMarkup(0, new RectangleDraft(new PageRect(100, 100, 300, 200)) { Comment = "기존 보 간격 확인", Author = "김샘" });
        var before = MuPdfUtf8Fix.Calls;
        var m = Assert.Single(doc.GetMarkups(0), x => x.Id == id);
        Assert.Equal("기존 보 간격 확인", m.Comment);
        Assert.Equal("김샘", m.Author);
        Assert.True(MuPdfUtf8Fix.Calls > before, "annotation strings should be returned through the UTF-8 callback");

        var outline = doc.GetOutline();
        Assert.Contains(outline, o => o.Title == "General Notes / 일반 사항");
    }
}

/// <summary>책갈피 제목을 PDF 객체에서 직접 읽기 (Windows 한글 깨짐 우회).</summary>
public class MuPdfOutlineTitlesTests
{
    [Theory]
    [InlineData("<< /Title (Architectural) >>", "Architectural")]
    [InlineData("<< /Title <FEFF0041C77CBC18> >>", "A일반")]
    [InlineData("<< /Title (A \\(B\\) \\\\ C) >>", "A (B) \\ C")]
    [InlineData("<< /Title (\\376\\377\\000A\\307\\174) >>", "A일")]          // 8진수로 쓴 UTF-16BE
    [InlineData("<< /Title (\\357\\273\\277\\355\\225\\234) >>", "한")]        // UTF-8 (BOM 포함)
    [InlineData("<< /Count 2 >>", "")]
    public void Title_strings_are_decoded(string source, string expected) =>
        Assert.Equal(expected, MuPdfOutlineTitles.Title(source));

    [Fact]
    public void Titles_follow_toc_order_and_read_korean()
    {
        using var doc = new MuPDF.NET.Document(stream: TestData.VirtualDrawings.CreatePlanSet());
        var titles = MuPdfOutlineTitles.Read(doc);
        var toc = doc.GetToc(simple: true);
        Assert.NotNull(titles);
        Assert.Equal(toc.Count, titles!.Count);
        Assert.Equal(toc.Select(t => t.Item2), titles); // Linux에서는 GetToc도 맞으므로 같아야 한다
        Assert.Contains("General Notes / 일반 사항", titles);
    }
}
