using System.Windows;
using System.Windows.Threading;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.App.Services;
using DrawingReview.Engine.MuPdf;

namespace DrawingReview.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        MainWindow? window = null;
        var dialogs = new WpfDialogService(() => window);
        var vm = new MainViewModel(new MuPdfEngine(), dialogs);
        window = new MainWindow(vm);
        MainWindow = window;
        window.Show();

        // 탐색기에서 PDF를 이 프로그램으로 열거나 명령줄로 파일을 넘긴 경우
        foreach (var path in e.Args.Where(a => a.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
            await vm.OpenPathAsync(path);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 예상하지 못한 오류로 프로그램이 바로 꺼지지 않게 알리고 계속 실행한다.
        MessageBox.Show(MainWindow, $"An unexpected error occurred:\n\n{e.Exception.Message}", "Drawing Review",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
