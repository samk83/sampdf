using System.Text.RegularExpressions;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Vector;

namespace DrawingReview.Cad;

/// <summary>
/// CAD 모델 좌표(mm, y 위로 증가) ↔ PDF 페이지 좌표(pt, y 아래로 증가) 변환.
/// 이전·개정 도면에 같은 변환을 써서 두 PDF가 정확히 겹치게 한다 (정렬이 필요 없음).
/// </summary>
public sealed record CadPageMapping(Box Extents, double Scale, double Margin, double PageWidth, double PageHeight)
{
    /// <summary>도면 범위를 긴 변 longEdgePt 크기의 페이지에 여백을 두고 맞춘다 (기본 A0 긴 변).</summary>
    public static CadPageMapping Fit(Box extents, double longEdgePt = 2384, double marginPt = 36)
    {
        var w = Math.Max(extents.MaxX - extents.MinX, 1e-6);
        var h = Math.Max(extents.MaxY - extents.MinY, 1e-6);
        var scale = (longEdgePt - 2 * marginPt) / Math.Max(w, h);
        // 아주 가는 도면도 페이지가 너무 납작해지지 않게 짧은 변을 최소 200pt로 둔다
        var pw = Math.Max(w * scale + 2 * marginPt, 200);
        var ph = Math.Max(h * scale + 2 * marginPt, 200);
        return new CadPageMapping(extents, scale, marginPt, pw, ph);
    }

    public PagePoint ToPage(Pt p) => new(Margin + (p.X - Extents.MinX) * Scale, Margin + (Extents.MaxY - p.Y) * Scale);

    public PageRect ToPage(Box b)
    {
        var a = ToPage(new Pt(b.MinX, b.MaxY));
        var c = ToPage(new Pt(b.MaxX, b.MinY));
        return new PageRect(a.X, a.Y, c.X, c.Y);
    }

    public Pt ToModel(PagePoint p) => new(Extents.MinX + (p.X - Margin) / Scale, Extents.MaxY - (p.Y - Margin) / Scale);

    public Box ToModel(PageRect r)
    {
        var a = ToModel(new PagePoint(r.X0, r.Y1));
        var c = ToModel(new PagePoint(r.X1, r.Y0));
        return new Box(a.X, a.Y, c.X, c.Y);
    }
}

/// <summary>
/// CAD 도면을 벡터 페이지로 그린다 (비교 결과를 기존 PDF 비교 화면에서 보여 주기 위한 "간단한 선 렌더러").
/// 모든 선은 한 가지 색·굵기로 그린다 (선 종류·도면층 색·해치는 그리지 않음).
/// </summary>
public static class CadPlotter
{
    /// <summary>여러 도면을 모두 담는 범위 (mm). 요소가 없으면 1×1 영역.</summary>
    public static Box Extents(params CadDrawing[] drawings)
    {
        Box? box = null;
        foreach (var p in drawings.SelectMany(d => d.Primitives))
            box = box is { } b ? b.Union(p.Bounds) : p.Bounds;
        return box ?? new Box(0, 0, 1, 1);
    }

    public static VectorPage Plot(CadDrawing drawing, CadPageMapping map, double lineWidthPt = 0.5)
    {
        var color = RgbColor.Black;
        var items = new List<VectorItem>(drawing.Primitives.Count);
        void Poly(IEnumerable<Pt> pts, bool closed = false) =>
            items.Add(new VectorPolyline(pts.Select(map.ToPage).ToList(), closed, color, lineWidthPt));
        void Text(Pt baseline, string text, double heightMm, double rotation)
        {
            var size = heightMm * map.Scale;
            if (size >= 0.5) items.Add(new VectorText(map.ToPage(baseline), text, size, rotation, color));
        }

        foreach (var p in drawing.Primitives)
        {
            switch (p.Type)
            {
                case PrimitiveType.Line:
                    Poly(p.Points);
                    break;
                case PrimitiveType.Polyline:
                    Poly(p.Points, p.Closed);
                    break;
                case PrimitiveType.Circle:
                    Poly(ArcPoints(p.Points[0], p.Size, 0, 360), closed: true);
                    break;
                case PrimitiveType.Arc:
                    {
                        var sweep = ((p.A2 - p.A1) % 360 + 360) % 360;
                        Poly(ArcPoints(p.Points[0], p.Size, p.A1, sweep == 0 ? 360 : sweep));
                        break;
                    }
                case PrimitiveType.Text:
                    {
                        var lines = PlainText(p.Text).Split('\n');
                        var rad = p.A1 * Math.PI / 180;
                        var down = new Pt(Math.Sin(rad), -Math.Cos(rad)); // 글자 방향에 수직인 아래쪽
                        var pitch = p.Size * 1.5;
                        // 여러 줄 글자의 삽입점이 위·가운데 기준이면 첫 줄 기준선을 아래로 옮긴다
                        var first = p.A2 switch { 1 => p.Size, 2 => p.Size / 2 - (lines.Length - 1) * pitch / 2, _ => 0 };
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var d = first + i * pitch;
                            Text(new Pt(p.Points[0].X + down.X * d, p.Points[0].Y + down.Y * d), lines[i], p.Size, p.A1);
                        }
                        break;
                    }
                case PrimitiveType.Dimension:
                    PlotDimension(p, Poly, Text);
                    break;
            }
        }
        return new VectorPage(map.PageWidth, map.PageHeight, items);
    }

    /// <summary>치수: 보조선, 치수선, 양 끝 사선 표시, 가운데 위쪽에 치수 문자.</summary>
    private static void PlotDimension(CadPrimitive p, Action<IEnumerable<Pt>, bool> poly, Action<Pt, string, double, double> text)
    {
        if (p.Points.Count < 4)
        {
            // 각도·반지름 등: 정의점에 문자만 표시
            text(p.Points[0], p.Text, p.A2 > 0 ? p.A2 : 2.5, 0);
            return;
        }
        Pt p1 = p.Points[0], p2 = p.Points[1], d1 = p.Points[2], d2 = p.Points[3];
        var len = d1.DistanceTo(d2);
        // 스타일의 문자 높이가 없거나 치수 길이에 비해 지나치게 작으면(배율 미설정 등) 보기 좋은 크기로 대신한다
        var h = p.A2 >= len / 500 && p.A2 > 0 ? p.A2 : Math.Max(len / 30, 1e-3);
        poly([p1, d1], false);
        poly([p2, d2], false);
        poly([d1, d2], false);
        if (len < 1e-9) return;
        var u = new Pt((d2.X - d1.X) / len, (d2.Y - d1.Y) / len);
        var tick = h * 0.5;
        foreach (var d in new[] { d1, d2 })
            poly([new Pt(d.X - (u.X - u.Y) * tick / 2, d.Y - (u.Y + u.X) * tick / 2), new Pt(d.X + (u.X - u.Y) * tick / 2, d.Y + (u.Y + u.X) * tick / 2)], false);

        // 문자는 읽기 쉬운 방향(-90°~90°)으로, 치수선 가운데 위쪽에 놓는다
        var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
        if (angle > 90.001 || angle <= -90) { angle += 180; u = new Pt(-u.X, -u.Y); }
        var normal = new Pt(-u.Y, u.X);
        var width = p.Text.Length * h * 0.6;
        var mid = new Pt((d1.X + d2.X) / 2, (d1.Y + d2.Y) / 2);
        text(new Pt(mid.X - u.X * width / 2 + normal.X * h * 0.4, mid.Y - u.Y * width / 2 + normal.Y * h * 0.4), p.Text, h, angle);
    }

    private static IEnumerable<Pt> ArcPoints(Pt c, double r, double startDeg, double sweepDeg)
    {
        var n = Math.Clamp((int)Math.Ceiling(sweepDeg / 5), 4, 90);
        for (int i = 0; i <= n; i++)
        {
            var a = (startDeg + sweepDeg * i / n) * Math.PI / 180;
            yield return new Pt(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
        }
    }

    /// <summary>여러 줄 글자(MText)의 서식 코드를 지우고 줄바꿈(\P)을 실제 줄로 바꾼다.</summary>
    public static string PlainText(string s)
    {
        if (s.IndexOf('\\') < 0 && s.IndexOf('{') < 0) return s;
        s = s.Replace("\\P", "\n").Replace("\\~", " ");
        s = Regex.Replace(s, @"\\[ACcFfHhQTWp][^;]*;", "");   // 글꼴·높이·색 등 ;로 끝나는 코드
        s = Regex.Replace(s, @"\\[LlOoKk]", "");              // 밑줄·윗줄·취소선
        s = Regex.Replace(s, @"\\S([^;^]*)[\^/#]([^;]*);", "$1/$2"); // 분수
        return s.Replace("{", "").Replace("}", "").Replace("\\\\", "\\");
    }
}
