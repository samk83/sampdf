using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.MuPdf;

namespace DrawingReview.App.Core.Tests;

public class RecoveryTests
{
    private static readonly RectangleDraft Rect = new(new PageRect(300, 300, 500, 400));

    private static (MainViewModel Vm, FakeDialogs Dialogs) Make(TempDir dir)
    {
        var dialogs = new FakeDialogs();
        var vm = new MainViewModel(new MuPdfEngine(), dialogs) { Recovery = new RecoveryService(dir.File("recovery")) };
        return (vm, dialogs);
    }

    [Fact]
    public async Task Autosave_writes_recovery_copy_without_touching_original()
    {
        using var dir = new TempDir();
        var (vm, _) = Make(dir);
        var path = dir.WritePlanSet();
        var originalBytes = await File.ReadAllBytesAsync(path);
        var doc = (await vm.OpenPathAsync(path))!;

        await vm.AutosaveAsync();
        Assert.Null(vm.Recovery!.Find(path)); // 변경이 없으면 저장하지 않음

        await doc.AddMarkupAsync(0, Rect);
        await vm.AutosaveAsync();
        Assert.NotNull(vm.Recovery.Find(path));
        Assert.NotNull(doc.LastAutosave);
        Assert.Contains("recovery copy", doc.SaveStateText);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Reopening_after_crash_offers_recovery_and_restores_changes()
    {
        using var dir = new TempDir();
        var path = dir.WritePlanSet();
        var (vm, _) = Make(dir);
        var doc = (await vm.OpenPathAsync(path))!;
        var item = await doc.AddMarkupAsync(0, Rect);
        await vm.AutosaveAsync();
        // 강제 종료를 흉내: 닫지 않고 새 프로그램 시작

        var (vm2, dialogs2) = Make(dir);
        dialogs2.ConfirmAnswers.Enqueue(true);
        var restored = (await vm2.OpenPathAsync(path))!;
        Assert.Contains(restored.Markups, m => m.Id == item.Id);
        Assert.True(restored.IsModified);
        Assert.Equal(path, restored.FilePath);

        Assert.True(await vm2.SaveDocumentAsync(restored));
        Assert.Null(vm2.Recovery!.Find(path)); // 저장 후 복구 사본 삭제
    }

    [Fact]
    public async Task Declining_recovery_discards_copy()
    {
        using var dir = new TempDir();
        var path = dir.WritePlanSet();
        var (vm, _) = Make(dir);
        var doc = (await vm.OpenPathAsync(path))!;
        await doc.AddMarkupAsync(0, Rect);
        await vm.AutosaveAsync();

        var (vm2, dialogs2) = Make(dir);
        dialogs2.ConfirmAnswers.Enqueue(false);
        var opened = (await vm2.OpenPathAsync(path))!;
        Assert.DoesNotContain(opened.Markups, m => m.Kind == MarkupKind.Rectangle);
        Assert.Null(vm2.Recovery!.Find(path));
    }

    [Fact]
    public async Task Closing_without_saving_discards_copy()
    {
        using var dir = new TempDir();
        var path = dir.WritePlanSet();
        var (vm, dialogs) = Make(dir);
        var doc = (await vm.OpenPathAsync(path))!;
        await doc.AddMarkupAsync(0, Rect);
        await vm.AutosaveAsync();
        dialogs.ConfirmAnswers.Enqueue(false);
        await vm.CloseDocumentAsync(doc);
        Assert.Null(vm.Recovery!.Find(path));
    }

    [Fact]
    public async Task Flattened_export_refuses_to_overwrite_open_document()
    {
        using var dir = new TempDir();
        var path = dir.WritePlanSet();
        var (vm, dialogs) = Make(dir);
        var doc = (await vm.OpenPathAsync(path))!;
        await doc.AddMarkupAsync(0, Rect);
        dialogs.SavePath = path;
        await vm.ExportFlattenedCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Errors);

        dialogs.SavePath = dir.File("flat.pdf");
        await vm.ExportFlattenedCommand.ExecuteAsync(null);
        Assert.True(File.Exists(dir.File("flat.pdf")));
        Assert.True(doc.IsModified); // 열린 문서는 그대로
    }
}
