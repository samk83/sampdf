using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using Xunit.Abstractions;

namespace DrawingReview.Cad.Tests;

/// <summary>DWG/DXF 비교 0단계 검증: 가상 CAD 도면으로 기획서 12장 조건을 시험한다.</summary>
public class CadCompareTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cad-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    public sealed record Rev
    {
        public bool AddLine { get; init; }
        public bool RemoveCircle { get; init; }
        public string Note { get; init; } = "VERIFY CLEARANCE";
        public double ColumnShiftX { get; init; }
        public double Scale { get; init; } = 1;           // 1 = mm 도면, 0.001 = m 도면
        public UnitsType Units { get; init; } = UnitsType.Millimeters;
        public bool Reverse { get; init; }               // 객체 순서를 뒤집어 다시 만든다 (handle이 바뀜)
        public double CircleRadius { get; init; } = 300;
    }

    /// <summary>단순한 구조 평면: 그리드 선, 기둥 블록(중첩), 원, 글자, 폴리선 벽.</summary>
    private static CadDocument Build(Rev r)
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = r.Units;
        var s = r.Scale;
        XYZ P(double x, double y) => new(x * s, y * s, 0);

        // 중첩 블록: COL(기둥) 안에 BASE(밑판)
        var basePlate = new BlockRecord("BASE");
        var plate = new LwPolyline { IsClosed = true };
        foreach (var (x, y) in new[] { (-250, -250), (250, -250), (250, 250), (-250, 250) })
            plate.Vertices.Add(new LwPolyline.Vertex(new XY(x * s, y * s)));
        basePlate.Entities.Add(plate);
        doc.BlockRecords.Add(basePlate);
        var col = new BlockRecord("COL");
        col.Entities.Add(new Circle { Center = P(0, 0), Radius = 150 * s });
        col.Entities.Add(new Insert(basePlate));
        doc.BlockRecords.Add(col);

        var entities = new List<Entity>();
        for (int i = 0; i <= 4; i++) entities.Add(new Line { StartPoint = P(i * 6000, -1000), EndPoint = P(i * 6000, 13000) });
        for (int j = 0; j <= 2; j++) entities.Add(new Line { StartPoint = P(-1000, j * 6000), EndPoint = P(25000, j * 6000) });
        for (int i = 0; i <= 4; i++)
            for (int j = 0; j <= 2; j++)
            {
                var shift = i == 2 && j == 1 ? r.ColumnShiftX : 0;
                entities.Add(new Insert(col) { InsertPoint = P(i * 6000 + shift, j * 6000) });
            }
        if (!r.RemoveCircle) entities.Add(new Circle { Center = P(9000, 3000), Radius = r.CircleRadius * s });
        entities.Add(new TextEntity { InsertPoint = P(1000, 9500), Height = 250 * s, Value = r.Note });
        entities.Add(new MText { InsertPoint = P(15000, 9500), Height = 250 * s, Value = "S-101 FRAMING PLAN" });
        entities.Add(new Arc { Center = P(18000, 3000), Radius = 900 * s, StartAngle = 0, EndAngle = Math.PI / 2 });
        if (r.AddLine) entities.Add(new Line { StartPoint = P(3000, 4500), EndPoint = P(9000, 4500) });
        if (r.Reverse) entities.Reverse();
        foreach (var e in entities) doc.Entities.Add(e);
        return doc;
    }

    private string Save(CadDocument doc, string name)
    {
        var path = Path.Combine(_dir, name);
        if (name.EndsWith(".dwg")) { using var w = new DwgWriter(path, doc); w.Write(); }
        else { using var w = new DxfWriter(path, doc, false); w.Write(); }
        return path;
    }

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
