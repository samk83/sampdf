using System.Windows;
using System.Windows.Threading;
using DrawingReview.App.Core.Services;
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
        var settings = new SettingsStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DrawingReview", "settings.json"));
        vm.ApplySettings(settings.Load());
        vm.Recovery = new RecoveryService(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrawingReview", "Recovery"));
        // 1분마다 변경된 문서를 복구 폴더에 자동 저장 (원본 PDF는 건드리지 않음)
        var autosave = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        autosave.Tick += async (_, _) => await vm.AutosaveAsync();
        autosave.Start();
        window = new MainWindow(vm);
        window.Closed += (_, _) =>
        {
            try { settings.Save(vm.CaptureSettings()); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { /* 설정 저장 실패는 무시 */ }
        };
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
