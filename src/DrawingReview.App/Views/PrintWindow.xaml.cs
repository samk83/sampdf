using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DrawingReview.App.Converters;
using DrawingReview.App.Core.Printing;
using DrawingReview.App.Core.ViewModels;

namespace DrawingReview.App.Views;

/// <summary>
/// 인쇄 미리보기와 인쇄 (기획서 6장 P0). 용지에 맞춤과 실제 크기를 구분하고, 실제 크기가 용지보다 크면 경고한다.
/// 각 페이지를 프린터 해상도에 가깝게 렌더링해 이미지로 인쇄한다.
/// </summary>
public partial class PrintWindow : Window
{
    /// <summary>인쇄용 렌더링 해상도와 한 장의 최대 긴 변 픽셀 (메모리 보호).</summary>
    private const double PrintDpi = 200;
    private const int MaxLongEdge = 7000;

    private readonly DocumentViewModel _doc;
    private readonly PrintDialog _dialog = new();
    private IReadOnlyList<int> _pages = [];
    private int _previewIndex;

    public PrintWindow(DocumentViewModel doc)
    {
        InitializeComponent();
        _doc = doc;
        Title = $"Print — {doc.FileName}";
        RangeText.Text = $"1-{doc.Pages.Count}";
        Loaded += (_, _) => Refresh();
    }

    private (double W, double H) PaperSize()
    {
        var w = _dialog.PrintableAreaWidth;
        var h = _dialog.PrintableAreaHeight;
        return w > 0 && h > 0 ? (w, h) : (8.27 * 96, 11.69 * 96); // 프린터 정보가 없으면 A4
    }

    private PrintScaling Scaling => ActualSize.IsChecked == true ? PrintScaling.ActualSize : PrintScaling.FitToPaper;

    private void OnOptionChanged(object sender, RoutedEventArgs e) { if (IsLoaded) Refresh(); }

    private void Refresh()
    {
        _pages = RangeCurrent.IsChecked == true ? [_doc.CurrentPageIndex]
            : RangeCustom.IsChecked == true ? PrintLayout.ParseRange(RangeText.Text, _doc.Pages.Count)
            : Enumerable.Range(0, _doc.Pages.Count).ToList();
        _previewIndex = Math.Clamp(_previewIndex, 0, Math.Max(0, _pages.Count - 1));
        PrintButton.IsEnabled = _pages.Count > 0;
        var (pw, ph) = PaperSize();
        PaperText.Text = $"Paper (printable area): {pw / 96 * 25.4:0} × {ph / 96 * 25.4:0} mm" +
            (_dialog.PrintQueue != null ? $" · {_dialog.PrintQueue.Name}" : "");
        _ = UpdatePreviewAsync();
    }

    private async Task UpdatePreviewAsync()
    {
        var (pw, ph) = PaperSize();
        Paper.Width = pw;
        Paper.Height = ph;
        if (_pages.Count == 0) { PreviewImage.Source = null; PageText.Text = "No pages"; return; }
        var pageIndex = _pages[_previewIndex];
        var info = _doc.Pages[pageIndex].Info;
        var placement = PrintLayout.Place(info.Width, info.Height, pw, ph, Scaling);
        PageText.Text = $"{_previewIndex + 1} / {_pages.Count}  (page {pageIndex + 1})";
        ScaleText.Text = $"Scale: {placement.Percent:0.#}% of actual size" + (placement.Rotate ? " · rotated 90°" : "");
        WarningText.Text = placement.Clipped ? "The page is larger than the paper at actual size; edges will be cut off." : "";

        var include = IncludeMarkups.IsChecked == true;
        var zoom = 1000 / Math.Max(info.Width, info.Height);
        var image = await _doc.RenderPageAsync(pageIndex, zoom, include);
        Place(PreviewImage, BitmapFactory.ToBitmap(image), info.Width, info.Height, placement);
    }

    /// <summary>이미지를 배치(배율·회전·여백)대로 용지 위에 놓는다.</summary>
    private static void Place(Image img, ImageSource source, double pageW, double pageH, PrintPlacement p)
    {
        img.Source = source;
        img.Stretch = Stretch.Fill;
        img.Width = pageW * p.Scale;
        img.Height = pageH * p.Scale;
        if (p.Rotate)
        {
            // 90° 돌리면 가로·세로가 바뀐다: 돌린 뒤의 왼쪽 위가 (OffsetX, OffsetY)에 오도록
            img.RenderTransform = new RotateTransform(90);
            Canvas.SetLeft(img, p.OffsetX + img.Height);
            Canvas.SetTop(img, p.OffsetY);
        }
        else
        {
            img.RenderTransform = Transform.Identity;
            Canvas.SetLeft(img, p.OffsetX);
            Canvas.SetTop(img, p.OffsetY);
        }
    }

    private void OnPrev(object sender, RoutedEventArgs e) { if (_previewIndex > 0) { _previewIndex--; _ = UpdatePreviewAsync(); } }
    private void OnNext(object sender, RoutedEventArgs e) { if (_previewIndex < _pages.Count - 1) { _previewIndex++; _ = UpdatePreviewAsync(); } }

    private void OnChoosePrinter(object sender, RoutedEventArgs e)
    {
        if (_dialog.ShowDialog() == true) Refresh();
    }

    private async void OnPrint(object sender, RoutedEventArgs e)
    {
        if (_dialog.PrintQueue == null && _dialog.ShowDialog() != true) return;
        PrintButton.IsEnabled = false;
        try
        {
            var (pw, ph) = PaperSize();
            var include = IncludeMarkups.IsChecked == true;
            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = new Size(pw, ph);
            foreach (var pageIndex in _pages)
            {
                var info = _doc.Pages[pageIndex].Info;
                var placement = PrintLayout.Place(info.Width, info.Height, pw, ph, Scaling);
                var zoom = Math.Min(PrintDpi / 72, MaxLongEdge / Math.Max(info.Width, info.Height));
                var image = await _doc.RenderPageAsync(pageIndex, zoom, include);
                var page = new FixedPage { Width = pw, Height = ph };
                var img = new Image();
                Place(img, BitmapFactory.ToBitmap(image), info.Width, info.Height, placement);
                page.Children.Add(img);
                var content = new PageContent();
                ((System.Windows.Markup.IAddChild)content).AddChild(page);
                document.Pages.Add(content);
            }
            _dialog.PrintDocument(document.DocumentPaginator, $"Drawing Review — {_doc.FileName}");
            Close();
        }
        catch (Exception ex) when (ex is PrintQueueException or InvalidOperationException or System.IO.IOException)
        {
            MessageBox.Show(this, ex.Message, "Print failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PrintButton.IsEnabled = true;
        }
    }
}
