using System.Windows;
using DrawingReview.App.Core.Services;
using DrawingReview.App.Views;
using Microsoft.Win32;

namespace DrawingReview.App.Services;

/// <summary>Windows 기본 파일 선택 창과 메시지 창으로 IDialogService를 구현한다.</summary>
public sealed class WpfDialogService(Func<Window?> owner) : IDialogService
{
    private const string PdfFilter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*";

    public IReadOnlyList<string> PickPdfFilesToOpen()
    {
        var dlg = new OpenFileDialog { Filter = PdfFilter, Multiselect = true, Title = "Open PDF" };
        return dlg.ShowDialog(owner()) == true ? dlg.FileNames : [];
    }

    public string? PickPdfFile(string title)
    {
        var dlg = new OpenFileDialog { Filter = PdfFilter, Title = title };
        return dlg.ShowDialog(owner()) == true ? dlg.FileName : null;
    }

    public string? PickCadFile(string title)
    {
        var dlg = new OpenFileDialog { Filter = "CAD drawings (*.dwg;*.dxf)|*.dwg;*.dxf|All files (*.*)|*.*", Title = title };
        return dlg.ShowDialog(owner()) == true ? dlg.FileName : null;
    }

    public string? PickCompareProject()
    {
        var dlg = new OpenFileDialog { Filter = "Comparison project (*.drcompare)|*.drcompare", Title = "Open comparison project" };
        return dlg.ShowDialog(owner()) == true ? dlg.FileName : null;
    }

    public string? PickSavePath(string suggestedFileName, string title = "Save PDF")
    {
        // 제안한 파일 이름의 확장자로 형식을 고른다 (PDF, CSV/JSON, PNG, 비교 프로젝트)
        var ext = System.IO.Path.GetExtension(suggestedFileName).ToLowerInvariant();
        var filter = ext switch
        {
            ".csv" or ".json" => "CSV (*.csv)|*.csv|JSON (*.json)|*.json",
            ".png" => "PNG image (*.png)|*.png",
            ".drcompare" => "Comparison project (*.drcompare)|*.drcompare",
            ".dxf" or ".dwg" => "DXF drawing (*.dxf)|*.dxf|DWG drawing (*.dwg)|*.dwg",
            _ => PdfFilter,
        };
        var dlg = new SaveFileDialog
        {
            Filter = filter,
            FileName = suggestedFileName,
            Title = title,
            AddExtension = true,
            DefaultExt = string.IsNullOrEmpty(ext) ? ".pdf" : ext,
            OverwritePrompt = true,
        };
        return dlg.ShowDialog(owner()) == true ? dlg.FileName : null;
    }

    public string? PromptPassword(string fileName, bool retry)
    {
        var dlg = new PasswordWindow(fileName, retry) { Owner = owner() };
        return dlg.ShowDialog() == true ? dlg.Password : null;
    }

    public bool? Confirm(string title, string message, bool allowCancel = true)
    {
        var buttons = allowCancel ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo;
        var result = Show(message, title, buttons, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null,
        };
    }

    public void ShowError(string title, string message) => Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message) => Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    private MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        owner() is { } w
            ? MessageBox.Show(w, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);
}
