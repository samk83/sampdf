using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdfium;

namespace DrawingReview.App.Core.Tests;

public class PropertiesTests
{
    private static async Task<(MainViewModel Vm, DocumentViewModel Doc, TempDir Dir)> OpenAsync()
    {
        var dir = new TempDir();
        var vm = new MainViewModel(new PdfiumEngine(), new FakeDialogs());
        var doc = (await vm.OpenPathAsync(dir.WritePlanSet()))!;
        return (vm, doc, dir);
    }

    [Fact]
    public async Task Shows_nothing_then_tool_defaults_then_selection()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        var p = vm.Properties;
        Assert.Equal(PropertiesViewModel.PropertiesMode.None, p.Mode);
        vm.ActiveTool = ToolKind.Cloud;
        Assert.Equal(PropertiesViewModel.PropertiesMode.ToolDefaults, p.Mode);
        Assert.Contains("Cloud", p.Header);
        var item = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(1, 1, 50, 50)) { Subject = "S1", Author = "Sam" });
        doc.Select(item);
        Assert.Equal(PropertiesViewModel.PropertiesMode.Selection, p.Mode);
        Assert.Equal("S1", p.Subject);
        Assert.Equal("Rectangle · 1 selected", p.Header);
    }

    [Fact]
    public async Task Changing_color_updates_all_selected_in_one_undo_and_remembers_it()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        var a = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(1, 1, 50, 50)));
        var b = await doc.AddMarkupAsync(0, new EllipseDraft(new PageRect(60, 1, 90, 50)));
        doc.SelectMany([a, b]);
        var blue = PropertiesViewModel.Palette.Single(c => c.Name == "Blue");
        vm.Properties.PickColorCommand.Execute(blue);
        await vm.Properties.PendingOperation;
        Assert.All(doc.Markups.Where(m => m.IsEditable), m => Assert.Equal(blue.Color.R, m.Draft!.Style.StrokeColor.R, 2));
        Assert.Equal("Change color", doc.Undo.UndoName);
        Assert.Equal(blue.Color, vm.Properties.RecentColors[0]);
        Assert.Equal(blue.Color, vm.ToolDefaults.Get(ToolKind.Rectangle).Style.StrokeColor); // 다음 사각형에도 적용

        await doc.Undo.UndoAsync();
        Assert.All(doc.Markups.Where(m => m.IsEditable), m => Assert.NotEqual(blue.Color.R, m.Draft!.Style.StrokeColor.R, 2));
    }

    [Fact]
    public async Task Editing_text_and_status_from_panel()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        var t = await doc.AddMarkupAsync(0, new TextBoxDraft(new PageRect(1, 1, 200, 50), "old"));
        doc.Select(t);
        Assert.Equal("Text", vm.Properties.CommentLabel);
        Assert.True(vm.Properties.ShowFontSize);
        vm.Properties.Comment = "새 문구";
        await vm.Properties.PendingOperation;
        Assert.Equal("새 문구", doc.Markups.Single(m => m.Id == t.Id).Comment);
        vm.Properties.Status = ReviewStatus.Resolved;
        await vm.Properties.PendingOperation;
        Assert.Equal(ReviewStatus.Resolved, doc.Markups.Single(m => m.Id == t.Id).Status);
    }

    [Fact]
    public async Task Locked_selection_cannot_change_style_but_can_unlock()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        var a = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(1, 1, 50, 50)));
        await doc.SetLockedAsync([a], true);
        doc.Select(doc.Markups.Single(m => m.Id == a.Id));
        Assert.False(vm.Properties.CanEdit);
        Assert.True(vm.Properties.IsLocked);
        vm.Properties.LineWidth = 6;
        await vm.Properties.PendingOperation;
        Assert.Equal(1.5, doc.Markups.Single(m => m.Id == a.Id).Draft!.Style.LineWidth, 2);
        vm.Properties.IsLocked = false;
        await vm.Properties.PendingOperation;
        Assert.False(doc.Markups.Single(m => m.Id == a.Id).IsLocked);
    }

    [Fact]
    public async Task Tool_default_changes_apply_to_next_markup()
    {
        var (vm, doc, dir) = await OpenAsync();
        using var _ = dir;
        vm.ActiveTool = ToolKind.Line;
        vm.Properties.LineWidth = 4;
        vm.Properties.Subject = "Coordination";
        var ui = vm.Interaction!;
        ui.PointerDown(new PagePoint(10, 10), ToolKind.Line, Editing.PointerModifiers.None, 0.5);
        await ui.PointerUpAsync(new PagePoint(200, 10), Editing.PointerModifiers.None);
        var line = doc.Markups.Single(m => m.Kind == MarkupKind.Line);
        Assert.Equal(4, line.Draft!.Style.LineWidth, 2);
        Assert.Equal("Coordination", line.Subject);
    }
}
