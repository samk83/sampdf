using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrawingReview.App.Core.Comparison;
using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.Tools;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Views;

/// <summary>
/// 도면 비교 창. 판단과 상태는 CompareViewModel이 갖고, 여기서는 화면 배치·동기화·대화상자만 처리한다.
/// </summary>
public partial class CompareWindow : Window
{
    private readonly CompareViewModel _vm;
    private readonly IDialogService _dialogs;
    private bool _syncing;
    private Core.ViewModels.DocumentViewModel? _syncedNew;

    public CompareWindow(CompareViewModel vm, IDialogService dialogs)
    {
        InitializeComponent();
        _vm = vm;
        _dialogs = dialogs;
        DataContext = vm;
        vm.PropertyChanged += OnVmChanged;
        vm.ManualPoints.CollectionChanged += (_, _) => UpdatePickedPoints();
        OldCanvas.PointPicked += async (_, p) => await vm.AddAlignmentPointAsync(true, p);
        NewCanvas.PointPicked += async (_, p) => await vm.AddAlignmentPointAsync(false, p);
        PreviewKeyDown += OnKey;
        Closed += (_, _) => vm.Dispose();
        ApplyMode();
        HookSync();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CompareViewModel.Mode): ApplyMode(); break;
            case nameof(CompareViewModel.NewDocument): HookSync(); break;
            case nameof(CompareViewModel.Alignment): SyncNow(); break;
            case nameof(CompareViewModel.SelectedChange):
                if (_vm.SelectedChange is { } c && !ChangeList.SelectedItems.Contains(c)) ChangeList.ScrollIntoView(c);
                break;
        }
    }

    /// <summary>나란히 보기면 이전 도면 칸을 보인다.</summary>
    private void ApplyMode()
    {
        var side = _vm.Mode == CompareViewMode.SideBySide;
        OldColumn.Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SplitterColumn.Width = side ? new GridLength(2) : new GridLength(0);
        Dispatcher.BeginInvoke(SyncNow, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ---- 확대·이동 동기화 ----

    private void HookSync()
    {
        if (_syncedNew != null) _syncedNew.Viewport.Changed -= OnNewViewportChanged;
        _syncedNew = _vm.NewDocument;
        if (_syncedNew != null) _syncedNew.Viewport.Changed += OnNewViewportChanged;
    }

    private void OnNewViewportChanged(object? sender, EventArgs e) => SyncNow();

    private void SyncNow()
    {
        if (_syncing || !_vm.SyncNavigation || _vm.Mode != CompareViewMode.SideBySide || _vm.NewDocument == null || _vm.OldDocument == null) return;
        if (_vm.OldDocument.Viewport.View.IsEmpty) return;
        _syncing = true;
        try
        {
            ViewportSync.Follow(_vm.NewDocument.Viewport, _vm.OldDocument.Viewport,
                _vm.Alignment?.Transform ?? Compare.Model.AlignmentTransform.Identity);
        }
        finally { _syncing = false; }
    }

    private void UpdatePickedPoints()
    {
        OldCanvas.PickedPoints = _vm.ManualPoints.Where((_, i) => i % 2 == 0).ToList();
        NewCanvas.PickedPoints = _vm.ManualPoints.Where((_, i) => i % 2 == 1).ToList();
        OldCanvas.InvalidateVisual();
        NewCanvas.InvalidateVisual();
    }

    // ---- 파일 ----

    private async void OnOpenDrawings(object sender, RoutedEventArgs e)
    {
        var oldPath = _dialogs.PickPdfFile("Select the OLD (previous) drawing");
        if (oldPath == null) return;
        var newPath = _dialogs.PickPdfFile("Select the NEW (revised) drawing");
        if (newPath == null) return;
        if (await _vm.OpenAsync(oldPath, newPath)) await _vm.RunCompareAsync();
    }

    private async void OnOpenProject(object sender, RoutedEventArgs e)
    {
        var path = _dialogs.PickCompareProject();
        if (path == null) return;
        try { await _vm.LoadProjectAsync(path); }
        catch (Exception ex) { _dialogs.ShowError("Could not open project", ex.Message); }
    }

    /// <summary>페이지 번호가 다른 짝을 지정한다 (간단한 입력 창).</summary>
    private void OnEditPair(object sender, RoutedEventArgs e)
    {
        if (_vm.OldDocument == null || _vm.NewDocument == null) return;
        var current = _vm.SelectedPair ?? new PagePair(0, 0);
        var dlg = new PairWindow(_vm.OldDocument.Pages.Count, _vm.NewDocument.Pages.Count, current) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var index = _vm.SelectedPair is { } p ? _vm.PagePairs.IndexOf(p) : -1;
            _vm.SetPair(dlg.AsNew ? -1 : index, dlg.Result);
        }
    }

    // ---- Cloud 설정 ----

    private void OnCloudOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _vm == null) return;
        static string Tag(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        var color = Tag(ColorBox) switch
        {
            "Blue" => new RgbColor(0.184, 0.435, 0.871),
            "Magenta" => new RgbColor(0.761, 0.220, 0.620),
            "Black" => RgbColor.Black,
            _ => new RgbColor(0.898, 0.216, 0.173),
        };
        _vm.CloudOptions = new CloudOptions
        {
            Shape = Tag(ShapeBox) == "Rectangle" ? CloudShape.Rectangle : CloudShape.Cloud,
            MarginPt = double.TryParse(Tag(MarginBox), out var m) ? m : 8,
            ArcSize = int.TryParse(Tag(ArcBox), out var a) ? a : 2,
            LineWidth = double.TryParse(Tag(WidthBox), out var w) ? w : 2,
            Color = color,
            Numbering = NumberingBox.IsChecked == true,
        };
    }

    // ---- 변경 목록 ----

    private void OnChangeDoubleClick(object sender, MouseButtonEventArgs e) => _vm.NavigateCommand.Execute(null);

    private void OnChangeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ChangeList.SelectedItems.Count == 1) _vm.NavigateCommand.Execute(null);
    }

    private void OnMerge(object sender, RoutedEventArgs e)
    {
        var items = ChangeList.SelectedItems.OfType<ChangeItemViewModel>().ToList();
        if (items.Count < 2) { _vm.Status = "Select two or more changes (Ctrl+click) to merge."; return; }
        _vm.Merge(items);
    }

    private async void OnSplit(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedChange is { } c) await _vm.SplitAsync(c);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return;
        switch (e.Key)
        {
            case Key.Escape:
                if (_vm.IsPickingAlignment) _vm.CancelManualAlignmentCommand.Execute(null);
                else if (_vm.Interaction is { IsBusy: true } i) i.Cancel();
                else _vm.ActiveTool = ToolKind.Select;
                e.Handled = true;
                break;
            case Key.V: _vm.ActiveTool = ToolKind.Select; e.Handled = true; break;
            case Key.H: _vm.ActiveTool = ToolKind.Pan; e.Handled = true; break;
            case Key.C when Keyboard.Modifiers == ModifierKeys.None: _vm.ActiveTool = ToolKind.Cloud; e.Handled = true; break;
            case Key.R when Keyboard.Modifiers == ModifierKeys.None: _vm.ActiveTool = ToolKind.Rectangle; e.Handled = true; break;
            case Key.Space when !e.IsRepeat: NewCanvas.IsSpacePanning = OldCanvas.IsSpacePanning = true; break;
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (e.Key == Key.Space) NewCanvas.IsSpacePanning = OldCanvas.IsSpacePanning = false;
    }
}
