using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Markups;

namespace DrawingReview.App.Views;

/// <summary>
/// Review List 표. 정렬·선택은 ViewModel이 결정하고, 이 클래스는 DataGrid 이벤트를 전달만 한다.
/// </summary>
public partial class ReviewListPanel : UserControl
{
    private bool _applyingSelection;

    public ReviewListPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ReviewListViewModel old) old.SelectionSynced -= OnSelectionSynced;
            if (e.NewValue is ReviewListViewModel vm) vm.SelectionSynced += OnSelectionSynced;
        };
    }

    private ReviewListViewModel? Vm => DataContext as ReviewListViewModel;

    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true; // DataGrid 자체 정렬 대신 ViewModel 정렬(읽는 순서 보조 정렬 포함)을 쓴다
        if (Vm != null && Enum.TryParse<ReviewSortColumn>(e.Column.SortMemberPath, out var column))
            Vm.SortByCommand.Execute(column);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingSelection || Vm == null) return;
        Vm.OnRowsSelected(Grid.SelectedItems.Cast<MarkupItemViewModel>().ToList());
    }

    /// <summary>캔버스에서 선택이 바뀌면 표의 선택을 맞춘다.</summary>
    private void OnSelectionSynced(object? sender, EventArgs e)
    {
        if (Vm == null) return;
        _applyingSelection = true;
        try
        {
            Grid.SelectedItems.Clear();
            foreach (var item in Vm.SelectedItems) Grid.SelectedItems.Add(item);
            if (Vm.SelectedItems.Count > 0) Grid.ScrollIntoView(Vm.SelectedItems[0]);
        }
        finally { _applyingSelection = false; }
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is MarkupItemViewModel item && Vm != null) Vm.OnRowsSelected([item]);
    }

    private void OnSetStatusClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = SetStatusButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var s in ReviewStatus.All)
            menu.Items.Add(new MenuItem { Header = s, Command = Vm?.SetStatusCommand, CommandParameter = s });
        menu.IsOpen = true;
    }

    private void OnRowMenuOpened(object sender, RoutedEventArgs e)
    {
        RowMenu.Items.Clear();
        if (Vm == null || Vm.SelectedItems.Count == 0)
        {
            RowMenu.Items.Add(new MenuItem { Header = "No markup selected", IsEnabled = false });
            return;
        }
        var status = new MenuItem { Header = "Set status" };
        foreach (var s in ReviewStatus.All)
            status.Items.Add(new MenuItem { Header = s, Command = Vm.SetStatusCommand, CommandParameter = s });
        RowMenu.Items.Add(status);
        if (Window.GetWindow(this)?.DataContext is MainViewModel main && main.ActiveDocument is { } doc)
        {
            var items = Vm.SelectedItems.ToList();
            var anyUnlocked = items.Any(i => !i.IsLocked);
            RowMenu.Items.Add(new MenuItem
            {
                Header = anyUnlocked ? "Lock" : "Unlock",
                Command = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => doc.SetLockedAsync(items, anyUnlocked)),
            });
            RowMenu.Items.Add(new Separator());
            RowMenu.Items.Add(new MenuItem { Header = "Delete", InputGestureText = "Del", Command = doc.DeleteSelectedCommand });
        }
    }
}
