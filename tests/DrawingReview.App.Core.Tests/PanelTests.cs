using DrawingReview.App.Core.Printing;
using DrawingReview.App.Core.Services;
using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.MuPdf;

namespace DrawingReview.App.Core.Tests;

public class PanelTests
{
    private static async Task<(MainViewModel Vm, DocumentViewModel Doc, TempDir Dir)> OpenAsync()
    {
        var dir = new TempDir();
        var vm = new MainViewModel(new MuPdfEngine(), new FakeDialogs());
        var doc = (await vm.OpenPathAsync(dir.WritePlanSet()))!;
        doc.Viewport.SetView(new Viewing.ViewSize(1200, 800));
        return (vm, doc, dir);
    }

    [Fact]
    public async Task Search_lists_hits_with_context_and_navigates()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        vm.Search.Query = "TRUSS";
        await vm.Search.SearchAsync();
        Assert.Equal(6, vm.Search.Results.Count); // 도면 5장 + 일반 사항 1장
        Assert.Contains("on 6 pages", vm.Search.Summary);
        Assert.Contains("NEW TRUSS", vm.Search.Results[0].Context);

        vm.Search.NextCommand.Execute(null);
        vm.Search.NextCommand.Execute(null);
        Assert.Equal(1, doc.CurrentPageIndex);
        Assert.Equal(1, vm.Search.CurrentHit!.PageIndex);
        vm.Search.PreviousCommand.Execute(null);
        Assert.Equal(0, doc.CurrentPageIndex);

        vm.Search.Query = "치수";
        await vm.Search.SearchAsync();
        Assert.Single(vm.Search.Results);
        vm.Search.Query = "nothing-here";
        await vm.Search.SearchAsync();
        Assert.Equal("No results", vm.Search.Summary);
    }

    [Fact]
    public async Task Bookmarks_load_and_navigate()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        await vm.Bookmarks.LoadAsync();
        Assert.Equal(6, vm.Bookmarks.Items.Count);
        vm.Bookmarks.Selected = vm.Bookmarks.Items.Single(b => b.Title.StartsWith("S-301"));
        Assert.Equal(2, doc.CurrentPageIndex);
    }

    [Fact]
    public async Task Saved_tool_style_applies_to_drawing_tool()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        Assert.Equal(["Structural Review", "Coordination", "Site Notes"], vm.SavedTools.Sets.Select(s => s.Name));
        var rfi = vm.SavedTools.Tools.Single(t => t.Name == "RFI callout");
        vm.SavedTools.UseCommand.Execute(rfi);
        Assert.Equal(ToolKind.Callout, vm.ActiveTool);
        Assert.Equal("RFI: ", vm.ToolDefaults.Get(ToolKind.Callout).DefaultText);
        Assert.Equal("RFI", vm.ToolDefaults.Get(ToolKind.Callout).Subject);

        Editing.TextEditRequest? req = null;
        vm.Interaction!.TextEditRequested += (_, r) => req = r;
        vm.Interaction.PointerDown(new PagePoint(300, 400), ToolKind.Callout, Editing.PointerModifiers.None, 0.5);
        await vm.Interaction.PointerUpAsync(new PagePoint(500, 300), Editing.PointerModifiers.None);
        Assert.Equal("RFI: ", req!.InitialText);
    }

    [Fact]
    public async Task Saving_selected_shape_with_content_places_copies()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        var cloud = await doc.AddMarkupAsync(0, CloudDraft.FromRect(new PageRect(100, 100, 160, 140)) with { Subject = "Custom" });
        doc.Select(cloud);
        var saved = vm.SavedTools.SaveSelected("My cloud", includeContent: true)!;
        Assert.NotNull(saved.Template);
        Assert.Contains(vm.SavedTools.Tools, t => t.Name == "My cloud");

        vm.SavedTools.UseCommand.Execute(saved);
        Assert.True(vm.Interaction!.IsPlacing);
        vm.Interaction.PointerDown(new PagePoint(500, 500), ToolKind.Select, Editing.PointerModifiers.None, 0.5);
        await vm.Interaction.PointerUpAsync(new PagePoint(500, 500), Editing.PointerModifiers.None);
        var placed = doc.Markups.Single(m => m.Kind == MarkupKind.Cloud && m.Id != cloud.Id);
        var vertices = ((CloudDraft)placed.Draft!).Vertices;
        Assert.Equal(500, vertices.Min(v => v.X), 0); // 모양의 왼쪽 위 = 클릭 점 (구름 볼록 부분 제외)
        Assert.Equal(560, vertices.Max(v => v.X), 0);
        Assert.False(vm.Interaction.IsPlacing);
    }

    [Fact]
    public void Saved_tools_round_trip_through_settings()
    {
        using var dir = new TempDir();
        var vm = new MainViewModel(new MuPdfEngine(), new FakeDialogs());
        vm.SavedTools.AddSetCommand.Execute(null);
        vm.SavedTools.SelectedSet!.Tools.Add(new SavedTool("Mine", ToolKind.Rectangle, MarkupStyle.Default with { LineWidth = 4 }, "S",
            Template: MarkupDraftData.From(new RectangleDraft(new PageRect(0, 0, 10, 10)))));
        var store = new SettingsStore(dir.File("s.json"));
        store.Save(vm.CaptureSettings());
        var vm2 = new MainViewModel(new MuPdfEngine(), new FakeDialogs());
        vm2.ApplySettings(store.Load());
        var mine = vm2.SavedTools.Sets.Last().Tools.Single();
        Assert.Equal(4, mine.Style.LineWidth);
        Assert.IsType<RectangleDraft>(mine.Template!.ToDraft());
    }

    [Theory]
    [InlineData(2592, 1728, PrintScaling.FitToPaper, true, false)]   // ARCH D 가로 → A4 세로: 돌리고 맞춤
    [InlineData(595, 842, PrintScaling.ActualSize, false, false)]    // A4 → A4 실제 크기
    [InlineData(2592, 1728, PrintScaling.ActualSize, true, true)]    // ARCH D 실제 크기 → 잘림 경고
    public void Print_layout(double w, double h, PrintScaling mode, bool rotate, bool clipped)
    {
        double paperW = 8.27 * 96, paperH = 11.69 * 96; // A4 세로
        var p = PrintLayout.Place(w, h, paperW, paperH, mode);
        Assert.Equal(rotate, p.Rotate);
        Assert.Equal(clipped, p.Clipped);
        if (mode == PrintScaling.ActualSize) Assert.Equal(100, p.Percent, 3);
        else Assert.True(p.Percent < 100);
    }

    [Fact]
    public void Page_range_parsing()
    {
        Assert.Equal([0, 1, 2, 4], PrintLayout.ParseRange("1-3, 5", 6));
        Assert.Equal([5], PrintLayout.ParseRange("6-99, x", 6));
        Assert.Empty(PrintLayout.ParseRange("", 6));
    }
}
