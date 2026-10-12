using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.Engine.Vector;

/// <summary>
/// 직접 그리는 벡터 페이지 (예: CAD 도면을 PDF로 옮길 때).
/// 좌표는 다른 곳과 같은 "보이는 페이지 좌표"(pt, 왼쪽 위 원점, 아래로 증가)다.
/// </summary>
public sealed record VectorPage(double Width, double Height, IReadOnlyList<VectorItem> Items);

public abstract record VectorItem(RgbColor Color);

/// <summary>꺾은선. 원·호는 짧은 선분으로 나눠 넣는다.</summary>
public sealed record VectorPolyline(IReadOnlyList<PagePoint> Points, bool Closed, RgbColor Color, double LineWidth) : VectorItem(Color);

/// <summary>한 줄 글자. Baseline은 글자 기준선의 시작점, RotationDegrees는 화면에서 반시계 방향 회전.</summary>
public sealed record VectorText(PagePoint Baseline, string Text, double Size, double RotationDegrees, RgbColor Color) : VectorItem(Color);
