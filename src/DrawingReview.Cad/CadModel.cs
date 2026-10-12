namespace DrawingReview.Cad;

/// <summary>2D 점 (mm, 도면 모델 좌표).</summary>
public readonly record struct Pt(double X, double Y)
{
    public double DistanceTo(Pt o) => Math.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y));
    public static Pt operator -(Pt a, Pt b) => new(a.X - b.X, a.Y - b.Y);
    public static Pt operator +(Pt a, Pt b) => new(a.X + b.X, a.Y + b.Y);
}

/// <summary>2D 영역 (mm).</summary>
public readonly record struct Box(double MinX, double MinY, double MaxX, double MaxY)
{
    public static Box Of(IEnumerable<Pt> pts)
    {
        var l = pts.ToList();
        return new Box(l.Min(p => p.X), l.Min(p => p.Y), l.Max(p => p.X), l.Max(p => p.Y));
    }

    public Box Inflate(double d) => new(MinX - d, MinY - d, MaxX + d, MaxY + d);
    public bool Intersects(Box o) => MinX <= o.MaxX && o.MinX <= MaxX && MinY <= o.MaxY && o.MinY <= MaxY;
    public Box Union(Box o) => new(Math.Min(MinX, o.MinX), Math.Min(MinY, o.MinY), Math.Max(MaxX, o.MaxX), Math.Max(MaxY, o.MaxY));
    public Pt Center => new((MinX + MaxX) / 2, (MinY + MaxY) / 2);
}

public enum PrimitiveType { Line, Circle, Arc, Polyline, Text }

/// <summary>
/// 비교용으로 정규화한 도면 요소 하나. 블록은 풀어서 넣고, 단위는 mm로 맞췄다.
/// Points: 선=양 끝(정렬됨), 원·호=중심, 폴리선=꼭짓점들, 글자=삽입점.
/// Size: 원·호=반지름, 글자=높이. Extra: 호=시작·끝 각도(도), 글자=회전.
/// </summary>
public sealed record CadPrimitive(PrimitiveType Type, string Layer, IReadOnlyList<Pt> Points, double Size = 0, string Text = "", double A1 = 0, double A2 = 0, bool Closed = false)
{
    public Box Bounds => Type switch
    {
        PrimitiveType.Circle or PrimitiveType.Arc => new Box(Points[0].X - Size, Points[0].Y - Size, Points[0].X + Size, Points[0].Y + Size),
        PrimitiveType.Text => new Box(Points[0].X, Points[0].Y, Points[0].X + Math.Max(Size, Text.Length * Size * 0.7), Points[0].Y + Size),
        _ => Box.Of(Points),
    };

    /// <summary>위치와 무관한 모양 특징 (이동 판정용): 길이·반지름·글자.</summary>
    public string ShapeSignature => Type switch
    {
        PrimitiveType.Line => $"L{Points[0].DistanceTo(Points[1]):0.0}|{Angle(Points[0], Points[1]):0.0}",
        PrimitiveType.Circle => $"C{Size:0.0}",
        PrimitiveType.Arc => $"A{Size:0.0}|{A1:0.0}|{A2:0.0}",
        PrimitiveType.Polyline => $"P{Points.Count}|" + string.Join(";", Points.Skip(1).Select(p => $"{p.X - Points[0].X:0.0},{p.Y - Points[0].Y:0.0}")),
        _ => $"T{Text}|{Size:0.0}",
    };

    private static double Angle(Pt a, Pt b) => (Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI + 360) % 180;
}

public sealed record CadDrawing(string UnitsName, double UnitToMm, IReadOnlyList<CadPrimitive> Primitives, IReadOnlyList<string> Warnings);

public enum CadChangeKind { Added, Removed, Modified, Moved }

/// <summary>CAD 변경 하나. Bounds는 개정 도면 기준(삭제는 이전 도면 위치), mm.</summary>
public sealed record CadChange(CadChangeKind Kind, PrimitiveType Type, string Layer, Box Bounds, string Description);

public sealed record CadCompareResult(IReadOnlyList<CadChange> Changes, int OldCount, int NewCount, IReadOnlyList<string> Warnings);
