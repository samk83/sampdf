using System.Text;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using static DrawingReview.Engine.Pdf.PdfValues;

namespace DrawingReview.Engine.Pdf;

/// <summary>주석 외형(/AP /N) 내용과 그 경계 상자. 좌표는 모두 PDF 좌표.</summary>
internal sealed class Appearance
{
    public required string Content { get; init; }
    public required (double X0, double Y0, double X1, double Y1) BBox { get; init; }
    public bool UsesHelvetica { get; init; }
    public bool UsesKorean { get; init; }
    public bool UsesGraphicsState { get; init; }
}

/// <summary>
/// MarkupDraft 하나의 외형(appearance stream)을 PDF 그리기 명령으로 만든다. 외부 뷰어(Acrobat·Chrome·Bluebeam)는
/// 이 외형을 그대로 그리므로 이 앱이 보는 모양과 같다. 투명도·혼합은 /GS 그래픽 상태로 넣는다.
/// </summary>
internal static class AppearanceBuilder
{
    public const string GraphicsStateName = "/GS";

    /// <summary>화살촉 길이(선 굵기 배수)와 반각.</summary>
    private const double ArrowLengthFactor = 8, ArrowMinLength = 6, ArrowHalfWidthFactor = 0.5;

    private static readonly double Kappa = 4 * (Math.Sqrt(2) - 1) / 3;

    public static Appearance Build(MarkupDraft draft, PageGeometry g)
    {
        var s = draft.Style;
        var gs = s.Opacity < 1 || draft is HighlightDraft;
        var sb = new StringBuilder("q\n");
        if (gs) sb.Append(GraphicsStateName).Append(" gs\n");

        switch (draft)
        {
            case TextBoxDraft d: return FreeText(sb, g, d.Rect, d.Text, s, null, gs);
            case CalloutDraft d: return FreeText(sb, g, d.TextRect, d.Text, s, d, gs);
            case LineDraft d: return Line(sb, g, d, s, gs);
            case RectangleDraft d: return Square(sb, g, d.Rect, s, gs);
            case EllipseDraft d: return Circle(sb, g, d.Rect, s, gs);
            case CloudDraft d: return Polygon(sb, g, d, s, gs);
            case InkDraft d: return Ink(sb, g, d, s, gs);
            case HighlightDraft d: return Highlight(sb, g, d.Rect, s);
            default: throw new NotSupportedException($"Unsupported markup draft: {draft.GetType().Name}");
        }
    }

    /// <summary>사각형 주석의 /Rect 안쪽 여백(/RD): 선 굵기의 절반.</summary>
    public static double RectPadding(MarkupStyle s) => s.LineWidth / 2;

    /// <summary>구름 호의 반지름 (pt). 볼록함(intensity) 1·2에 맞춘다.</summary>
    public static double CloudRadius(double intensity) => 4 + 3 * Math.Max(1, intensity);

    // ---- 도형 ----

    private static Appearance Line(StringBuilder sb, PageGeometry g, LineDraft d, MarkupStyle s, bool gs)
    {
        var a = g.ToPdf(d.Start);
        var b = g.ToPdf(d.End);
        var lw = s.LineWidth;
        Stroke(sb, s.StrokeColor, lw);
        var points = new List<(double X, double Y)> { a, b };
        if (d.ArrowAtEnd && Distance(a, b) > 0.01)
        {
            var (tip, p1, p2, baseCenter) = ArrowHead(a, b, lw);
            sb.Append(Inv($"{N(a.X)} {N(a.Y)} m {N(baseCenter.X)} {N(baseCenter.Y)} l S\n"));
            Fill(sb, s.StrokeColor);
            sb.Append(Inv($"{N(tip.X)} {N(tip.Y)} m {N(p1.X)} {N(p1.Y)} l {N(p2.X)} {N(p2.Y)} l h B\n"));
            points.Add(p1); points.Add(p2);
        }
        else
        {
            sb.Append(Inv($"{N(a.X)} {N(a.Y)} m {N(b.X)} {N(b.Y)} l S\n"));
        }
        sb.Append("Q\n");
        return new Appearance { Content = sb.ToString(), BBox = Bounds(points, lw / 2 + 1), UsesGraphicsState = gs };
    }

    private static Appearance Square(StringBuilder sb, PageGeometry g, PageRect rect, MarkupStyle s, bool gs)
    {
        var r = g.ToPdfRect(rect);
        var lw = s.LineWidth;
        var op = PaintOperator(sb, s);
        sb.Append(Inv($"{N(r.X0)} {N(r.Y0)} {N(r.X1 - r.X0)} {N(r.Y1 - r.Y0)} re {op}\n"));
        sb.Append("Q\n");
        return new Appearance { Content = sb.ToString(), BBox = Inflate(r, lw / 2), UsesGraphicsState = gs };
    }

    private static Appearance Circle(StringBuilder sb, PageGeometry g, PageRect rect, MarkupStyle s, bool gs)
    {
        var r = g.ToPdfRect(rect);
        var lw = s.LineWidth;
        var op = PaintOperator(sb, s);
        double cx = (r.X0 + r.X1) / 2, cy = (r.Y0 + r.Y1) / 2, rx = (r.X1 - r.X0) / 2, ry = (r.Y1 - r.Y0) / 2;
        double kx = rx * Kappa, ky = ry * Kappa;
        sb.Append(Inv($"{N(cx + rx)} {N(cy)} m\n"));
        sb.Append(Inv($"{N(cx + rx)} {N(cy + ky)} {N(cx + kx)} {N(cy + ry)} {N(cx)} {N(cy + ry)} c\n"));
        sb.Append(Inv($"{N(cx - kx)} {N(cy + ry)} {N(cx - rx)} {N(cy + ky)} {N(cx - rx)} {N(cy)} c\n"));
        sb.Append(Inv($"{N(cx - rx)} {N(cy - ky)} {N(cx - kx)} {N(cy - ry)} {N(cx)} {N(cy - ry)} c\n"));
        sb.Append(Inv($"{N(cx + kx)} {N(cy - ry)} {N(cx + rx)} {N(cy - ky)} {N(cx + rx)} {N(cy)} c\n"));
        sb.Append(op).Append("\nQ\n");
        return new Appearance { Content = sb.ToString(), BBox = Inflate(r, lw / 2), UsesGraphicsState = gs };
    }

    private static Appearance Polygon(StringBuilder sb, PageGeometry g, CloudDraft d, MarkupStyle s, bool gs)
    {
        var pts = d.Vertices.Select(g.ToPdf).ToList();
        var lw = s.LineWidth;
        var op = PaintOperator(sb, s);
        sb.Append("1 j\n");
        double bulge = 0;
        if (d.Intensity > 0 && pts.Count >= 3)
        {
            bulge = CloudRadius(d.Intensity);
            AppendCloudPath(sb, pts, bulge);
        }
        else
        {
            for (int i = 0; i < pts.Count; i++)
                sb.Append(Inv($"{N(pts[i].X)} {N(pts[i].Y)} ")).Append(i == 0 ? "m\n" : "l\n");
            sb.Append("h\n");
        }
        sb.Append(op).Append("\nQ\n");
        return new Appearance { Content = sb.ToString(), BBox = Bounds(pts, bulge + lw / 2), UsesGraphicsState = gs };
    }

    /// <summary>
    /// 구름 테두리: 변마다 바깥쪽으로 볼록한 반원을 이어 그린다. 반원 반지름은 radius에 가깝게 변을 균등하게 나눈다.
    /// </summary>
    private static void AppendCloudPath(StringBuilder sb, List<(double X, double Y)> pts, double radius)
    {
        // 다각형 방향(부호 있는 넓이)으로 바깥쪽 법선 방향을 정한다
        double area = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var (x0, y0) = pts[i];
            var (x1, y1) = pts[(i + 1) % pts.Count];
            area += x0 * y1 - x1 * y0;
        }
        var ccw = area > 0;

        sb.Append(Inv($"{N(pts[0].X)} {N(pts[0].Y)} m\n"));
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            var len = Distance(a, b);
            if (len < 0.01) continue;
            var n = Math.Max(1, (int)Math.Round(len / (2 * radius)));
            var chord = len / n;
            var r = chord / 2;
            double tx = (b.X - a.X) / len, ty = (b.Y - a.Y) / len;
            // 바깥쪽 법선: 반시계 방향 다각형이면 진행 방향의 오른쪽
            double nx = ccw ? ty : -ty, ny = ccw ? -tx : tx;
            var outward = Math.Atan2(ny, nx);
            for (int k = 0; k < n; k++)
            {
                double cx = a.X + tx * chord * (k + 0.5), cy = a.Y + ty * chord * (k + 0.5);
                // 시작점(a쪽)은 바깥 방향 + 90°, 끝점(b쪽)은 바깥 방향 − 90°; 가운데(바깥 방향)를 지나는 반원
                // 법선이 진행 방향의 왼쪽(반시계)이면 -t(시작)는 outward+90°, +t(끝)는 outward-90°에 있다
                var normalLeft = IsCcwFrom(tx, ty, nx, ny);
                var start = outward + (normalLeft ? Math.PI / 2 : -Math.PI / 2);
                var end = outward + (normalLeft ? -Math.PI / 2 : Math.PI / 2);
                AppendArc(sb, cx, cy, r, start, end);
            }
        }
        sb.Append("h\n");
    }

    /// <summary>진행 방향 t에서 법선 n으로 가는 회전이 반시계인지 (반원을 돌릴 방향을 정한다).</summary>
    private static bool IsCcwFrom(double tx, double ty, double nx, double ny) => tx * ny - ty * nx > 0;

    /// <summary>원호를 90° 이하 조각의 베지어 곡선으로 이어 그린다 (현재 점은 호의 시작점이어야 한다).</summary>
    private static void AppendArc(StringBuilder sb, double cx, double cy, double r, double start, double end)
    {
        var sweep = end - start;
        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) - 1e-9));
        var step = sweep / pieces;
        var k = 4.0 / 3 * Math.Tan(step / 4);
        for (int i = 0; i < pieces; i++)
        {
            double a0 = start + step * i, a1 = a0 + step;
            double c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
            double p1x = cx + r * (c0 - k * s0), p1y = cy + r * (s0 + k * c0);
            double p2x = cx + r * (c1 + k * s1), p2y = cy + r * (s1 - k * c1);
            sb.Append(Inv($"{N(p1x)} {N(p1y)} {N(p2x)} {N(p2y)} {N(cx + r * c1)} {N(cy + r * s1)} c\n"));
        }
    }

    private static Appearance Ink(StringBuilder sb, PageGeometry g, InkDraft d, MarkupStyle s, bool gs)
    {
        var lw = s.LineWidth;
        Stroke(sb, s.StrokeColor, lw);
        sb.Append("1 J 1 j\n");
        var all = new List<(double X, double Y)>();
        foreach (var stroke in d.Strokes)
        {
            var pts = stroke.Select(g.ToPdf).ToList();
            if (pts.Count == 0) continue;
            all.AddRange(pts);
            for (int i = 0; i < pts.Count; i++)
                sb.Append(Inv($"{N(pts[i].X)} {N(pts[i].Y)} ")).Append(i == 0 ? "m\n" : "l\n");
            if (pts.Count == 1) sb.Append(Inv($"{N(pts[0].X)} {N(pts[0].Y)} l\n")); // 점 하나도 둥근 끝으로 보이게
            sb.Append("S\n");
        }
        sb.Append("Q\n");
        return new Appearance { Content = sb.ToString(), BBox = Bounds(all, lw / 2 + 1), UsesGraphicsState = gs };
    }

    private static Appearance Highlight(StringBuilder sb, PageGeometry g, PageRect rect, MarkupStyle s)
    {
        var r = g.ToPdfRect(rect);
        Fill(sb, s.StrokeColor);
        sb.Append(Inv($"{N(r.X0)} {N(r.Y0)} {N(r.X1 - r.X0)} {N(r.Y1 - r.Y0)} re f\n"));
        sb.Append("Q\n");
        return new Appearance { Content = sb.ToString(), BBox = r, UsesGraphicsState = true };
    }

    // ---- 글상자 ----

    private static Appearance FreeText(StringBuilder sb, PageGeometry g, PageRect rect, string text, MarkupStyle s, CalloutDraft? callout, bool gs)
    {
        var r = g.ToPdfRect(rect);
        var lw = s.LineWidth;
        var points = new List<(double X, double Y)> { (r.X0, r.Y0), (r.X1, r.Y1) };

        if (s.FillColor is { } fill)
        {
            Fill(sb, fill);
            sb.Append(Inv($"{N(r.X0)} {N(r.Y0)} {N(r.X1 - r.X0)} {N(r.Y1 - r.Y0)} re f\n"));
        }
        if (lw > 0)
        {
            Stroke(sb, s.TextColor, lw);
            sb.Append(Inv($"{N(r.X0 + lw / 2)} {N(r.Y0 + lw / 2)} {N(r.X1 - r.X0 - lw)} {N(r.Y1 - r.Y0 - lw)} re S\n"));
        }
        if (callout != null)
        {
            var cl = CalloutPoints(g, callout).Select(g.ToPdf).ToList();
            Stroke(sb, s.TextColor, Math.Max(lw, 0.5));
            sb.Append("1 J 1 j\n");
            for (int i = cl.Count - 1; i >= 0; i--)
                sb.Append(Inv($"{N(cl[i].X)} {N(cl[i].Y)} ")).Append(i == cl.Count - 1 ? "m\n" : "l\n");
            sb.Append("S\n");
            if (cl.Count >= 2 && Distance(cl[1], cl[0]) > 0.01)
            {
                var (tip, p1, p2, _) = ArrowHead(cl[1], cl[0], Math.Max(lw, 0.5));
                sb.Append(Inv($"{N(p1.X)} {N(p1.Y)} m {N(tip.X)} {N(tip.Y)} l {N(p2.X)} {N(p2.Y)} l S\n"));
                points.Add(p1); points.Add(p2);
            }
            points.AddRange(cl);
        }

        // 글자: 화면에서 똑바로 선 좌표계(cm)로 바꾸고 글상자 안으로 잘라 쓴다
        var usesHelv = false; var usesKorean = false;
        var lines = TextLayout.Layout(text, s.FontSize, rect.Width - 2 * Margin(lw));
        if (lines.Any(l => l.Count > 0))
        {
            var (a, b, c, d, e, f) = g.UprightFrame(rect);
            sb.Append(Inv($"q {N(r.X0)} {N(r.Y0)} {N(r.X1 - r.X0)} {N(r.Y1 - r.Y0)} re W n\n"));
            sb.Append(Inv($"{N(a, "0.####")} {N(b, "0.####")} {N(c, "0.####")} {N(d, "0.####")} {N(e)} {N(f)} cm\n"));
            Fill(sb, s.TextColor);
            sb.Append("BT\n");
            var size = s.FontSize;
            var margin = Margin(lw);
            var baseline = rect.Height - margin - size * 0.93;
            foreach (var line in lines)
            {
                sb.Append(Inv($"1 0 0 1 {N(margin)} {N(baseline)} Tm\n"));
                foreach (var run in line)
                {
                    if (run.Latin)
                    {
                        usesHelv = true;
                        sb.Append(Inv($"{StandardFonts.HelveticaName} {N(size)} Tf ({EscapeLiteral(run.Text)}) Tj\n"));
                    }
                    else
                    {
                        usesKorean = true;
                        sb.Append(Inv($"{StandardFonts.KoreanName} {N(size)} Tf <{KoreanCidFont.Hex(run.Text)}> Tj\n"));
                    }
                }
                baseline -= size * TextLayout.LineHeightFactor;
            }
            sb.Append("ET\nQ\n");
        }
        sb.Append("Q\n");
        return new Appearance
        {
            Content = sb.ToString(),
            BBox = Bounds(points, callout != null ? Math.Max(lw, 0.5) / 2 + 1 : 0),
            UsesHelvetica = usesHelv,
            UsesKorean = usesKorean,
            UsesGraphicsState = gs,
        };
    }

    public static double Margin(double lineWidth) => lineWidth + 2;

    /// <summary>지시선 점 (보이는 좌표): 화살촉 → 꺾임(선택) → 글상자에 닿는 점. PDF 규격의 /CL 순서.</summary>
    public static List<PagePoint> CalloutPoints(PageGeometry g, CalloutDraft d)
    {
        var r = d.TextRect;
        var reference = d.Knee ?? d.ArrowTip;
        var attach = new PagePoint(Math.Clamp(reference.X, r.X0, r.X1), Math.Clamp(reference.Y, r.Y0, r.Y1));
        var pts = new List<PagePoint> { d.ArrowTip };
        if (d.Knee is { } knee) pts.Add(knee);
        pts.Add(attach);
        return pts;
    }

    // ---- 공통 ----

    /// <summary>from → to 방향의 화살촉: 끝점(tip), 양 옆 점, 그리고 선이 끝나야 할 밑변 가운데.</summary>
    private static ((double X, double Y) Tip, (double X, double Y) P1, (double X, double Y) P2, (double X, double Y) Base) ArrowHead(
        (double X, double Y) from, (double X, double Y) to, double lw)
    {
        var len = Distance(from, to);
        double ux = (to.X - from.X) / len, uy = (to.Y - from.Y) / len;
        var size = Math.Max(ArrowMinLength, ArrowLengthFactor * lw);
        var half = size * ArrowHalfWidthFactor;
        var bx = to.X - ux * size; var by = to.Y - uy * size;
        return (to, (bx - uy * half, by + ux * half), (bx + uy * half, by - ux * half), (bx, by));
    }

    private static string PaintOperator(StringBuilder sb, MarkupStyle s)
    {
        var hasStroke = s.LineWidth > 0;
        if (hasStroke) Stroke(sb, s.StrokeColor, s.LineWidth);
        if (s.FillColor is { } fill) Fill(sb, fill);
        return (hasStroke, s.FillColor != null) switch
        {
            (true, true) => "B",
            (true, false) => "S",
            (false, true) => "f",
            _ => "n",
        };
    }

    private static void Stroke(StringBuilder sb, RgbColor c, double lw) =>
        sb.Append(Inv($"{N(c.R)} {N(c.G)} {N(c.B)} RG {N(lw)} w\n"));

    private static void Fill(StringBuilder sb, RgbColor c) =>
        sb.Append(Inv($"{N(c.R)} {N(c.G)} {N(c.B)} rg\n"));

    private static (double X0, double Y0, double X1, double Y1) Bounds(IEnumerable<(double X, double Y)> pts, double pad)
    {
        var list = pts.ToList();
        if (list.Count == 0) return (0, 0, 0, 0);
        return Inflate((list.Min(p => p.X), list.Min(p => p.Y), list.Max(p => p.X), list.Max(p => p.Y)), pad);
    }

    private static (double X0, double Y0, double X1, double Y1) Inflate((double X0, double Y0, double X1, double Y1) r, double pad) =>
        (r.X0 - pad, r.Y0 - pad, r.X1 + pad, r.Y1 + pad);

    private static double Distance((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static string N(double v) => Num(v);
    private static string N(double v, string format) => Num(v, format);
    private static string Inv(FormattableString s) => Invariant(s);
}
