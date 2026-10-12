using System.Windows.Controls;
using System.Windows.Input;
using DrawingReview.App.Core.ViewModels;

namespace DrawingReview.App.Views;

public partial class SearchPanel : UserControl
{
    public SearchPanel() => InitializeComponent();

    /// <summary>Enter: 처음이면 검색, 결과가 있으면 다음. Shift+Enter: 이전.</summary>
    private async void OnQueryKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not SearchViewModel vm) return;
        e.Handled = true;
        if (vm.Results.Count == 0) { await vm.SearchCommand.ExecuteAsync(null); vm.NextCommand.Execute(null); }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) vm.PreviousCommand.Execute(null);
        else vm.NextCommand.Execute(null);
    }
}
