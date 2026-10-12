using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingReview.App.Converters;
using DrawingReview.App.Core.Comparison;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Controls;

/// <summary>
/// 비교 창의 도면 캔버스. 마크업 캔버스(확대·이동·Cloud 편집)를 그대로 쓰고,
/// 그 위에 겹쳐 보기 그림, 변경 영역 표시, 수동 정렬 점을 더 그린다.
/// IsOldSide면 변경 영역을 이전 도면 좌표(BoundsInOld)로 그린다.
/// </summary>
public sealed class CompareCanvas : MarkupPageView
{
    private BitmapSource? _overlayBitmap;

    public static readonly DependencyProperty OverlayImageProperty = DependencyProperty.Register(
        nameof(OverlayImage), typeof(RenderedImage), typeof(CompareCanvas),
        new PropertyMetadata(null, (d, e) =>
        {
            var c = (CompareCanvas)d;
            c._overlayBitmap = e.NewValue is RenderedImage img ? BitmapFactory.ToBitmap(img) : null;
            c.InvalidateVisual();
        }));

    public RenderedImage? OverlayImage
    {
        get => (RenderedImage?)GetValue(OverlayImageProperty);
        set => SetValue(OverlayImageProperty, value);
    }

    public static readonly DependencyProperty ChangesProperty = DependencyProperty.Register(
        nameof(Changes), typeof(IEnumerable), typeof(CompareCanvas),
        new PropertyMetadata(null, (d, e) => ((CompareCanvas)d).OnChangesSourceChanged(e.OldValue as IEnumerable, e.NewValue as IEnumerable)));

    public IEnumerable? Changes
    {
        get => (IEnumerable?)GetValue(ChangesProperty);
        set => SetValue(ChangesProperty, value);
    }

    public static readonly DependencyProperty SelectedChangeProperty = DependencyProperty.Register(
        nameof(SelectedChange), typeof(ChangeItemViewModel), typeof(CompareCanvas),
        new PropertyMetadata(null, (d, _) => ((CompareCanvas)d).InvalidateVisual()));

    public ChangeItemViewModel? SelectedChange
    {
        get => (ChangeItemViewModel?)GetValue(SelectedChangeProperty);
        set => SetValue(SelectedChangeProperty, value);
    }

    public static readonly DependencyProperty IsOldSideProperty = DependencyProperty.Register(
        nameof(IsOldSide), typeof(bool), typeof(CompareCanvas), new PropertyMetadata(false));

    public bool IsOldSide
    {
        get => (bool)GetValue(IsOldSideProperty);
        set => SetValue(IsOldSideProperty, value);
    }

    /// <summary>켜져 있으면 클릭이 마크업 도구 대신 정렬 점 찍기로 쓰인다.</summary>
    public static readonly DependencyProperty IsPickingPointProperty = DependencyProperty.Register(
        nameof(IsPickingPoint), typeof(bool), typeof(CompareCanvas),
        new PropertyMetadata(false, (d, _) => ((CompareCanvas)d).Cursor = (bool)((CompareCanvas)d).GetValue(IsPickingPointProperty) ? Cursors.Pen : null));

    public bool IsPickingPoint
    {
        get => (bool)GetValue(IsPickingPointProperty);
        set => SetValue(IsPickingPointProperty, value);
    }

    /// <summary>정렬 점을 찍었을 때 (페이지 좌표).</summary>
    public event EventHandler<PagePoint>? PointPicked;

    /// <summary>찍어 둔 정렬 점 (이 화면 쪽 것만).</summary>
    public IReadOnlyList<PagePoint> PickedPoints { get; set; } = [];

    private void OnChangesSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (oldValue is INotifyCollectionChanged o) o.CollectionChanged -= OnChangesCollectionChanged;
        if (newValue is INotifyCollectionChanged n) n.CollectionChanged += OnChangesCollectionChanged;
        Resubscribe();
        InvalidateVisual();
    }

    private readonly List<INotifyPropertyChanged> _subscribed = [];

    private void OnChangesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Resubscribe();
        InvalidateVisual();
    }

    private void Resubscribe()
    {
        foreach (var s in _subscribed) s.PropertyChanged -= OnChangeItemChanged;
        _subscribed.Clear();
        if (Changes == null) return;
        foreach (var item in Changes.OfType<INotifyPropertyChanged>())
        {
            item.PropertyChanged += OnChangeItemChanged;
            _subscribed.Add(item);
        }
    }

    private void OnChangeItemChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (IsPickingPoint && e.ChangedButton == MouseButton.Left && Viewport != null)
        {
            PointPicked?.Invoke(this, ToPage(e.GetPosition(this)));
            e.Handled = true;
            return;
        }
        base.OnMouseDown(e);
    }

    protected override void OnRenderOverlay(DrawingContext dc)
    {
        if (Document is not { } doc || Viewport is not { } vp) return;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (_overlayBitmap != null)
        {
            // 겹쳐 보기 그림은 페이지 전체를 덮으므로 PDF 위의 Revision Cloud를 다시 그려 보이게 한다.
            dc.DrawImage(_overlayBitmap, ToScreen(new PageRect(0, 0, vp.PageWidth, vp.PageHeight)));
            foreach (var m in doc.MarkupsOnPage(doc.CurrentPageIndex))
                if (m.Draft != null) DraftRenderer.Draw(dc, m.Draft, ToScreenPoint, vp.Zoom, dpi);
        }

        base.OnRenderOverlay(dc);

        if (Changes != null)
        {
            foreach (var c in Changes.OfType<ChangeItemViewModel>())
            {
                if (!c.IsVisible) continue;
                var r = ToScreen((IsOldSide ? c.BoundsInOld : c.Bounds).Inflate(4));
                var brush = c.Status == ChangeStatus.Dismissed ? DismissedBrush
                    : c.Kind switch { ChangeKind.Added => AddedBrush, ChangeKind.Removed => RemovedBrush, _ => ModifiedBrush };
                var pen = new Pen(brush, c == SelectedChange ? 3 : 1.5);
                if (c.Confidence == ChangeConfidence.Low || c.Status == ChangeStatus.Dismissed) pen.DashStyle = new DashStyle([4, 3], 0);
                pen.Freeze();
                dc.DrawRectangle(c == SelectedChange ? SelectedFill : null, pen, r);
                var label = new FormattedText(c.Number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 11, Brushes.White, dpi);
                var tag = new Rect(r.X, r.Y - label.Height - 2, label.Width + 8, label.Height + 2);
                dc.DrawRoundedRectangle(brush, null, tag, 3, 3);
                dc.DrawText(label, new Point(tag.X + 4, tag.Y + 1));
            }
        }

        foreach (var (p, i) in PickedPoints.Select((p, i) => (p, i)))
        {
            var s = ToScreenPoint(p);
            dc.DrawEllipse(null, PickPen, s, 8, 8);
            dc.DrawLine(PickPen, new Point(s.X - 12, s.Y), new Point(s.X + 12, s.Y));
            dc.DrawLine(PickPen, new Point(s.X, s.Y - 12), new Point(s.X, s.Y + 12));
            var t = new FormattedText((i + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"), 12, PickPen.Brush, dpi);
            dc.DrawText(t, new Point(s.X + 10, s.Y - 22));
        }
    }

    // 상태는 색과 선 모양(점선 = 확인 필요/무시함)과 번호 글자로 함께 구분한다.
    private static readonly Brush AddedBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xDE)));
    private static readonly Brush RemovedBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xD9, 0x3B, 0x30)));
    private static readonly Brush ModifiedBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x1E)));
    private static readonly Brush DismissedBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x8A, 0x8D, 0x93)));
    private static readonly Brush SelectedFill = Frozen(new SolidColorBrush(Color.FromArgb(0x22, 0xE0, 0x8A, 0x1E)));
    private static readonly Pen PickPen = FrozenPen(new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0xB3, 0x7E)), 2));

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
    private static Pen FrozenPen(Pen p) { p.Freeze(); return p; }
}
