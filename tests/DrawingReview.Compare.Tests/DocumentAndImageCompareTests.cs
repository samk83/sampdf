using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Pdfium;
using DrawingReview.TestData;
using Xunit.Abstractions;
using R = DrawingReview.TestData.ComparePairs.Revision;

namespace DrawingReview.Compare.Tests;

/// <summary>그림 파일 비교, 지워진 선 층, 문서 전체(여러 페이지) 비교.</summary>
public class DocumentAndImageCompareTests(ITestOutputHelper output) : IDisposable
{
    private static readonly PdfiumEngine Engine = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("doccmp-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>가상 도면을 150dpi PNG로 저장 (스캔본 흉내).</summary>
    private string Png(string name, R? revision = null)
    {
        using var doc = Engine.Open(ComparePairs.Sheet(revision));
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, doc.RenderPagePng(0, 150 / 72.0, includeMarkups: false));
        return path;
    }

    private static byte[] Pages(params byte[][] sheets) => PdfTools.Merge(sheets);

    [Fact]
    public void Png_images_open_as_pdf_and_compare()
    {
        Assert.True(PdfiumEngine.IsImageFile("a.PNG"));
        using var oldDoc = Engine.Open(Png("old.png"));
        using var newDoc = Engine.Open(Png("new.png", new R { AddLine = true, Note = "VERIFY CLEARANCE AT COLUMN" }));
        Assert.Equal(1, newDoc.PageCount);
        Assert.Null(newDoc.SourcePath); // 그림 경로에 PDF를 덮어쓰지 않도록

        var result = DocumentComparer.Compare(oldDoc, newDoc);
        foreach (var r in result.Pages[0].Result!.Regions) output.WriteLine($"#{r.Number} {r.Category} {r.Kind} {r.Bounds}");
        // 그림에는 글자 정보가 없으므로 글자 변경도 그림 변경(Cloud)으로 나온다
        Assert.Equal(2, result.Total.Total);
        Assert.Equal(2, result.Total.Graphic);

        // 표시를 넣고 PDF로 저장할 수 있다
        RevisionMarks.Apply(newDoc, 0, result.Pages[0].Result!.Regions, new RevisionMarkOptions());
        var outPath = Path.Combine(_dir, "marked.pdf");
        newDoc.SaveTo(outPath);
        using var reopened = Engine.Open(outPath);
        Assert.NotEmpty(reopened.GetMarkups(0));
    }

    [Fact]
    public void Identical_images_have_no_changes()
    {
        using var a = Engine.Open(Png("a.png"));
        using var b = Engine.Open(Png("b.png"));
        Assert.Equal(0, DocumentComparer.Compare(a, b).Total.Total);
    }

    [Fact]
    public void Removed_layer_shows_only_the_deleted_beam()
    {
        using var oldDoc = Engine.Open(ComparePairs.Sheet());
        using var newDoc = Engine.Open(ComparePairs.Sheet(new R { RemoveBeam = true }));
        var layer = PageComparer.RenderRemovedLayer(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0), AlignmentTransform.Identity, 2);
        int red = 0, redInBeam = 0;
        var beam = new Engine.Geometry.PageRect(396, 360, 604, 394); // 보 선 + "B2" 글자
        for (int y = 0; y < layer.Height; y++)
            for (int x = 0; x < layer.Width; x++)
            {
                var i = y * layer.Stride + x * 3;
                if (layer.Pixels[i] < 150 || layer.Pixels[i + 1] > 100) continue; // 빨강만
                red++;
                if (x / 2.0 >= beam.X0 && x / 2.0 <= beam.X1 && y / 2.0 >= beam.Y0 && y / 2.0 <= beam.Y1) redInBeam++;
            }
        output.WriteLine($"red {red}, in beam {redInBeam}");
        Assert.True(red > 50);
        Assert.Equal(red, redInBeam);
    }

    [Fact]
    public void Extra_and_missing_pages_are_reported()
    {
        var sheet = ComparePairs.Sheet();
        var changed = ComparePairs.Sheet(new R { AddLine = true });
        using var oldDoc = Engine.Open(Pages(sheet, sheet));
        using var newDoc = Engine.Open(Pages(sheet, changed, sheet));
        var result = DocumentComparer.Compare(oldDoc, newDoc);
        Assert.Equal(3, result.Pages.Count);
        Assert.Equal(0, result.Pages[0].Summary.Total);
        Assert.Equal(1, result.Pages[1].Summary.Total);
        Assert.True(result.Pages[2].IsAddedPage);
        Assert.Contains(result.Warnings, w => w.Contains("1 more page"));

        using var fewer = Engine.Open(Pages(sheet));
        var r2 = DocumentComparer.Compare(oldDoc, fewer);
        Assert.True(r2.Pages[1].IsRemovedPage);
        Assert.Contains(r2.Warnings, w => w.Contains("fewer page"));
    }
}
