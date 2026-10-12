using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DrawingReview.App.Core.Editing;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Controls;

/// <summary>
/// 그리는 중·옮기는 중인 마크업 미리보기를 WPF로 그린다.
/// 저장된 마크업의 실제 모습은 PDF 엔진 렌더링이 보여주고, 이것은 마우스를 움직이는 동안의 임시 그림이다.
/// </summary>
public static class DraftRenderer
{
    public static void Draw(DrawingContext dc, MarkupDraft d, Func<PagePoint, Point> map, double zoom, double dpi, double alpha = 1)
    {
        var s = d.Style;
        var stroke = Brush(s.StrokeColor, s.Opacity * alpha);
        var pen = new Pen(stroke, Math.Max(1, s.LineWidth * zoom)) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        var fill = s.FillColor is { } f ? Brush(f, s.Opacity * alpha) : null;
        Rect R(PageRect r) => new(map(new PagePoint(r.X0, r.Y0)), map(new PagePoint(r.X1, r.Y1)));

        switch (d)
        {
            case RectangleDraft r:
                dc.DrawRectangle(fill, pen, R(r.Rect));
                break;
            case EllipseDraft e:
                var er = R(e.Rect);
                dc.DrawEllipse(fill, pen, new Point(er.X + er.Width / 2, er.Y + er.Height / 2), er.Width / 2, er.Height / 2);
                break;
            case HighlightDraft h:
                dc.DrawRectangle(Brush(s.StrokeColor, Math.Max(0.25, s.Opacity) * alpha), null, R(h.Rect));
                break;
            case LineDraft l:
                var a = map(l.Start);
                var b = map(l.End);
                dc.DrawLine(pen, a, b);
                if (l.ArrowAtEnd) DrawArrowHead(dc, a, b, pen, stroke, closed: true);
                break;
            case InkDraft i:
                foreach (var st in i.Strokes) dc.DrawGeometry(null, pen, Polyline(st.Select(map).ToList(), closed: false));
                break;
            case CloudDraft c:
                var pts = c.Vertices.Select(map).ToList();
                dc.DrawGeometry(fill, pen, c.Intensity > 0 ? Cloud(pts, Math.Max(6, 9 * zoom * c.Intensity)) : Polyline(pts, closed: true));
                break;
            case TextBoxDraft t:
                DrawTextBox(dc, R(t.Rect), t.Text, s, fill, pen, zoom, dpi, alpha);
                break;
            case CalloutDraft co:
                var leader = DraftGeometry.CalloutPoints(co).Select(map).ToList();
                dc.DrawGeometry(null, pen, Polyline(leader, closed: false));
                DrawArrowHead(dc, leader[1], leader[0], pen, stroke, closed: false);
                DrawTextBox(dc, R(co.TextRect), co.Text, s, fill ?? Brushes.White, pen, zoom, dpi, alpha);
                break;
        }
    }

    private static void DrawTextBox(DrawingContext dc, Rect box, string text, MarkupStyle s, Brush? fill, Pen pen, double zoom, double dpi, double alpha)
    {
        dc.DrawRectangle(fill, pen, box);
        if (string.IsNullOrEmpty(text) || box.Width < 4) return;
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"),
            Math.Max(4, s.FontSize * zoom), Brush(s.TextColor, s.Opacity * alpha), dpi)
        {
            MaxTextWidth = Math.Max(1, box.Width - 4 * zoom),
            MaxTextHeight = Math.Max(1, box.Height - 2 * zoom),
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(ft, new Point(box.X + 2 * zoom, box.Y + 1 * zoom));
    }

    private static void DrawArrowHead(DrawingContext dc, Point from, Point tip, Pen pen, Brush brush, bool closed)
    {
        var v = tip - from;
        if (v.Length < 1e-6) return;
        v.Normalize();
        var size = Math.Max(9, pen.Thickness * 4);
        var n = new Vector(-v.Y, v.X);
        var p1 = tip - v * size + n * size * 0.45;
        var p2 = tip - v * size - n * size * 0.45;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(p1, closed, closed);
            ctx.LineTo(tip, true, true);
            ctx.LineTo(p2, true, true);
        }
        g.Freeze();
        dc.DrawGeometry(closed ? brush : null, pen, g);
    }

    private static Geometry Polyline(IReadOnlyList<Point> pts, bool closed)
    {
        var g = new StreamGeometry();
        if (pts.Count == 0) return g;
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(pts[0], false, closed);
            ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
        }
        g.Freeze();
        return g;
    }

    /// <summary>다각형 변을 따라 바깥으로 볼록한 반원을 이어 붙여 구름 모양을 만든다 (미리보기용 근사).</summary>
    private static Geometry Cloud(IReadOnlyList<Point> pts, double scallop)
    {
        var g = new StreamGeometry();
        if (pts.Count < 3) return g;
        // 화면 좌표(아래로 +y)에서 넓이 부호로 도는 방향을 알아 바깥쪽으로 볼록하게 한다.
        double area = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            area += p.X * q.Y - q.X * p.Y;
        }
        var sweep = area > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(pts[0], false, true);
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                var len = (b - a).Length;
                var n = Math.Max(1, (int)Math.Round(len / scallop));
                for (int k = 1; k <= n; k++)
                {
                    var p = a + (b - a) * k / n;
                    var r = len / n / 2;
                    ctx.ArcTo(p, new Size(r, r), 0, false, sweep, true, true);
                }
            }
        }
        g.Freeze();
        return g;
    }

    public static SolidColorBrush Brush(RgbColor c, double opacity)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(opacity * 255, 0, 255),
            (byte)(c.R * 255), (byte)(c.G * 255), (byte)(c.B * 255)));
        b.Freeze();
        return b;
    }
}
