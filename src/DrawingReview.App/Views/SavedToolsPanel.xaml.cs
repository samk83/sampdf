using System.Windows;
using System.Windows.Controls;
using DrawingReview.App.Core.ViewModels;
using Microsoft.Win32;

namespace DrawingReview.App.Views;

public partial class SavedToolsPanel : UserControl
{
    public SavedToolsPanel() => InitializeComponent();

    private SavedToolsViewModel? Vm => DataContext as SavedToolsViewModel;

    private void OnSaveSelected(object sender, RoutedEventArgs e)
    {
        if (Vm?.SaveSelected(NewName.Text, IncludeContent.IsChecked == true) is null)
            MessageBox.Show(Window.GetWindow(this)!, "Select one markup on the drawing first.", "Save tool", MessageBoxButton.OK, MessageBoxImage.Information);
        else NewName.Text = "";
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Tool set (*.json)|*.json", Title = "Import tool set" };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true || Vm == null) return;
        try { Vm.ImportSet(dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this)!, ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedSet is not { } set) return;
        var dlg = new SaveFileDialog { Filter = "Tool set (*.json)|*.json", FileName = set.Name + ".json", Title = "Export tool set" };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true) Vm.ExportSet(dlg.FileName);
    }
}
