using System.Windows;
using DrawingReview.App.Core.Comparison;

namespace DrawingReview.App.Views;

public partial class PairWindow : Window
{
    public PairWindow(int oldCount, int newCount, PagePair current)
    {
        InitializeComponent();
        OldPage.ItemsSource = Enumerable.Range(1, oldCount).ToList();
        NewPage.ItemsSource = Enumerable.Range(1, newCount).ToList();
        OldPage.SelectedIndex = current.OldPage;
        NewPage.SelectedIndex = current.NewPage;
    }

    public PagePair Result => new(Math.Max(0, OldPage.SelectedIndex), Math.Max(0, NewPage.SelectedIndex));

    public bool AsNew => AddNew.IsChecked == true;

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
