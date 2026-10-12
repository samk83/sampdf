using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Types.Units;
using DrawingReview.TestData;
using Rev = DrawingReview.TestData.CadSamples.Rev;

namespace DrawingReview.Cad.Tests;

/// <summary>DXF/DWG 사본에 Revision Cloud 넣기 (원본 보호).</summary>
public class CadCloudExportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cadcloud-").FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private string Source(string name, Rev? rev = null) => CadSamples.Save(CadSamples.Build(rev ?? new Rev()), Path.Combine(_dir, name));

    [Theory]
    [InlineData("new.dxf", "out.dxf")]
    [InlineData("new.dwg", "out.dwg")]
    [InlineData("new.dwg", "out.dxf")]
    public void Clouds_are_added_to_a_copy_on_revision_layer(string sourceName, string outName)
    {
        var src = Source(sourceName);
        var before = File.ReadAllBytes(src);
        var output = Path.Combine(_dir, outName);
        var clouds = new[] { new CadCloud(new Box(8000, 2000, 10000, 4000), 1), new CadCloud(new Box(500, 9000, 6000, 10000), 2) };

        Assert.Equal(2, CadCloudExporter.Export(src, output, clouds));

        Assert.Equal(before, File.ReadAllBytes(src)); // 원본 그대로
        var doc = outName.EndsWith(".dwg") ? DwgReader.Read(output) : DxfReader.Read(output);
        var cloudPolys = doc.Entities.OfType<LwPolyline>().Where(e => e.Layer?.Name == CadCloudExporter.LayerName).ToList();
        Assert.Equal(2, cloudPolys.Count);
        Assert.All(cloudPolys, p => Assert.All(p.Vertices, v => Assert.True(v.Bulge < 0)));
        Assert.Contains(doc.Entities.OfType<TextEntity>(), t => t.Layer?.Name == CadCloudExporter.LayerName && t.Value == "2");

        // 사본을 원본과 비교하면 REVISION 도면층에 추가된 것만 나온다
        var result = CadComparer.Compare(CadLoader.Load(src), CadLoader.Load(output));
        Assert.Equal(4, result.Changes.Count);
        Assert.All(result.Changes, c => { Assert.Equal(CadChangeKind.Added, c.Kind); Assert.Equal(CadCloudExporter.LayerName, c.Layer); });
        var first = CadLoader.Load(output).Primitives.First(p => p.Layer == CadCloudExporter.LayerName && p.Type == PrimitiveType.Polyline);
        Assert.Equal(8000, first.Bounds.MinX, 3);
        Assert.Equal(4000, first.Bounds.MaxY, 3);
    }

    [Fact]
    public void Cloud_coordinates_are_converted_back_to_drawing_units()
    {
        var src = Source("m.dxf", new Rev { Scale = 0.001, Units = UnitsType.Meters });
        var output = Path.Combine(_dir, "m-out.dxf");
        CadCloudExporter.Export(src, output, [new CadCloud(new Box(8000, 2000, 10000, 4000), 1)]);
        var poly = DxfReader.Read(output).Entities.OfType<LwPolyline>().Single(e => e.Layer?.Name == CadCloudExporter.LayerName);
        Assert.Equal(8, poly.Vertices.Min(v => v.Location.X), 6);
        Assert.Equal(4, poly.Vertices.Max(v => v.Location.Y), 6);
    }

    [Fact]
    public void Overwriting_the_source_is_refused()
    {
        var src = Source("s.dxf");
        Assert.Throws<InvalidOperationException>(() => CadCloudExporter.Export(src, src, [new CadCloud(new Box(0, 0, 1, 1), 1)]));
    }
}
