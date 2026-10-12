using DrawingReview.App.Core.Viewing;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Core.Tests;

public class ViewportTests
{
    private static Viewport Make(double viewW = 1000, double viewH = 800, double pageW = 2592, double pageH = 1728)
    {
        var v = new Viewport();
        v.SetPage(pageW, pageH);
        v.SetView(new ViewSize(viewW, viewH));
        return v;
    }

    [Fact]
    public void Fit_page_shows_whole_page_centered()
    {
        var v = Make();
        var tl = v.PageToScreen(new PagePoint(0, 0));
        var br = v.PageToScreen(new PagePoint(2592, 1728));
        Assert.True(tl.X >= 0 && tl.Y >= 0 && br.X <= 1000 && br.Y <= 800);
        Assert.Equal(1000 - br.X, tl.X, 3);           // 가로 가운데
        Assert.Equal(new PageRect(0, 0, 2592, 1728), v.VisiblePageRegion());
    }

    [Fact]
    public void Screen_and_page_conversion_round_trip()
    {
        var v = Make();
        v.ZoomAt(new ScreenPoint(300, 200), 3.7);
        var p = new PagePoint(1234.5, 678.9);
        var back = v.ScreenToPage(v.PageToScreen(p));
        Assert.Equal(p.X, back.X, 6);
        Assert.Equal(p.Y, back.Y, 6);
    }

    [Fact]
    public void Zoom_at_keeps_point_under_cursor_fixed()
    {
        var v = Make();
        var anchor = new ScreenPoint(420, 310);
        var before = v.ScreenToPage(anchor);
        v.ZoomAt(anchor, 2.5);
        var after = v.ScreenToPage(anchor);
        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }

    [Fact]
    public void Zoom_is_clamped()
    {
        var v = Make();
        v.ZoomAt(new ScreenPoint(0, 0), 1e9);
        Assert.Equal(Viewport.MaxZoom, v.Zoom);
        v.ZoomAt(new ScreenPoint(0, 0), 1e-12);
        Assert.Equal(Viewport.MinZoom, v.Zoom);
    }

    [Fact]
    public void Pan_cannot_lose_the_page_completely()
    {
        var v = Make();
        v.PanBy(100000, 100000);
        var tl = v.PageToScreen(new PagePoint(0, 0));
        Assert.True(tl.X <= 1000 - 79 && tl.Y <= 800 - 79, $"page origin {tl} pushed off screen");
        v.PanBy(-1e6, -1e6);
        var br = v.PageToScreen(new PagePoint(2592, 1728));
        Assert.True(br.X >= 79 && br.Y >= 79);
    }

    [Fact]
    public void Zoom_percent_is_relative_to_actual_size()
    {
        var v = Make();
        v.SetZoomPercent(100);
        Assert.Equal(96.0 / 72.0, v.Zoom, 6);
        Assert.Equal(100, v.ZoomPercent, 6);
    }

    [Fact]
    public void Visible_region_is_clipped_to_page_when_zoomed_in()
    {
        var v = Make();
        v.SetZoomPercent(400);
        var region = v.VisiblePageRegion()!.Value;
        Assert.True(region.Width < 2592 && region.Height < 1728);
        Assert.Equal(1000 / v.Zoom, region.Width, 3);
    }

    [Fact]
    public void Resizing_window_keeps_center_point()
    {
        var v = Make();
        v.SetZoomPercent(200);
        var center = v.ScreenToPage(new ScreenPoint(500, 400));
        v.SetView(new ViewSize(1400, 900));
        var newCenter = v.ScreenToPage(new ScreenPoint(700, 450));
        Assert.Equal(center.X, newCenter.X, 3);
        Assert.Equal(center.Y, newCenter.Y, 3);
    }

    [Fact]
    public void Zoom_to_region_centers_the_region()
    {
        var v = Make();
        var r = new PageRect(1000, 500, 1200, 600);
        v.ZoomToRegion(r);
        var c = v.PageToScreen(r.Center);
        Assert.Equal(500, c.X, 3);
        Assert.Equal(400, c.Y, 3);
    }

    [Fact]
    public void Changed_event_fires_on_zoom_and_pan()
    {
        var v = Make();
        int count = 0;
        v.Changed += (_, _) => count++;
        v.PanBy(10, 0);
        v.ZoomAt(new ScreenPoint(1, 1), 1.1);
        Assert.Equal(2, count);
    }
}
