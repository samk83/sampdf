namespace DrawingReview.Engine.Geometry;

// 모든 좌표는 "보이는 페이지 좌표"(PDF 포인트, 1pt = 1/72 inch)를 사용한다.
// 원점은 화면에 보이는 페이지의 왼쪽 위이며, 페이지 회전(/Rotate)과 CropBox가 이미 반영된 상태다.
// 화면 픽셀 좌표는 UI 계층에서 확대율로 변환하고, 엔진 안으로는 이 좌표만 들어온다.

public readonly record struct PagePoint(double X, double Y);

public readonly record struct PageRect(double X0, double Y0, double X1, double Y1)
{
    public double Width => X1 - X0;
    public double Height => Y1 - Y0;
    public PagePoint Center => new((X0 + X1) / 2, (Y0 + Y1) / 2);

    public static PageRect FromPoints(IEnumerable<PagePoint> points)
    {
        var list = points.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one point is required.", nameof(points));
        return new PageRect(list.Min(p => p.X), list.Min(p => p.Y), list.Max(p => p.X), list.Max(p => p.Y));
    }

    public PageRect Inflate(double amount) => new(X0 - amount, Y0 - amount, X1 + amount, Y1 + amount);

    public bool Contains(PageRect other) =>
        other.X0 >= X0 && other.Y0 >= Y0 && other.X1 <= X1 && other.Y1 <= Y1;
}

/// <summary>페이지 한 장의 크기·회전 정보. Width/Height는 회전이 반영된 보이는 크기다.</summary>
public sealed record PageInfo(int Index, double Width, double Height, int Rotation, string? Label);

/// <summary>페이지의 단어 하나와 위치 (보이는 페이지 좌표).</summary>
public sealed record TextWord(string Text, PageRect Bounds);

/// <summary>책갈피 한 줄. Level은 1부터, PageIndex는 0부터 (-1 = 페이지 없음).</summary>
public sealed record OutlineItem(int Level, string Title, int PageIndex);
