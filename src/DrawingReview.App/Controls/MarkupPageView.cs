using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Controls;

/// <summary>
/// 도면 캔버스 + 마크업 편집. 마우스 입력을 App.Core의 MarkupInteraction에 전달하고,
/// 선택 표시·손잡이·미리보기·범위 선택 사각형·검색 결과 강조를 도면 위에 그린다.
/// </summary>
public class MarkupPageView : PageView
{
    private static readonly Brush AccentBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x2F, 0x7F, 0xE0)));
    private static readonly Brush BandFill = Frozen(new SolidColorBrush(Color.FromArgb(0x28, 0x4C, 0x9B, 0xEA)));
    private static readonly Brush SearchFill = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x9F, 0x1A)));
    private static readonly Pen SelectionPen = FrozenPen(new Pen(AccentBrush, 1) { DashStyle = new DashStyle([4, 3], 0) });
    private static readonly Pen HandlePen = FrozenPen(new Pen(AccentBrush, 1.5));
    private static readonly Pen SearchPen = FrozenPen(new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)), 2));
    private const double HandleSize = 8;

    private bool _drawingWithMouse;

    public static readonly DependencyProperty InteractionProperty = DependencyProperty.Register(
        nameof(Interaction), typeof(MarkupInteraction), typeof(MarkupPageView),
        new PropertyMetadata(null, (d, e) => ((MarkupPageView)d).OnInteractionChanged((MarkupInteraction?)e.OldValue, (MarkupInteraction?)e.NewValue)));

    public MarkupInteraction? Interaction
    {
        get => (MarkupInteraction?)GetValue(InteractionProperty);
        set => SetValue(InteractionProperty, value);
    }

    /// <summary>검색 결과 강조 (현재 페이지에 해당하는 영역).</summary>
    public static readonly DependencyProperty SearchHitProperty = DependencyProperty.Register(
        nameof(SearchHit), typeof(SearchHitInfo), typeof(MarkupPageView),
        new PropertyMetadata(null, (d, _) => ((MarkupPageView)d).InvalidateVisual()));

    public SearchHitInfo? SearchHit
    {
        get => (SearchHitInfo?)GetValue(SearchHitProperty);
        set => SetValue(SearchHitProperty, value);
    }

    public MarkupPageView()
    {
        DocumentChanged += (oldDoc, newDoc) =>
        {
            if (oldDoc != null) { oldDoc.SelectionChanged -= OnRedraw; oldDoc.MarkupsChanged -= OnRedraw; }
            if (newDoc != null) { newDoc.SelectionChanged += OnRedraw; newDoc.MarkupsChanged += OnRedraw; }
        };
    }

    private void OnInteractionChanged(MarkupInteraction? oldValue, MarkupInteraction? newValue)
    {
        if (oldValue != null) oldValue.Changed -= OnRedraw;
        if (newValue != null) newValue.Changed += OnRedraw;
        InvalidateVisual();
    }

    private void OnRedraw(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>페이지 영역을 이 컨트롤 안의 화면 사각형으로 (글자 입력 칸 배치용).</summary>
    public Rect PageRectToScreen(PageRect r) => ToScreen(r);

    protected PagePoint ToPage(Point p) => Viewport!.ScreenToPage(new Core.Viewing.ScreenPoint(p.X, p.Y));

    protected Point ToScreenPoint(PagePoint p)
    {
        var s = Viewport!.PageToScreen(p);
        return new Point(s.X, s.Y);
    }

    private static PointerModifiers Mods()
    {
        var m = PointerModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) m |= PointerModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) m |= PointerModifiers.Control;
        return m;
    }

    // ---- 마우스 ----

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e); // 가운데 버튼·손 도구·스페이스 이동은 기본 클래스가 처리
        if (e.Handled || e.ChangedButton != MouseButton.Left || Interaction is not { } ui || Viewport is not { } vp || Document?.CurrentPage == null) return;
        var p = ToPage(e.GetPosition(this));
        if (e.ClickCount == 2 && ActiveTool == ToolKind.Select)
        {
            ui.Cancel();
            ui.DoubleClick(p, 1 / vp.Zoom);
            e.Handled = true;
            return;
        }
        ui.PointerDown(p, ActiveTool, Mods(), 1 / vp.Zoom);
        _drawingWithMouse = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Handled || IsPanning || Interaction is not { } ui || Viewport is not { } vp || Document?.CurrentPage == null) return;
        var p = ToPage(e.GetPosition(this));
        if (_drawingWithMouse) ui.PointerMove(p, Mods());
        else if (ActiveTool == ToolKind.Select && !IsSpacePanning) UpdateHoverCursor(ui, p, vp.Zoom);
    }

    protected override async void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Handled || e.ChangedButton != MouseButton.Left || !_drawingWithMouse || Interaction is not { } ui) return;
        _drawingWithMouse = false;
        var p = ToPage(e.GetPosition(this));
        ReleaseMouseCapture();
        e.Handled = true;
        await ui.PointerUpAsync(p, Mods());
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_drawingWithMouse) { _drawingWithMouse = false; Interaction?.Cancel(); }
    }

    private void UpdateHoverCursor(MarkupInteraction ui, PagePoint p, double zoom)
    {
        Cursor = ui.HandleUnder(p, 1 / zoom) switch
        {
            HandleKind.TopLeft or HandleKind.BottomRight => Cursors.SizeNWSE,
            HandleKind.TopRight or HandleKind.BottomLeft => Cursors.SizeNESW,
            HandleKind.Top or HandleKind.Bottom => Cursors.SizeNS,
            HandleKind.Left or HandleKind.Right => Cursors.SizeWE,
            HandleKind.LineStart or HandleKind.LineEnd or HandleKind.CalloutTip => Cursors.Cross,
            _ => ui.HitTest(p) is { } hit ? (hit.IsLocked || !hit.IsEditable ? Cursors.Arrow : Cursors.SizeAll) : Cursors.Arrow,
        };
    }

    // ---- 겹쳐 그리기 ----

    protected override void OnRenderOverlay(DrawingContext dc)
    {
        if (Document is not { } doc || Viewport is not { } vp) return;
        var page = doc.CurrentPageIndex;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (SearchHit is { } hit && hit.PageIndex == page)
            foreach (var r in hit.Rects) dc.DrawRectangle(SearchFill, SearchPen, ToScreen(r.Inflate(1)));

        foreach (var m in doc.SelectedMarkups.Where(m => m.PageIndex == page))
        {
            var box = ToScreen(m.Bounds);
            box.Inflate(3, 3);
            dc.DrawRectangle(null, SelectionPen, box);
        }
        if (doc.SelectedMarkups.Count == 1 && doc.SelectedMarkups[0] is { IsEditable: true, IsLocked: false } sel && sel.PageIndex == page
            && Interaction is not { IsBusy: true })
        {
            foreach (var (_, point) in DraftGeometry.Handles(sel.Draft!))
            {
                var c = ToScreenPoint(point);
                dc.DrawRectangle(Brushes.White, HandlePen, new Rect(c.X - HandleSize / 2, c.Y - HandleSize / 2, HandleSize, HandleSize));
            }
        }

        if (Interaction is { } ui)
        {
            foreach (var d in ui.Previews)
                DraftRenderer.Draw(dc, d, ToScreenPoint, vp.Zoom, dpi, alpha: 0.85);
            if (ui.RubberBand is { } band)
                dc.DrawRectangle(BandFill, SelectionPen, ToScreen(band));
        }
    }

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
    private static Pen FrozenPen(Pen p) { p.Freeze(); return p; }
}

/// <summary>캔버스에 강조할 검색 결과 (페이지와 영역들).</summary>
public sealed record SearchHitInfo(int PageIndex, IReadOnlyList<PageRect> Rects);
