using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Core.Editing;

/// <summary>선택 상자에서 잡을 수 있는 손잡이.</summary>
public enum HandleKind
{
    None,
    TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left,
    /// <summary>선·화살표의 시작점.</summary>
    LineStart,
    /// <summary>선·화살표의 끝점.</summary>
    LineEnd,
    /// <summary>Callout 화살촉. 옮겨도 글상자와 문구는 그대로다 (기획서 7장).</summary>
    CalloutTip,
}

/// <summary>
/// 마크업 도형 계산 (이동, 크기 조절, 클릭 판정, 손잡이). 모두 보이는 페이지 좌표(pt) 기준의 순수 함수다.
/// </summary>
public static class DraftGeometry
{
    /// <summary>도형 전체를 감싸는 영역 (Callout 화살촉, 펜 획 포함).</summary>
    public static PageRect Bounds(MarkupDraft d) => d switch
    {
        TextBoxDraft t => t.Rect,
        CalloutDraft c => Union(c.TextRect, PageRect.FromPoints(CalloutPoints(c))),
        LineDraft l => PageRect.FromPoints([l.Start, l.End]),
        RectangleDraft r => r.Rect,
        EllipseDraft e => e.Rect,
        HighlightDraft h => h.Rect,
        CloudDraft c => PageRect.FromPoints(c.Vertices),
        InkDraft i => PageRect.FromPoints(i.Strokes.SelectMany(s => s)),
        _ => throw new NotSupportedException(d.GetType().Name),
    };

    /// <summary>Callout 지시선 점들: 화살촉 → (꺾임점) → 글상자에 닿는 점.</summary>
    public static IReadOnlyList<PagePoint> CalloutPoints(CalloutDraft c)
    {
        var r = c.TextRect;
        var reference = c.Knee ?? c.ArrowTip;
        var attach = new PagePoint(Math.Clamp(reference.X, r.X0, r.X1), Math.Clamp(reference.Y, r.Y0, r.Y1));
        return c.Knee is { } k ? [c.ArrowTip, k, attach] : [c.ArrowTip, attach];
    }

    public static MarkupDraft Translate(MarkupDraft d, double dx, double dy)
    {
        PagePoint P(PagePoint p) => new(p.X + dx, p.Y + dy);
        PageRect R(PageRect r) => new(r.X0 + dx, r.Y0 + dy, r.X1 + dx, r.Y1 + dy);
        return d switch
        {
            TextBoxDraft t => t with { Rect = R(t.Rect) },
            CalloutDraft c => c with { TextRect = R(c.TextRect), ArrowTip = P(c.ArrowTip), Knee = c.Knee is { } k ? P(k) : null },
            LineDraft l => l with { Start = P(l.Start), End = P(l.End) },
            RectangleDraft r => r with { Rect = R(r.Rect) },
            EllipseDraft e => e with { Rect = R(e.Rect) },
            HighlightDraft h => h with { Rect = R(h.Rect) },
            CloudDraft c => c with { Vertices = c.Vertices.Select(P).ToList() },
            InkDraft i => i with { Strokes = i.Strokes.Select(s => (IReadOnlyList<PagePoint>)s.Select(P).ToList()).ToList() },
            _ => throw new NotSupportedException(d.GetType().Name),
        };
    }

    /// <summary>
    /// 전체 영역을 newBounds로 늘리거나 줄인다. 모든 점을 같은 비율로 옮긴다.
    /// 글상자는 글자 크기를 바꾸지 않고 상자만 바뀐다.
    /// </summary>
    public static MarkupDraft Resize(MarkupDraft d, PageRect newBounds)
    {
        var old = Bounds(d);
        double sx = old.Width > 1e-6 ? newBounds.Width / old.Width : 1;
        double sy = old.Height > 1e-6 ? newBounds.Height / old.Height : 1;
        PagePoint P(PagePoint p) => new(newBounds.X0 + (p.X - old.X0) * sx, newBounds.Y0 + (p.Y - old.Y0) * sy);
        PageRect R(PageRect r) => PageRect.FromPoints([P(new(r.X0, r.Y0)), P(new(r.X1, r.Y1))]);
        return d switch
        {
            TextBoxDraft t => t with { Rect = R(t.Rect) },
            CalloutDraft c => c with { TextRect = R(c.TextRect), ArrowTip = P(c.ArrowTip), Knee = c.Knee is { } k ? P(k) : null },
            LineDraft l => l with { Start = P(l.Start), End = P(l.End) },
            RectangleDraft r => r with { Rect = R(r.Rect) },
            EllipseDraft e => e with { Rect = R(e.Rect) },
            HighlightDraft h => h with { Rect = R(h.Rect) },
            CloudDraft c => c with { Vertices = c.Vertices.Select(P).ToList() },
            InkDraft i => i with { Strokes = i.Strokes.Select(s => (IReadOnlyList<PagePoint>)s.Select(P).ToList()).ToList() },
            _ => throw new NotSupportedException(d.GetType().Name),
        };
    }

    /// <summary>점이 도형 위(또는 tolerance 이내)에 있는지. 선 종류는 선 가까이, 면 종류는 안쪽까지.</summary>
    public static bool HitTest(MarkupDraft d, PagePoint p, double tolerance)
    {
        switch (d)
        {
            case LineDraft l:
                return DistanceToSegment(p, l.Start, l.End) <= tolerance + l.Style.LineWidth / 2;
            case InkDraft i:
                foreach (var s in i.Strokes)
                {
                    if (s.Count == 1 && Distance(p, s[0]) <= tolerance) return true;
                    for (int k = 1; k < s.Count; k++)
                        if (DistanceToSegment(p, s[k - 1], s[k]) <= tolerance + i.Style.LineWidth / 2) return true;
                }
                return false;
            case CalloutDraft c:
                if (Contains(c.TextRect.Inflate(tolerance), p)) return true;
                var pts = CalloutPoints(c);
                for (int k = 1; k < pts.Count; k++)
                    if (DistanceToSegment(p, pts[k - 1], pts[k]) <= tolerance) return true;
                return false;
            default:
                return Contains(Bounds(d).Inflate(tolerance), p);
        }
    }

    /// <summary>선택된 도형에서 잡을 수 있는 손잡이 위치들.</summary>
    public static IReadOnlyList<(HandleKind Kind, PagePoint Point)> Handles(MarkupDraft d)
    {
        if (d is LineDraft l) return [(HandleKind.LineStart, l.Start), (HandleKind.LineEnd, l.End)];
        var b = d is CalloutDraft co ? co.TextRect : Bounds(d);
        var list = new List<(HandleKind, PagePoint)>
        {
            (HandleKind.TopLeft, new(b.X0, b.Y0)), (HandleKind.Top, new(b.Center.X, b.Y0)),
            (HandleKind.TopRight, new(b.X1, b.Y0)), (HandleKind.Right, new(b.X1, b.Center.Y)),
            (HandleKind.BottomRight, new(b.X1, b.Y1)), (HandleKind.Bottom, new(b.Center.X, b.Y1)),
            (HandleKind.BottomLeft, new(b.X0, b.Y1)), (HandleKind.Left, new(b.X0, b.Center.Y)),
        };
        if (d is CalloutDraft c) list.Add((HandleKind.CalloutTip, c.ArrowTip));
        return list;
    }

    public static HandleKind HandleAt(MarkupDraft d, PagePoint p, double tolerance)
    {
        foreach (var (kind, point) in Handles(d))
            if (Math.Abs(point.X - p.X) <= tolerance && Math.Abs(point.Y - p.Y) <= tolerance) return kind;
        return HandleKind.None;
    }

    /// <summary>손잡이를 p로 끌었을 때의 새 도형. 최소 크기는 minSize(pt).</summary>
    public static MarkupDraft DragHandle(MarkupDraft d, HandleKind handle, PagePoint p, double minSize = 4)
    {
        switch (handle)
        {
            case HandleKind.LineStart when d is LineDraft l: return l with { Start = p };
            case HandleKind.LineEnd when d is LineDraft l: return l with { End = p };
            case HandleKind.CalloutTip when d is CalloutDraft c: return c with { ArrowTip = p };
            case HandleKind.None: return d;
        }

        var b = d is CalloutDraft co ? co.TextRect : Bounds(d);
        double x0 = b.X0, y0 = b.Y0, x1 = b.X1, y1 = b.Y1;
        if (handle is HandleKind.TopLeft or HandleKind.Left or HandleKind.BottomLeft) x0 = Math.Min(p.X, x1 - minSize);
        if (handle is HandleKind.TopRight or HandleKind.Right or HandleKind.BottomRight) x1 = Math.Max(p.X, x0 + minSize);
        if (handle is HandleKind.TopLeft or HandleKind.Top or HandleKind.TopRight) y0 = Math.Min(p.Y, y1 - minSize);
        if (handle is HandleKind.BottomLeft or HandleKind.Bottom or HandleKind.BottomRight) y1 = Math.Max(p.Y, y0 + minSize);
        var nb = new PageRect(x0, y0, x1, y1);
        // Callout은 글상자만 바뀌고 화살촉은 제자리에 둔다.
        return d is CalloutDraft c2 ? c2 with { TextRect = nb } : Resize(d, nb);
    }

    public static bool Intersects(PageRect a, PageRect b) => a.X0 <= b.X1 && b.X0 <= a.X1 && a.Y0 <= b.Y1 && b.Y0 <= a.Y1;

    public static PageRect Union(PageRect a, PageRect b) =>
        new(Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1));

    public static bool Contains(PageRect r, PagePoint p) => p.X >= r.X0 && p.X <= r.X1 && p.Y >= r.Y0 && p.Y <= r.Y1;

    public static double Distance(PagePoint a, PagePoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public static double DistanceToSegment(PagePoint p, PagePoint a, PagePoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-12) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        return Distance(p, new PagePoint(a.X + t * dx, a.Y + t * dy));
    }

    /// <summary>Shift를 누르고 그릴 때: 시작점 기준 45° 단위로 끝점을 맞춘다.</summary>
    public static PagePoint SnapAngle(PagePoint start, PagePoint end)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var angle = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
        return new PagePoint(start.X + len * Math.Cos(angle), start.Y + len * Math.Sin(angle));
    }

    /// <summary>Shift를 누르고 사각형을 그릴 때: 정사각형으로 맞춘다.</summary>
    public static PagePoint SnapSquare(PagePoint start, PagePoint end)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y;
        var size = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new PagePoint(start.X + Math.Sign(dx == 0 ? 1 : dx) * size, start.Y + Math.Sign(dy == 0 ? 1 : dy) * size);
    }
}
