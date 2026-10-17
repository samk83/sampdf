using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdfium;

namespace DrawingReview.App.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Settings_round_trip_through_file()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.File("cfg/settings.json"));
        var vm = new MainViewModel(new PdfiumEngine(), new FakeDialogs());
        vm.ToolDefaults.Author = "Sam Kim";
        vm.ToolDefaults.SetStyle(ToolKind.Cloud, MarkupStyle.Default with { StrokeColor = RgbColor.Blue, LineWidth = 3 });
        vm.Properties.RecentColors.Add(RgbColor.Blue);
        vm.KeepToolActive = true;
        vm.IsReviewListVisible = false;
        store.Save(vm.CaptureSettings());

        var vm2 = new MainViewModel(new PdfiumEngine(), new FakeDialogs());
        vm2.ApplySettings(store.Load());
        Assert.Equal("Sam Kim", vm2.ToolDefaults.Author);
        Assert.Equal(RgbColor.Blue, vm2.ToolDefaults.Get(ToolKind.Cloud).Style.StrokeColor);
        Assert.Equal(3, vm2.ToolDefaults.Get(ToolKind.Cloud).Style.LineWidth);
        Assert.Equal([RgbColor.Blue], vm2.Properties.RecentColors);
        Assert.True(vm2.KeepToolActive);
        Assert.False(vm2.IsReviewListVisible);
    }

    [Fact]
    public void Missing_or_corrupt_file_gives_defaults()
    {
        using var dir = new TempDir();
        Assert.NotNull(new SettingsStore(dir.File("none.json")).Load());
        File.WriteAllText(dir.File("bad.json"), "{ not json");
        var s = new SettingsStore(dir.File("bad.json")).Load();
        Assert.True(s.IsLeftPanelVisible);
    }

    [Fact]
    public async Task Opened_files_go_to_recent_list()
    {
        using var dir = new TempDir();
        var vm = new MainViewModel(new PdfiumEngine(), new FakeDialogs());
        var a = dir.WritePlanSet("a.pdf");
        var b = dir.WritePlanSet("b.pdf");
        await vm.OpenPathAsync(a);
        await vm.OpenPathAsync(b);
        Assert.Equal([b, a], vm.RecentFiles);
    }
}
