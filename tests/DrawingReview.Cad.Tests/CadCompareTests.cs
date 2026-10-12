using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using DrawingReview.TestData;
using Xunit.Abstractions;
using Rev = DrawingReview.TestData.CadSamples.Rev;

namespace DrawingReview.Cad.Tests;

/// <summary>DWG/DXF 비교 0단계 검증: 가상 CAD 도면으로 기획서 12장 조건을 시험한다.</summary>
public class CadCompareTests(ITestOutputHelper output) : IDisposable
{
    private static CadDocument Build(Rev r) => CadSamples.Build(r);

    private string Save(CadDocument doc, string name) => CadSamples.Save(doc, Path.Combine(_dir, name));

    private readonly string _dir = Directory.CreateTempSubdirectory("cad-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private CadCompareResult Run(Rev oldRev, Rev newRev, string ext = ".dxf")
    {
        var a = CadLoader.Load(Save(Build(oldRev), "old" + ext));
        var b = CadLoader.Load(Save(Build(newRev), "new" + ext));
        var result = CadComparer.Compare(a, b);
        output.WriteLine($"old {result.OldCount} / new {result.NewCount} primitives");
        foreach (var c in result.Changes) output.WriteLine($"  {c.Kind} {c.Type} [{c.Layer}] {c.Description}");
        foreach (var w in result.Warnings) output.WriteLine("  warning: " + w);
        return result;
    }

    [Fact] // 1
    public void Identical_drawings_have_no_changes()
    {
        var result = Run(new Rev(), new Rev());
        Assert.Empty(result.Changes);
        Assert.True(result.OldCount > 40); // 블록을 펼친 기본 요소 수
    }

    [Fact] // 기획서 3장: 객체 ID만 바뀐 것은 변경이 아니다
    public void Recreated_entities_with_new_handles_are_not_changes()
    {
        Assert.Empty(Run(new Rev(), new Rev { Reverse = true }).Changes);
    }

    [Fact] // 2
    public void Added_line_is_detected()
    {
        var c = Assert.Single(Run(new Rev(), new Rev { AddLine = true }).Changes);
        Assert.Equal(CadChangeKind.Added, c.Kind);
        Assert.Equal(PrimitiveType.Line, c.Type);
    }

    [Fact] // 3
    public void Removed_circle_is_detected()
    {
        var c = Assert.Single(Run(new Rev(), new Rev { RemoveCircle = true }).Changes);
        Assert.Equal(CadChangeKind.Removed, c.Kind);
        Assert.Equal(PrimitiveType.Circle, c.Type);
    }

    [Fact] // 4: 기둥(블록) 이동 → 펼친 요소가 이동으로 보고된다
    public void Moved_block_is_reported_as_moved()
    {
        var result = Run(new Rev(), new Rev { ColumnShiftX = 400 });
        Assert.NotEmpty(result.Changes);
        Assert.All(result.Changes, c => Assert.Equal(CadChangeKind.Moved, c.Kind));
        Assert.Contains(result.Changes, c => c.Description.Contains("moved 400 mm"));
    }

    [Fact] // 6
    public void Changed_text_is_reported_with_old_and_new_value()
    {
        var c = Assert.Single(Run(new Rev(), new Rev { Note = "VERIFY CLEARANCE AT COLUMN" }).Changes);
        Assert.Equal(CadChangeKind.Modified, c.Kind);
        Assert.Contains("\"VERIFY CLEARANCE\" → \"VERIFY CLEARANCE AT COLUMN\"", c.Description);
    }

    [Fact]
    public void Changed_radius_is_modified()
    {
        var c = Assert.Single(Run(new Rev(), new Rev { CircleRadius = 450 }).Changes);
        Assert.Equal(CadChangeKind.Modified, c.Kind);
        Assert.Contains("300 → 450", c.Description);
    }

    [Fact] // 5: 치수 하나가 바뀌면 이전 → 개정 값으로 보고한다
    public void Changed_dimension_is_detected_with_values()
    {
        var c = Assert.Single(Run(new Rev(), new Rev { DimEndX = 12500 }).Changes);
        Assert.Equal(CadChangeKind.Modified, c.Kind);
        Assert.Equal(PrimitiveType.Dimension, c.Type);
        Assert.Contains("\"6000\" → \"6500\"", c.Description);
    }

    [Fact]
    public void Changed_dimension_text_override_is_detected()
    {
        var c = Assert.Single(Run(new Rev(), new Rev { DimText = "6000 VIF" }).Changes);
        Assert.Equal(CadChangeKind.Modified, c.Kind);
        Assert.Contains("\"6000\" → \"6000 VIF\"", c.Description);
    }

    [Fact] // 문자를 덮어쓴 치수는 길이가 바뀌어도 숫자가 그대로라 따로 경고한다
    public void Overridden_dimension_with_changed_length_is_flagged()
    {
        var c = Assert.Single(Run(new Rev { DimText = "6000" }, new Rev { DimText = "6000", DimEndX = 12500 }).Changes);
        Assert.Contains("check text override", c.Description);
        Assert.Contains("6000 → 6500", c.Description);
    }

    [Fact]
    public void Dimensions_are_loaded_with_dimension_line_points()
    {
        var d = CadLoader.Load(Save(Build(new Rev()), "d.dwg"));
        var dims = d.Primitives.Where(p => p.Type == PrimitiveType.Dimension).ToList();
        Assert.Equal(3, dims.Count);
        Assert.All(dims, p => Assert.Equal(6000, p.Size, 3));
        var first = dims.First(p => p.Points[0].DistanceTo(new Pt(0, -1000)) < 1e-6);
        Assert.Equal(new Pt(0, -2000), first.Points[2]);
        Assert.Equal(new Pt(6000, -2000), first.Points[3]);
        Assert.DoesNotContain(d.Warnings, w => w.Contains("DIMENSION", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] // 12: 단위 차이 (mm 도면 vs m 도면)
    public void Unit_differences_are_handled()
    {
        var result = Run(new Rev(), new Rev { Scale = 0.001, Units = UnitsType.Meters });
        Assert.Empty(result.Changes);
        Assert.Contains(result.Warnings, w => w.Contains("Units differ"));
    }

    [Fact]
    public void Unitless_drawing_produces_warning()
    {
        var result = Run(new Rev { Units = UnitsType.Unitless }, new Rev { Units = UnitsType.Unitless });
        Assert.Contains(result.Warnings, w => w.Contains("Unitless"));
    }

    [Fact] // DWG 직접 읽기 (ODA 변환기 없이)
    public void Dwg_round_trip_matches_dxf()
    {
        var dxf = CadLoader.Load(Save(Build(new Rev()), "a.dxf"));
        var dwg = CadLoader.Load(Save(Build(new Rev()), "a.dwg"));
        Assert.Equal(dxf.Primitives.Count, dwg.Primitives.Count);
        Assert.Empty(CadComparer.Compare(dxf, dwg).Changes);
        var changed = Run(new Rev(), new Rev { AddLine = true, Note = "X" }, ".dwg");
        Assert.Equal(2, changed.Changes.Count);
    }

    [Fact] // 14
    public void Source_files_are_not_modified()
    {
        var path = Save(Build(new Rev()), "src.dwg");
        var before = File.ReadAllBytes(path);
        CadLoader.Load(path);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Nested_blocks_are_expanded_with_transforms()
    {
        var d = CadLoader.Load(Save(Build(new Rev()), "n.dxf"));
        // 기둥 15개 × (원 1 + 밑판 폴리선 1)
        Assert.Equal(15, d.Primitives.Count(p => p.Type == PrimitiveType.Polyline));
        var plate = d.Primitives.First(p => p.Type == PrimitiveType.Polyline && p.Bounds.Center.DistanceTo(new Pt(12000, 6000)) < 1);
        Assert.Equal(500, plate.Bounds.MaxX - plate.Bounds.MinX, 3);
    }

    [Fact] // 9: 큰 도면 성능 참고치
    public void Large_drawing_compares_quickly()
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = UnitsType.Millimeters;
        var rnd = new Random(1);
        for (int i = 0; i < 30000; i++)
            doc.Entities.Add(new Line { StartPoint = new XYZ(rnd.Next(0, 100000), rnd.Next(0, 70000), 0), EndPoint = new XYZ(rnd.Next(0, 100000), rnd.Next(0, 70000), 0) });
        var a = CadLoader.Normalize(doc);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = CadComparer.Compare(a, a);
        output.WriteLine($"30,000 lines compared in {sw.ElapsedMilliseconds} ms");
        Assert.Empty(result.Changes);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30));
    }
}
