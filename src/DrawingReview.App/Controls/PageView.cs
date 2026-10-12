using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DrawingReview.App.Converters;
using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.Viewing;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Controls;

/// <summary>
/// 가운데 도면 캔버스. 두 겹으로 그린다:
/// 1) 미리보기: 페이지 전체를 낮은 해상도로 한 번 렌더링해 즉시 보여준다.
/// 2) 고해상도 타일: 확대·이동이 멈추면 보이는 영역만 화면 해상도로 렌더링해 덮는다.
/// 확대·이동 계산은 App.Core의 Viewport가 하고, 이 클래스는 그리기와 마우스 입력만 담당한다.
/// </summary>
public class PageView : FrameworkElement
{
    private static readonly Brush CanvasBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x42)));
    private static readonly Brush ShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0)));
    private static readonly Brush HintBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xC9, 0xCC, 0xD1)));
    /// <summary>타일 한 장의 최대 픽셀 수 (메모리·속도 보호).</summary>
    private const double MaxTilePixels = 24_000_000;

    private readonly DispatcherTimer _renderTimer;
    private CancellationTokenSource? _renderCts;
    private BitmapSource? _preview;
    private int _previewPage = -1;
    private BitmapSource? _tile;
    private PageRect _tileRegion;
    private int _tilePage = -1;
    private double _tilePixelsPerPoint;
    private Point? _panLast;

    public PageView()
    {
        Focusable = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        _renderTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(140) };
        _renderTimer.Tick += (_, _) => { _renderTimer.Stop(); _ = RenderTileAsync(); };
    }

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(DocumentViewModel), typeof(PageView),
        new PropertyMetadata(null, (d, e) => ((PageView)d).OnDocumentChanged((DocumentViewModel?)e.OldValue, (DocumentViewModel?)e.NewValue)));

    public DocumentViewModel? Document
    {
        get => (DocumentViewModel?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public static readonly DependencyProperty ActiveToolProperty = DependencyProperty.Register(
        nameof(ActiveTool), typeof(ToolKind), typeof(PageView),
        new PropertyMetadata(ToolKind.Select, (d, _) => ((PageView)d).UpdateCursor()));

    public ToolKind ActiveTool
    {
        get => (ToolKind)GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    /// <summary>스페이스바를 누르고 있는 동안은 어떤 도구든 끌어서 이동한다 (창이 알려 준다).</summary>
    public bool IsSpacePanning
    {
        get => _isSpacePanning;
        set { _isSpacePanning = value; UpdateCursor(); }
    }
    private bool _isSpacePanning;

    protected Viewport? Viewport => Document?.Viewport;

    /// <summary>표시하는 문서가 바뀔 때 (하위 클래스가 이벤트를 다시 연결하는 데 쓴다).</summary>
    protected event Action<DocumentViewModel?, DocumentViewModel?>? DocumentChanged;

    private void OnDocumentChanged(DocumentViewModel? oldDoc, DocumentViewModel? newDoc)
    {
        if (oldDoc != null)
        {
            oldDoc.Viewport.Changed -= OnViewportChanged;
            oldDoc.PageChanged -= OnPageChanged;
            oldDoc.RenderInvalidated -= OnRenderInvalidated;
        }
        _preview = null;
        _tile = null;
        _previewPage = _tilePage = -1;
        if (newDoc != null)
        {
            newDoc.Viewport.Changed += OnViewportChanged;
            newDoc.PageChanged += OnPageChanged;
            newDoc.RenderInvalidated += OnRenderInvalidated;
            if (ActualWidth > 0) newDoc.Viewport.SetView(new ViewSize(ActualWidth, ActualHeight));
            _ = LoadPreviewAsync();
        }
        DocumentChanged?.Invoke(oldDoc, newDoc);
        InvalidateVisual();
    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void OnPageChanged(object? sender, EventArgs e)
    {
        _tile = null;
        _tilePage = -1;
        _ = LoadPreviewAsync();
        InvalidateVisual();
    }

    private void OnRenderInvalidated(object? sender, int? page)
    {
        if (Document is not { } doc || (page is { } p && p != doc.CurrentPageIndex)) return;
        _ = LoadPreviewAsync(keepCurrentUntilReady: true);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Viewport?.SetView(new ViewSize(sizeInfo.NewSize.Width, sizeInfo.NewSize.Height));
    }

    private async Task LoadPreviewAsync(bool keepCurrentUntilReady = false)
    {
        if (Document is not { } doc || doc.CurrentPage == null) return;
        var page = doc.CurrentPageIndex;
        if (!keepCurrentUntilReady) { _preview = null; _previewPage = -1; }
        try
        {
            var image = await doc.RenderPreviewAsync(page);
            if (Document != doc || doc.CurrentPageIndex != page) return; // 그사이 다른 페이지로 이동함
            _preview = BitmapFactory.ToBitmap(image);
            _previewPage = page;
            if (keepCurrentUntilReady) { _tile = null; _tilePage = -1; }
            InvalidateVisual();
            _renderTimer.Stop();
            _renderTimer.Start();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Preview render failed: {ex}");
        }
    }

    private async Task RenderTileAsync()
    {
        if (Document is not { } doc || Viewport is not { } vp || doc.CurrentPage == null) return;
        if (vp.VisiblePageRegion() is not { } visible) return;

        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var ppp = vp.Zoom * dpi;
        var page = doc.CurrentPageIndex;

        // 미리보기 해상도로 충분하면 타일이 필요 없다.
        var previewPpp = DocumentViewModel.PreviewLongEdge / Math.Max(vp.PageWidth, vp.PageHeight);
        if (ppp <= previewPpp * 1.05)
        {
            _tile = null;
            InvalidateVisual();
            return;
        }

        // 화면 가장자리 이동에 대비해 조금 넓게 렌더링한다.
        var margin = Math.Min(visible.Width, visible.Height) * 0.08;
        var region = new PageRect(
            Math.Max(0, visible.X0 - margin), Math.Max(0, visible.Y0 - margin),
            Math.Min(vp.PageWidth, visible.X1 + margin), Math.Min(vp.PageHeight, visible.Y1 + margin));
        var pixels = region.Width * region.Height * ppp * ppp;
        if (pixels > MaxTilePixels) ppp *= Math.Sqrt(MaxTilePixels / pixels);

        if (_tile != null && _tilePage == page && Math.Abs(_tilePixelsPerPoint - ppp) < 1e-6 && Contains(_tileRegion, visible))
            return; // 지금 타일이 이미 보이는 영역을 덮고 있음

        _renderCts?.Cancel();
        var cts = _renderCts = new CancellationTokenSource();
        try
        {
            var image = await doc.RenderRegionAsync(page, region, ppp, cts.Token);
            if (cts.IsCancellationRequested || Document != doc || doc.CurrentPageIndex != page) return;
            _tile = BitmapFactory.ToBitmap(image);
            _tileRegion = image.Region;
            _tilePage = page;
            _tilePixelsPerPoint = ppp;
            InvalidateVisual();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tile render failed: {ex}");
        }
    }

    private static bool Contains(PageRect outer, PageRect inner) =>
        inner.X0 >= outer.X0 - 0.5 && inner.Y0 >= outer.Y0 - 0.5 && inner.X1 <= outer.X1 + 0.5 && inner.Y1 <= outer.Y1 + 0.5;

    /// <summary>페이지 영역(PDF pt) → 화면 사각형.</summary>
    protected Rect ToScreen(PageRect r)
    {
        var vp = Viewport!;
        var a = vp.PageToScreen(new PagePoint(r.X0, r.Y0));
        return new Rect(a.X, a.Y, r.Width * vp.Zoom, r.Height * vp.Zoom);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(CanvasBrush, null, new Rect(RenderSize));
        if (Document is not { } doc || Viewport is not { } vp || doc.CurrentPage == null)
        {
            DrawHint(dc, "Open a PDF with Ctrl+O, or drag files here");
            return;
        }

        var pageRect = ToScreen(new PageRect(0, 0, vp.PageWidth, vp.PageHeight));
        dc.DrawRectangle(ShadowBrush, null, new Rect(pageRect.X + 3, pageRect.Y + 4, pageRect.Width, pageRect.Height));
        dc.DrawRectangle(Brushes.White, null, pageRect);
        if (_preview != null && _previewPage == doc.CurrentPageIndex)
            dc.DrawImage(_preview, pageRect);
        if (_tile != null && _tilePage == doc.CurrentPageIndex)
            dc.DrawImage(_tile, ToScreen(_tileRegion));

        OnRenderOverlay(dc);
    }

    /// <summary>도면 위에 겹쳐 그리는 층 (선택 표시, 그리는 중인 마크업). 하위 클래스가 사용한다.</summary>
    protected virtual void OnRenderOverlay(DrawingContext dc) { }

    private void DrawHint(DrawingContext dc, string text)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 15, HintBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point((ActualWidth - ft.Width) / 2, (ActualHeight - ft.Height) / 2));
    }

    // ---- 마우스: 휠 확대, 가운데 버튼/손 도구/스페이스 끌기로 이동 ----

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Viewport is not { } vp) return;
        var p = e.GetPosition(this);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            vp.PanBy(e.Delta / 2.0, 0);                     // Shift+휠: 가로 이동
        else
            vp.ZoomAt(new ScreenPoint(p.X, p.Y), Math.Pow(1.2, e.Delta / 120.0));
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var pan = e.ChangedButton == MouseButton.Middle
                  || (e.ChangedButton == MouseButton.Left && (ActiveTool == ToolKind.Pan || IsSpacePanning));
        if (pan && Viewport != null)
        {
            _panLast = e.GetPosition(this);
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_panLast is { } last && Viewport is { } vp)
        {
            var p = e.GetPosition(this);
            vp.PanBy(p.X - last.X, p.Y - last.Y);
            _panLast = p;
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_panLast != null)
        {
            _panLast = null;
            ReleaseMouseCapture();
            UpdateCursor();
            e.Handled = true;
        }
    }

    protected bool IsPanning => _panLast != null;

    protected virtual void UpdateCursor()
    {
        Cursor = ActiveTool == ToolKind.Pan || IsSpacePanning ? Cursors.Hand
            : ToolInfo.CreatesMarkup(ActiveTool) ? Cursors.Cross
            : Cursors.Arrow;
    }

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
