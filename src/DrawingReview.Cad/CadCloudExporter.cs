using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using DrawingReview.Engine.Persistence;

namespace DrawingReview.Cad;

/// <summary>Revision Cloud 하나: 감쌀 영역(mm, 모델 좌표)과 번호.</summary>
public sealed record CadCloud(Box Bounds, int Number);

/// <summary>
/// 개정 도면의 사본에 Revision Cloud를 CAD 객체로 넣어 DXF 또는 DWG로 저장한다 (기획서 10장 Export).
/// - 원본 파일은 절대 바꾸지 않는다 (같은 경로로는 저장 거부).
/// - Cloud는 볼록 호(bulge)가 있는 닫힌 폴리선, 번호는 글자. 모두 "REVISION" 도면층(빨강)에 넣는다.
/// - 주의: 사본은 ACadSharp가 다시 쓴 파일이다. ACadSharp가 읽지 못한 객체(일부 사용자 정의 객체 등)는
///   사본에서 빠질 수 있으므로 CAD 프로그램에서 열어 확인한다.
/// </summary>
public static class CadCloudExporter
{
    public const string LayerName = "REVISION";

    /// <returns>사본에 넣은 Cloud 수.</returns>
    public static int Export(string sourcePath, string outputPath, IReadOnlyList<CadCloud> clouds)
    {
        var source = Path.GetFullPath(sourcePath);
        var output = Path.GetFullPath(outputPath);
        if (string.Equals(source, output, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The original drawing is never overwritten. Choose a new file name.");
        var outExt = Path.GetExtension(output).ToLowerInvariant();
        if (outExt is not (".dxf" or ".dwg")) throw new NotSupportedException("Export clouds as .dxf or .dwg.");
        if (clouds.Count == 0) throw new ArgumentException("There are no clouds to export.", nameof(clouds));

        var doc = Path.GetExtension(source).ToLowerInvariant() == ".dwg" ? DwgReader.Read(source) : DxfReader.Read(source);
        var (_, toMm) = CadLoader.Units(doc.Header.InsUnits);

        if (!doc.Layers.TryGetValue(LayerName, out var layer))
        {
            layer = new Layer(LayerName) { Color = Color.Red };
            doc.Layers.Add(layer);
        }

        foreach (var cloud in clouds)
        {
            // mm → 도면 단위
            var b = new Box(cloud.Bounds.MinX / toMm, cloud.Bounds.MinY / toMm, cloud.Bounds.MaxX / toMm, cloud.Bounds.MaxY / toMm);
            var poly = CloudPolyline(b, out var arc);
            poly.Layer = layer;
            doc.Entities.Add(poly);
            doc.Entities.Add(new TextEntity
            {
                Layer = layer,
                Value = cloud.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Height = arc * 0.8,
                InsertPoint = new XYZ(b.MaxX + arc * 0.5, b.MaxY + arc * 0.5, 0),
            });
        }

        SafeFileSaver.Save(output, temp =>
        {
            if (outExt == ".dwg") { using var w = new DwgWriter(temp, doc); w.Write(); }
            else { using var w = new DxfWriter(temp, doc, false); w.Write(); }
        }, temp =>
        {
            var check = outExt == ".dwg" ? DwgReader.Read(temp) : DxfReader.Read(temp);
            var count = check.Entities.OfType<LwPolyline>().Count(e => e.Layer?.Name == LayerName);
            if (count < clouds.Count) throw new IOException("The exported drawing could not be verified.");
        });
        return clouds.Count;
    }

    /// <summary>영역을 감싸는 구름 모양 닫힌 폴리선 (반시계 방향, 바깥으로 볼록한 호). arc = 호 하나의 길이.</summary>
    public static LwPolyline CloudPolyline(Box b, out double arc)
    {
        var w = Math.Max(b.MaxX - b.MinX, 1e-6);
        var h = Math.Max(b.MaxY - b.MinY, 1e-6);
        arc = Math.Max(Math.Min(w, h) / 3, Math.Max(w, h) / 12);
        var poly = new LwPolyline { IsClosed = true };
        var corners = new[] { new XY(b.MinX, b.MinY), new XY(b.MaxX, b.MinY), new XY(b.MaxX, b.MaxY), new XY(b.MinX, b.MaxY) };
        for (int k = 0; k < 4; k++)
        {
            var a = corners[k];
            var c = corners[(k + 1) % 4];
            var len = Math.Sqrt((c.X - a.X) * (c.X - a.X) + (c.Y - a.Y) * (c.Y - a.Y));
            var n = Math.Max(1, (int)Math.Round(len / arc));
            for (int i = 0; i < n; i++)
            {
                var t = (double)i / n;
                // 반시계로 돌 때 바깥쪽은 진행 방향의 오른쪽 → 시계 방향 호(음수 bulge)
                poly.Vertices.Add(new LwPolyline.Vertex(new XY(a.X + (c.X - a.X) * t, a.Y + (c.Y - a.Y) * t)) { Bulge = -0.4 });
            }
        }
        return poly;
    }
}
