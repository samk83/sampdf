using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;

namespace DrawingReview.Cad;

/// <summary>
/// DWG·DXF를 읽어 비교용 기본 요소로 정규화한다 (ACadSharp, MIT).
/// - DWG는 ACadSharp가 직접 읽는다 (R14~2018 형식). 읽을 수 없으면 오류로 알린다.
/// - 블록 삽입(Insert)은 풀어서(explode) 넣는다. 중첩 블록도 펼친다.
/// - 단위($INSUNITS)를 mm로 바꾼다. 단위가 없으면 경고한다.
/// - 처리하지 않는 객체 종류와 외부 참조(Xref)는 경고로 알린다 (조용히 무시하지 않음 — 기획서 7장).
/// </summary>
public static class CadLoader
{
    public static CadDrawing Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        CadDocument doc = ext switch
        {
            ".dwg" => DwgReader.Read(path),
            ".dxf" => DxfReader.Read(path),
            _ => throw new NotSupportedException($"Unsupported CAD file type: {ext}"),
        };
        return Normalize(doc);
    }

    public static CadDrawing Normalize(CadDocument doc)
    {
        var warnings = new List<string>();
        var (unitName, toMm) = Units(doc.Header.InsUnits);
        if (doc.Header.InsUnits == UnitsType.Unitless)
            warnings.Add("Drawing units are not set ($INSUNITS = Unitless); values are treated as millimeters.");

        foreach (var block in doc.BlockRecords)
        {
            var flags = block.BlockEntity?.Flags ?? ACadSharp.Blocks.BlockTypeFlags.None;
            if (flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRef) || flags.HasFlag(ACadSharp.Blocks.BlockTypeFlags.XRefOverlay))
                warnings.Add($"External reference \"{block.Name}\" is not loaded; its content is not compared.");
        }

        var result = new List<CadPrimitive>();
        var unsupported = new Dictionary<string, int>();
        var identity = new Transform(Matrix4.Identity);
        foreach (var e in doc.Entities) Add(e, identity, result, unsupported, toMm, depth: 0);
        foreach (var (type, count) in unsupported.OrderByDescending(k => k.Value))
            warnings.Add($"{count} {type} object(s) are not compared yet.");
        return new CadDrawing(unitName, toMm, result, warnings);
    }

    private static void Add(Entity e, Transform t, List<CadPrimitive> output, Dictionary<string, int> unsupported, double toMm, int depth)
    {
        var layer = e.Layer?.Name ?? "0";
        Pt P(XYZ v) { var w = t.ApplyTransform(v, false); return new Pt(w.X * toMm, w.Y * toMm); }
        var scale = Math.Sqrt(Math.Abs(t.Matrix.M00 * t.Matrix.M11 - t.Matrix.M01 * t.Matrix.M10));
        switch (e)
        {
            case Line l:
                {
                    var a = P(l.StartPoint);
                    var b = P(l.EndPoint);
                    // 방향과 무관하게 같은 선으로 보도록 끝점을 정렬한다
                    if (b.X < a.X || (Math.Abs(b.X - a.X) < 1e-9 && b.Y < a.Y)) (a, b) = (b, a);
                    output.Add(new CadPrimitive(PrimitiveType.Line, layer, [a, b]));
                    break;
                }
            case Arc arc:
                output.Add(new CadPrimitive(PrimitiveType.Arc, layer, [P(arc.Center)], arc.Radius * scale * toMm, "",
                    arc.StartAngle * 180 / Math.PI, arc.EndAngle * 180 / Math.PI));
                break;
            case Circle c:
                output.Add(new CadPrimitive(PrimitiveType.Circle, layer, [P(c.Center)], c.Radius * scale * toMm));
                break;
            case LwPolyline pl:
                output.Add(new CadPrimitive(PrimitiveType.Polyline, layer,
                    pl.Vertices.Select(v => P(new XYZ(v.Location.X, v.Location.Y, 0))).ToList(), Closed: pl.IsClosed));
                break;
            case TextEntity text:
                output.Add(new CadPrimitive(PrimitiveType.Text, layer, [P(text.InsertPoint)], text.Height * scale * toMm, text.Value ?? "", text.Rotation));
                break;
            case MText mtext:
                output.Add(new CadPrimitive(PrimitiveType.Text, layer, [P(mtext.InsertPoint)], mtext.Height * scale * toMm, mtext.Value ?? "", mtext.Rotation));
                break;
            case Dimension dim:
                output.Add(DimensionPrimitive(dim, layer, P, scale * toMm));
                break;
            case Insert insert when depth < 16:
                {
                    // 블록 내용을 삽입 위치·배율·회전으로 옮겨 넣는다 (중첩 블록 포함)
                    var inner = insert.GetTransform();
                    // CSMath는 행 벡터 방식(v × M)이라 안쪽(블록) 변환을 먼저 곱한다
                    var combined = new Transform(inner.Matrix * t.Matrix);
                    foreach (var child in insert.Block.Entities)
                        Add(child, combined, output, unsupported, toMm, depth + 1);
                    if (insert.Attributes != null)
                        foreach (var att in insert.Attributes)
                            output.Add(new CadPrimitive(PrimitiveType.Text, layer, [P(att.InsertPoint)], att.Height * toMm, att.Value ?? "", att.Rotation));
                    break;
                }
            default:
                var name = e.ObjectName;
                unsupported[name] = unsupported.GetValueOrDefault(name) + 1;
                break;
        }
    }

    /// <summary>
    /// 치수를 비교용 요소로 만든다. 치수의 그림 블록(*D)은 펼치지 않고 치수 자체의 값·문자·점을 쓴다.
    /// 선형·정렬 치수는 치수선 양 끝도 계산해 두어 화면에 다시 그릴 수 있게 한다.
    /// </summary>
    private static CadPrimitive DimensionPrimitive(Dimension dim, string layer, Func<XYZ, Pt> P, double lengthScale)
    {
        string text;
        try { text = dim.GetMeasurementText(); }
        catch { text = string.IsNullOrEmpty(dim.Text) ? dim.Measurement.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : dim.Text; }
        var measure = dim.IsAngular ? dim.Measurement : dim.Measurement * lengthScale;
        // 문자를 직접 쓴 치수(override)는 A1 = 1. 자동 문자 치수는 단위 표시가 달라도 측정값(mm)으로 비교한다.
        var overridden = !string.IsNullOrEmpty(dim.Text) && dim.Text != "<>" ? 1 : 0;

        if (dim is DimensionAligned aligned)
        {
            var p1 = aligned.FirstPoint;
            var p2 = aligned.SecondPoint;
            var def = aligned.DefinitionPoint;
            // 치수선 방향: 회전 치수(Linear)는 Rotation, 정렬 치수는 재는 두 점 방향
            double ux, uy;
            if (dim is DimensionLinear linear) { ux = Math.Cos(linear.Rotation); uy = Math.Sin(linear.Rotation); }
            else
            {
                var len = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
                (ux, uy) = len > 1e-12 ? ((p2.X - p1.X) / len, (p2.Y - p1.Y) / len) : (1, 0);
            }
            XYZ OnLine(XYZ p)
            {
                var t = (p.X - def.X) * ux + (p.Y - def.Y) * uy;
                return new XYZ(def.X + ux * t, def.Y + uy * t, 0);
            }
            return new CadPrimitive(PrimitiveType.Dimension, layer, [P(p1), P(p2), P(OnLine(p1)), P(OnLine(p2))], measure, text, overridden);
        }
        return new CadPrimitive(PrimitiveType.Dimension, layer, [P(dim.DefinitionPoint)], measure, text, overridden);
    }

    /// <summary>$INSUNITS → (이름, mm 배율).</summary>
    public static (string Name, double ToMm) Units(UnitsType u) => u switch
    {
        UnitsType.Millimeters => ("mm", 1),
        UnitsType.Centimeters => ("cm", 10),
        UnitsType.Meters => ("m", 1000),
        UnitsType.Inches => ("in", 25.4),
        UnitsType.Feet => ("ft", 304.8),
        UnitsType.Unitless => ("unitless", 1),
        _ => (u.ToString(), 1),
    };
}
