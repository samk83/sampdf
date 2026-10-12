using System.Windows;

namespace DrawingReview.App.Views;

public partial class PasswordWindow : Window
{
    public PasswordWindow(string fileName, bool retry)
    {
        InitializeComponent();
        Message.Text = retry
            ? $"The password was not correct. Enter the password for \"{fileName}\"."
            : $"\"{fileName}\" is password protected. Enter the password to open it.";
        Loaded += (_, _) => Input.Focus();
    }

    public string Password => Input.Password;

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
