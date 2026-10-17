using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Pdfium;
using DrawingReview.TestData;
using Xunit.Abstractions;
using R = DrawingReview.TestData.ComparePairs.Revision;

namespace DrawingReview.Compare.Tests;

/// <summary>Drawing Difference Checker 기획서 12장 검수 조건.</summary>
public class CompareAcceptanceTests(ITestOutputHelper output)
{
    private static readonly PdfiumEngine Engine = new();

    private CompareResult Run(byte[] oldPdf, byte[] newPdf, CompareSettings? settings = null, AlignmentResult? alignment = null)
    {
        using var oldDoc = Engine.Open(oldPdf);
        using var newDoc = Engine.Open(newPdf);
        var result = PageComparer.Compare(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0), settings, alignment);
        output.WriteLine($"alignment: {result.Alignment.Method} q={result.Alignment.Quality:0.000} scale={result.Alignment.Transform.Scale:0.0000} " +
                         $"rot={result.Alignment.Transform.RotationDegrees:0.000} ({result.Alignment.Note}); {result.Duration.TotalMilliseconds:0} ms");
        foreach (var r in result.Regions) output.WriteLine($"  #{r.Number} {r.Kind} {r.Confidence} {r.Bounds} +{r.AddedPixels} -{r.RemovedPixels}");
        foreach (var w in result.Warnings) output.WriteLine("  warning: " + w);
        return result;
    }

    private static bool Overlaps(PageRect a, PageRect b, double slack = 3) =>
        a.X0 - slack <= b.X1 && b.X0 - slack <= a.X1 && a.Y0 - slack <= b.Y1 && b.Y0 - slack <= a.Y1;

    [Fact] // 1
    public void Identical_files_produce_zero_changes()
    {
        var pdf = ComparePairs.Sheet();
        var result = Run(pdf, pdf);
        Assert.Empty(result.Regions);
        Assert.Equal(AlignmentMethod.Identity, result.Alignment.Method);
        Assert.Empty(result.Warnings);
    }

    [Fact] // 2
    public void Added_line_is_detected()
    {
        var result = Run(ComparePairs.Sheet(), ComparePairs.Sheet(new R { AddLine = true }));
        var region = Assert.Single(result.Regions);
        Assert.Equal(ChangeKind.Added, region.Kind);
        Assert.True(Overlaps(region.Bounds, ComparePairs.AddedLineArea), $"{region.Bounds}");
        Assert.Equal(ChangeConfidence.High, region.Confidence);
    }

    [Fact] // 3
    public void Deleted_line_is_detected()
    {
        var result = Run(ComparePairs.Sheet(), ComparePairs.Sheet(new R { RemoveBeam = true }));
        var region = Assert.Single(result.Regions);
        Assert.Equal(ChangeKind.Removed, region.Kind);
        Assert.True(Overlaps(region.Bounds, ComparePairs.BeamB2Area), $"{region.Bounds}");
    }

    [Fact] // 4
    public void Moved_beam_produces_visible_change_region()
    {
        var result = Run(ComparePairs.Sheet(), ComparePairs.Sheet(new R { MoveBeamDx = 30 }));
        Assert.NotEmpty(result.Regions);
        var moved = new PageRect(430, 380, 630, 390);
        Assert.Contains(result.Regions, r => Overlaps(r.Bounds, ComparePairs.BeamB2Area));
        Assert.Contains(result.Regions, r => Overlaps(r.Bounds, moved));
        Assert.Contains(result.Regions, r => r.AddedPixels > 0);
        Assert.Contains(result.Regions, r => r.RemovedPixels > 0);
    }

    [Fact] // 5
    public void Changed_dimension_is_detected()
    {
        var result = Run(ComparePairs.Sheet(), ComparePairs.Sheet(new R { Dimension = "3500" }));
        var region = Assert.Single(result.Regions);
        Assert.Equal(ChangeKind.Modified, region.Kind);
        Assert.True(Overlaps(region.Bounds, ComparePairs.DimensionTextArea), $"{region.Bounds}");
        Assert.Equal(ChangeConfidence.High, region.Confidence); // 글자 비교로 확실히 잡혀야 한다
    }

    [Fact] // 6
    public void Changed_text_note_is_detected()
    {
        var result = Run(ComparePairs.Sheet(), ComparePairs.Sheet(new R { Note = "VERIFY CLEARANCE AT COLUMN" }));
        var region = Assert.Single(result.Regions);
        Assert.True(Overlaps(region.Bounds, ComparePairs.NoteArea), $"{region.Bounds}");
    }

    [Theory] // 7
    [InlineData(0.2, 0.0)]
    [InlineData(0.35, 0.25)]
    public void Slight_rasterization_differences_are_filtered(double sx, double sy)
    {
        var result = Run(ComparePairs.Sheet(), ComparePairs.Sheet(new R { ShiftX = sx, ShiftY = sy }));
        Assert.Empty(result.Regions);
    }

    [Theory] // 8
    [InlineData(0.97, 0, 20, 15)]
    [InlineData(0.95, 1, 30, 10)]
    [InlineData(1.0, 0, -12, 8)]
    public void Scale_rotation_and_offset_are_aligned(double scale, int rotate, double ox, double oy)
    {
        var oldPdf = ComparePairs.Sheet();
        var newPdf = ComparePairs.Transformed(oldPdf, scale, rotate, ox, oy, extraLine: new PageRect(300, 750, 500, 750));
        var result = Run(oldPdf, newPdf);
        Assert.True(result.Alignment.IsReliable, $"alignment quality {result.Alignment.Quality}");
        // 회전하면 회전한 페이지 전체가 틀 안에 들어가도록 더 줄어든다 (가로·세로 중 더 빠듯한 쪽 기준)
        var rad = rotate * Math.PI / 180;
        double w = ComparePairs.Width, h = ComparePairs.Height, c = Math.Cos(rad), sn = Math.Sin(rad);
        var expectedScale = scale * Math.Min(w / (w * c + h * sn), h / (w * sn + h * c));
        Assert.Equal(expectedScale, result.Alignment.Transform.Scale, 2);
        Assert.Equal(-rotate, result.Alignment.Transform.RotationDegrees, 1);
        var region = Assert.Single(result.Regions, r => r.Confidence == ChangeConfidence.High);
        Assert.True(Overlaps(region.Bounds, new PageRect(300, 745, 500, 755)), $"{region.Bounds}");
    }

    [Fact] // 10
    public void Multiple_changes_are_separated()
    {
        var result = Run(ComparePairs.Sheet(), ComparePairs.Sheet(new R { AddFarDetails = true }));
        Assert.Equal(3, result.Regions.Count);
        foreach (var area in ComparePairs.FarDetailAreas)
            Assert.Single(result.Regions, r => Overlaps(r.Bounds, area));
        Assert.Equal([1, 2, 3], result.Regions.Select(r => r.Number));
    }

    [Fact] // 8 (제목란 무시)
    public void Ignore_region_hides_title_block_changes_only()
    {
        var oldPdf = ComparePairs.Sheet();
        var newPdf = ComparePairs.Sheet(new R { TitleDate = "2026-10-05", AddLine = true });
        Assert.Contains(Run(oldPdf, newPdf).Regions, r => Overlaps(r.Bounds, ComparePairs.TitleBlock));
        var result = Run(oldPdf, newPdf, new CompareSettings { IgnoreRegions = [ComparePairs.TitleBlock] });
        var region = Assert.Single(result.Regions);
        Assert.True(Overlaps(region.Bounds, ComparePairs.AddedLineArea));
    }

    [Fact] // 14
    public void Original_files_remain_unchanged()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cmp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "old.pdf");
            var b = Path.Combine(dir, "new.pdf");
            File.WriteAllBytes(a, ComparePairs.Sheet());
            File.WriteAllBytes(b, ComparePairs.Sheet(new R { AddLine = true }));
            var ha = File.ReadAllBytes(a);
            var hb = File.ReadAllBytes(b);
            using (var oldDoc = Engine.Open(a))
            using (var newDoc = Engine.Open(b))
                PageComparer.Compare(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0));
            Assert.Equal(ha, File.ReadAllBytes(a));
            Assert.Equal(hb, File.ReadAllBytes(b));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Manual_two_point_alignment_recovers_transform()
    {
        var oldPdf = ComparePairs.Sheet();
        var newPdf = ComparePairs.Transformed(oldPdf, 0.9, 0, 40, 30);
        // 그리드 A1, I6 버블 중심 (이전 도면) → 개정 도면에서는 0.9배 + (40,30)
        PagePoint O(double x, double y) => new(x, y);
        PagePoint N(double x, double y) => new(x * 0.9 + 40, y * 0.9 + 30);
        var t = AlignmentTransform.FromTwoPoints(O(100, 62), O(1060, 650), N(100, 62), N(1060, 650));
        Assert.Equal(0.9, t.Scale, 4);
        Assert.Equal(0, t.RotationDegrees, 3);
        using var oldDoc = Engine.Open(oldPdf);
        using var newDoc = Engine.Open(newPdf);
        var manual = Aligner.Manual(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0), t);
        Assert.True(manual.IsReliable, $"quality {manual.Quality}");
        var result = PageComparer.Compare(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0), alignment: manual);
        Assert.DoesNotContain(result.Regions, r => r.Confidence == ChangeConfidence.High);
    }

    [Fact]
    public void Transform_inverse_and_rect_mapping()
    {
        var t = AlignmentTransform.FromTwoPoints(new(0, 0), new(100, 0), new(10, 20), new(10, 220));
        Assert.Equal(2, t.Scale, 6);
        Assert.Equal(90, t.RotationDegrees, 6);
        var p = t.Inverse().Apply(t.Apply(new PagePoint(33, 44)));
        Assert.Equal(33, p.X, 6);
        Assert.Equal(44, p.Y, 6);
    }

    [Fact]
    public void Overlay_image_marks_old_red_and_new_blue()
    {
        using var oldDoc = Engine.Open(ComparePairs.Sheet());
        using var newDoc = Engine.Open(ComparePairs.Sheet(new R { AddLine = true, RemoveBeam = true }));
        var img = PageComparer.RenderOverlay(PageSource.From(oldDoc, 0), PageSource.From(newDoc, 0), AlignmentTransform.Identity, 1);
        (byte, byte, byte) Px(double x, double y) { var i = (int)y * img.Stride + (int)x * 3; return (img.Pixels[i], img.Pixels[i + 1], img.Pixels[i + 2]); }
        Assert.Equal((byte)30, Px(900, 185).Item1);   // 추가된 선 = 파랑
        Assert.Equal((byte)220, Px(500, 385).Item1);  // 삭제된 보 = 빨강
        Assert.Equal((byte)60, Px(580, 300).Item1);   // 공통 벽 = 회색
    }

    [Fact] // 9 (대형 도면 성능 참고치)
    public void Large_sheet_compare_time_is_reported()
    {
        var oldPdf = VirtualDrawings.CreateDenseVectorSheet(80);
        var result = Run(oldPdf, oldPdf, new CompareSettings { Dpi = 100 });
        Assert.Empty(result.Regions);
        Assert.True(result.Duration < TimeSpan.FromSeconds(60));
    }
}
