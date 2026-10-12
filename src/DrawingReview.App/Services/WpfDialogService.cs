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

    public string? PickSavePath(string suggestedFileName, string title = "Save PDF")
    {
        var dlg = new SaveFileDialog
        {
            Filter = PdfFilter,
            FileName = suggestedFileName,
            Title = title,
            AddExtension = true,
            DefaultExt = ".pdf",
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
