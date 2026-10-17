using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdfium;
using DrawingReview.Engine.Vector;

namespace DrawingReview.Engine.Tests;

/// <summary>벡터 페이지 → PDF (CAD 도면 표시용).</summary>
public class VectorPdfTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vec-").FullName;
    private readonly PdfiumEngine _engine = new();

    public void Dispose() => Directory.Delete(_dir, true);

    private static VectorPage Sample() => new(600, 400,
    [
        new VectorPolyline([new PagePoint(50, 100), new PagePoint(550, 100)], false, RgbColor.Black, 2),
        new VectorPolyline([new PagePoint(100, 150), new PagePoint(200, 150), new PagePoint(200, 250), new PagePoint(100, 250)], true, RgbColor.Black, 1),
        new VectorText(new PagePoint(300, 200), "GRID A (1)", 20, 0, RgbColor.Black),
        new VectorText(new PagePoint(300, 300), "기둥 C1", 20, 0, RgbColor.Black),
        new VectorText(new PagePoint(560, 350), "UP", 12, 90, RgbColor.Black),
    ]);

    private static bool Dark(RenderedImage img, double x, double y)
    {
        int px = (int)(x * img.Zoom), py = (int)(y * img.Zoom);
        var i = py * img.Stride + px * 3;
        return img.Pixels[i] < 128 && img.Pixels[i + 1] < 128 && img.Pixels[i + 2] < 128;
    }

    [Fact]
    public void Lines_and_text_are_written_in_page_coordinates()
    {
        var path = Path.Combine(_dir, "v.pdf");
        _engine.WriteVectorPdf(path, [Sample(), Sample() with { Width = 300 }]);
        using var doc = _engine.Open(path);
        Assert.Equal(2, doc.PageCount);
        Assert.Equal(600, doc.GetPageInfo(0).Width, 1);
        Assert.Equal(400, doc.GetPageInfo(0).Height, 1);

        var img = doc.RenderPage(0, 2);
        Assert.True(Dark(img, 300, 100));   // 가로선 (위에서 100pt)
        Assert.False(Dark(img, 300, 120));
        Assert.True(Dark(img, 100, 200));   // 닫힌 사각형의 왼쪽 변
        Assert.False(Dark(img, 150, 200));  // 안쪽은 비어 있음

        var words = doc.GetWords(0).Select(w => w.Text).ToList();
        Assert.Contains("GRID", words);
        Assert.Contains("(1)", words);
        Assert.Contains("기둥", words);
        var grid = doc.GetWords(0).First(w => w.Text == "GRID");
        Assert.InRange(grid.Bounds.Y1, 195, 206); // 기준선 y=200 근처
    }

    [Fact]
    public void Existing_file_is_replaced_safely()
    {
        var path = Path.Combine(_dir, "v.pdf");
        _engine.WriteVectorPdf(path, [Sample()]);
        _engine.WriteVectorPdf(path, [Sample(), Sample()]);
        using var doc = _engine.Open(path);
        Assert.Equal(2, doc.PageCount);
    }
}
