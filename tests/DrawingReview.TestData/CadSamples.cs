using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;

namespace DrawingReview.TestData;

/// <summary>
/// 가상 CAD 도면(구조 평면) 생성기: 그리드 선, 중첩 블록 기둥, 원, 호, 글자, 여러 줄 글자, 치수.
/// Rev 옵션으로 개정 내용을 바꿔 비교 시험에 쓴다.
/// </summary>
public static class CadSamples
{
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
        public double DimEndX { get; init; } = 12000;     // 두 번째 치수의 끝점 (치수 값 변경용)
        public string DimText { get; init; } = "";        // 두 번째 치수의 문자 덮어쓰기 ("" = 측정값 그대로)
    }

    /// <summary>단순한 구조 평면: 그리드 선, 기둥 블록(중첩), 원, 글자, 폴리선 벽.</summary>
    public static CadDocument Build(Rev r)
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

        // 90° 회전해 넣는 문 블록 (호 + 글자): 블록 변환이 각도에 반영되는지 시험
        var door = new BlockRecord("DOOR");
        door.Entities.Add(new Arc { Center = P(0, 0), Radius = 900 * s, StartAngle = 0, EndAngle = Math.PI / 2 });
        door.Entities.Add(new TextEntity { InsertPoint = P(100, 100), Height = 200 * s, Value = "D1" });
        doc.BlockRecords.Add(door);

        var entities = new List<Entity>();
        entities.Add(new Insert(door) { InsertPoint = P(21000, 9000), Rotation = Math.PI / 2 });
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
        // 치수: 그리드 간격 6000 두 개 (선형 치수) + 세로 정렬 치수
        entities.Add(new DimensionLinear { FirstPoint = P(0, -1000), SecondPoint = P(6000, -1000), DefinitionPoint = P(6000, -2000) });
        entities.Add(new DimensionLinear { FirstPoint = P(6000, -1000), SecondPoint = P(r.DimEndX, -1000), DefinitionPoint = P(r.DimEndX, -2000), Text = r.DimText });
        entities.Add(new DimensionAligned(P(-1000, 0), P(-1000, 6000)) { Offset = 1000 * s });
        if (r.AddLine) entities.Add(new Line { StartPoint = P(3000, 4500), EndPoint = P(9000, 4500) });
        if (r.Reverse) entities.Reverse();
        foreach (var e in entities) doc.Entities.Add(e);
        return doc;
    }

    /// <summary>확장자(.dwg/.dxf)에 맞춰 저장하고 경로를 돌려준다.</summary>
    public static string Save(CadDocument doc, string path)
    {
        if (path.EndsWith(".dwg")) { using var w = new DwgWriter(path, doc); w.Write(); }
        else { using var w = new DxfWriter(path, doc, false); w.Write(); }
        return path;
    }

}
