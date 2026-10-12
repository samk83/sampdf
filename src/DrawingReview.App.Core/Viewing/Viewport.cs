using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Core.Viewing;

/// <summary>화면 좌표 (WPF의 장치 독립 픽셀, 1/96 inch).</summary>
public readonly record struct ScreenPoint(double X, double Y);

public readonly record struct ViewSize(double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>
/// 한 페이지를 화면에 보여주는 확대율과 위치. 화면 표시(WPF)와 무관한 순수 계산이라 테스트할 수 있다.
/// Zoom = 화면 픽셀(DIP) / PDF 포인트. 100% 확대 = 실제 크기 = 96/72.
/// Offset = 페이지 왼쪽 위 모서리가 화면에서 놓이는 위치.
/// </summary>
public sealed class Viewport
{
    public const double ActualSizeZoom = 96.0 / 72.0;
    public const double MinZoom = 0.01;
    public const double MaxZoom = 64;
    private const double FitMargin = 16;
    /// <summary>이동해도 페이지가 이만큼(DIP)은 화면에 남는다. 도면을 놓치지 않게 한다.</summary>
    private const double KeepVisible = 80;

    public double Zoom { get; private set; } = 1;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public ViewSize View { get; private set; }
    public double PageWidth { get; private set; }
    public double PageHeight { get; private set; }

    /// <summary>확대율이나 위치가 바뀌면 발생. 화면 갱신·고해상도 렌더링 요청의 계기.</summary>
    public event EventHandler? Changed;

    /// <summary>실제 크기 대비 백분율 (상태 표시줄용).</summary>
    public double ZoomPercent => Zoom / ActualSizeZoom * 100;

    public void SetView(ViewSize view)
    {
        if (view == View) return;
        var hadView = !View.IsEmpty;
        var center = hadView ? ScreenToPage(new ScreenPoint(View.Width / 2, View.Height / 2)) : default;
        View = view;
        if (hadView && PageWidth > 0)
            CenterOn(center);
        else
            FitPage();
    }

    /// <summary>다른 페이지(크기)로 바꾼다. fit이 true면 화면에 맞추고, 아니면 확대율을 유지한 채 왼쪽 위를 보여준다.</summary>
    public void SetPage(double width, double height, bool fit = true)
    {
        PageWidth = width;
        PageHeight = height;
        if (fit) FitPage();
        else { OffsetX = FitMargin; OffsetY = FitMargin; Clamp(); Raise(); }
    }

    public ScreenPoint PageToScreen(PagePoint p) => new(OffsetX + p.X * Zoom, OffsetY + p.Y * Zoom);

    public PagePoint ScreenToPage(ScreenPoint s) => new((s.X - OffsetX) / Zoom, (s.Y - OffsetY) / Zoom);

    /// <summary>화면 한 점을 고정한 채 확대·축소 (마우스 휠).</summary>
    public void ZoomAt(ScreenPoint anchor, double factor)
    {
        var before = ScreenToPage(anchor);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        OffsetX = anchor.X - before.X * Zoom;
        OffsetY = anchor.Y - before.Y * Zoom;
        Clamp();
        Raise();
    }

    /// <summary>화면 중앙을 기준으로 확대율을 지정한다.</summary>
    public void SetZoom(double zoom) =>
        ZoomAt(new ScreenPoint(View.Width / 2, View.Height / 2), zoom / Zoom);

    public void SetZoomPercent(double percent) => SetZoom(percent / 100 * ActualSizeZoom);

    public void PanBy(double dx, double dy)
    {
        OffsetX += dx;
        OffsetY += dy;
        Clamp();
        Raise();
    }

    public void FitPage()
    {
        if (View.IsEmpty || PageWidth <= 0) return;
        Zoom = Math.Clamp(Math.Min((View.Width - 2 * FitMargin) / PageWidth, (View.Height - 2 * FitMargin) / PageHeight), MinZoom, MaxZoom);
        OffsetX = (View.Width - PageWidth * Zoom) / 2;
        OffsetY = (View.Height - PageHeight * Zoom) / 2;
        Raise();
    }

    public void FitWidth()
    {
        if (View.IsEmpty || PageWidth <= 0) return;
        Zoom = Math.Clamp((View.Width - 2 * FitMargin) / PageWidth, MinZoom, MaxZoom);
        OffsetX = FitMargin;
        OffsetY = FitMargin;
        Raise();
    }

    /// <summary>페이지의 한 점을 화면 중앙에 오게 한다 (검토 목록에서 주석으로 이동할 때).</summary>
    public void CenterOn(PagePoint p)
    {
        OffsetX = View.Width / 2 - p.X * Zoom;
        OffsetY = View.Height / 2 - p.Y * Zoom;
        Clamp();
        Raise();
    }

    /// <summary>페이지 영역이 화면에 들어오도록 확대하고 가운데에 둔다. 너무 크게 확대하지 않도록 maxZoom을 둔다.</summary>
    public void ZoomToRegion(PageRect region, double maxZoom = 4)
    {
        if (View.IsEmpty) return;
        var w = Math.Max(region.Width, 1);
        var h = Math.Max(region.Height, 1);
        Zoom = Math.Clamp(Math.Min(View.Width * 0.6 / w, View.Height * 0.6 / h), MinZoom, Math.Min(maxZoom, MaxZoom));
        CenterOn(region.Center);
    }

    /// <summary>현재 화면에 보이는 페이지 영역 (페이지 밖은 잘라냄). 보이는 부분이 없으면 null.</summary>
    public PageRect? VisiblePageRegion()
    {
        if (View.IsEmpty || PageWidth <= 0) return null;
        var tl = ScreenToPage(new ScreenPoint(0, 0));
        var br = ScreenToPage(new ScreenPoint(View.Width, View.Height));
        var x0 = Math.Max(0, tl.X);
        var y0 = Math.Max(0, tl.Y);
        var x1 = Math.Min(PageWidth, br.X);
        var y1 = Math.Min(PageHeight, br.Y);
        return x1 > x0 && y1 > y0 ? new PageRect(x0, y0, x1, y1) : null;
    }

    /// <summary>페이지가 화면에서 완전히 사라지지 않게 위치를 제한한다.</summary>
    private void Clamp()
    {
        if (View.IsEmpty || PageWidth <= 0) return;
        double w = PageWidth * Zoom, h = PageHeight * Zoom;
        double keepX = Math.Min(KeepVisible, w), keepY = Math.Min(KeepVisible, h);
        OffsetX = Math.Clamp(OffsetX, keepX - w, View.Width - keepX);
        OffsetY = Math.Clamp(OffsetY, keepY - h, View.Height - keepY);
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
