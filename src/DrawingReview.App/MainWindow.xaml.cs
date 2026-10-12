using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;

namespace DrawingReview.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _closeConfirmed;
    private GridLength _leftWidth = new(236), _rightWidth = new(300), _reviewHeight = new(230);

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        SourceInitialized += (_, _) => UseDarkTitleBar();
        Drop += OnDrop;
        DragOver += OnDragOver;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        Closing += OnClosing;
    }

    // ---- 패널 접기/펴기 ----

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsLeftPanelVisible):
                Toggle(LeftColumn, LeftSplitterColumn, _vm.IsLeftPanelVisible, ref _leftWidth);
                break;
            case nameof(MainViewModel.IsRightPanelVisible):
                Toggle(RightColumn, RightSplitterColumn, _vm.IsRightPanelVisible, ref _rightWidth);
                break;
            case nameof(MainViewModel.IsReviewListVisible):
                if (_vm.IsReviewListVisible) { ReviewRow.Height = _reviewHeight; ReviewSplitterRow.Height = new GridLength(4); }
                else { _reviewHeight = ReviewRow.Height; ReviewRow.Height = new GridLength(0); ReviewSplitterRow.Height = new GridLength(0); }
                break;
        }
    }

    private static void Toggle(ColumnDefinition column, ColumnDefinition splitter, bool visible, ref GridLength saved)
    {
        if (visible) { column.Width = saved; splitter.Width = new GridLength(4); }
        else { saved = column.Width; column.Width = new GridLength(0); splitter.Width = new GridLength(0); }
    }

    // ---- 키보드: 도구 단축키, 스페이스 이동, Esc ----

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 글자를 입력하는 칸에 있을 때는 단축키를 가로채지 않는다.
        if (Keyboard.FocusedElement is TextBox or PasswordBox or ComboBox) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;

        if (e.Key == Key.Space)
        {
            if (!e.IsRepeat) Viewer.IsSpacePanning = true;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            _vm.CancelToolCommand.Execute(null);
            e.Handled = true;
            return;
        }
        var key = new KeyConverter().ConvertToString(e.Key);
        if (key is { Length: 1 } && ToolInfo.FromShortcut(key[0]) is { } tool)
        {
            _vm.SelectToolCommand.Execute(tool);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) Viewer.IsSpacePanning = false;
    }

    // ---- 파일 끌어다 놓기 ----

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
            await _vm.OpenPathAsync(f);
    }

    private void OnThumbnailSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ThumbnailList.SelectedItem is { } item) ThumbnailList.ScrollIntoView(item);
    }

    // ---- 닫기: 저장하지 않은 변경 확인 ----

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed) return;
        e.Cancel = true;
        if (await _vm.CloseAllAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    // ---- Windows 10/11 어두운 제목 표시줄 ----

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void UseDarkTitleBar()
    {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
        }
        catch (DllNotFoundException) { /* 오래된 Windows: 기본 제목 표시줄 사용 */ }
        catch (EntryPointNotFoundException) { }
    }
}
