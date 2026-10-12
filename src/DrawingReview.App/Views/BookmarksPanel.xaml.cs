using System.Windows;
using System.Windows.Controls;

namespace DrawingReview.App.Views;

public partial class BookmarksPanel : UserControl
{
    public BookmarksPanel() => InitializeComponent();

    /// <summary>단계(Level)만큼 들여쓰고, 1단계는 굵게 보여준다.</summary>
    private void OnItemLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock t && t.Tag is int level)
        {
            t.Margin = new Thickness((level - 1) * 14, 0, 0, 0);
            t.FontWeight = level == 1 ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }
}
