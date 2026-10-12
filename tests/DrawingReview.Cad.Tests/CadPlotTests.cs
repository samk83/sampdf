using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.MuPdf;
using DrawingReview.TestData;
using Rev = DrawingReview.TestData.CadSamples.Rev;

namespace DrawingReview.Cad.Tests;

/// <summary>CAD 도면 → PDF 표시 (비교 결과를 기존 비교 화면에서 보기 위한 선 렌더러).</summary>
public class CadPlotTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cadplot-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private CadDrawing Load(Rev r, string name = "a.dxf") => CadLoader.Load(CadSamples.Save(CadSamples.Build(r), Path.Combine(_dir, name)));

    [Fact]
    public void Mapping_round_trips_and_flips_y()
    {
        var map = CadPageMapping.Fit(new Box(0, 0, 20000, 10000), longEdgePt: 1000, marginPt: 50);
        Assert.Equal(1000, map.PageWidth, 6);
        Assert.Equal(550, map.PageHeight, 6);
        Assert.Equal(new PagePoint(50, 50), map.ToPage(new Pt(0, 10000)));   // 모델 왼쪽 위 = 페이지 왼쪽 위
        Assert.Equal(new PagePoint(950, 500), map.ToPage(new Pt(20000, 0)));
        var box = new Box(1000, 2000, 3000, 5000);
        var back = map.ToModel(map.ToPage(box));
        Assert.Equal(box.MinX, back.MinX, 6);
        Assert.Equal(box.MaxY, back.MaxY, 6);
    }

    [Fact]
    public void Rotated_block_rotates_arc_angles_and_text()
    {
        var d = Load(new Rev());
        var arc = d.Primitives.Single(p => p.Type == PrimitiveType.Arc && p.Points[0].DistanceTo(new Pt(21000, 9000)) < 1e-6);
        Assert.Equal(90, arc.A1, 3);
        Assert.Equal(180, arc.A2, 3);
        var text = d.Primitives.Single(p => p.Type == PrimitiveType.Text && p.Text == "D1");
        Assert.Equal(90, text.A1, 3);
        Assert.Equal(20900, text.Points[0].X, 3); // (100,100)을 90° 돌리면 (-100,100)
    }

    [Fact]
    public void Plotted_pdf_shows_lines_text_and_dimensions()
    {
        var d = Load(new Rev());
        var map = CadPageMapping.Fit(CadPlotter.Extents(d));
        var page = CadPlotter.Plot(d, map);
        var engine = new MuPdfEngine();
        var path = Path.Combine(_dir, "plot.pdf");
        engine.WriteVectorPdf(path, [page]);

        using var pdf = engine.Open(path);
        var words = pdf.GetWords(0).Select(w => w.Text).ToList();
        Assert.Contains("S-101", words);
        Assert.Contains("VERIFY", words);
        Assert.Contains("6000", words);  // 치수 문자
        Assert.Contains("D1", words);

        // 그리드 선 x=0 (모델) 위치가 어둡다
        var img = pdf.RenderPage(0, 2);
        var at = map.ToPage(new Pt(0, 3000));
        Assert.True(DarkNear(img, at), "grid line should be drawn");
        Assert.False(DarkNear(img, map.ToPage(new Pt(3000, 3000))), "empty bay should be blank");
    }

    private static bool DarkNear(RenderedImage img, PagePoint p)
    {
        int cx = (int)(p.X * img.Zoom), cy = (int)(p.Y * img.Zoom);
        for (int y = cy - 2; y <= cy + 2; y++)
            for (int x = cx - 2; x <= cx + 2; x++)
            {
                var i = y * img.Stride + x * 3;
                if (img.Pixels[i] < 160) return true;
            }
        return false;
    }

    [Theory]
    [InlineData("PLAIN", "PLAIN")]
    [InlineData("LINE 1\\PLINE 2", "LINE 1\nLINE 2")]
    [InlineData("{\\fArial|b1;BOLD} TEXT", "BOLD TEXT")]
    [InlineData("\\LUNDER\\l NOTE", "UNDER NOTE")]
    public void Mtext_formatting_is_removed(string raw, string plain) => Assert.Equal(plain, CadPlotter.PlainText(raw));
}
