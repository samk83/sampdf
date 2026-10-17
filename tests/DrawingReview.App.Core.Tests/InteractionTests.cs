using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdfium;

namespace DrawingReview.App.Core.Tests;

public class InteractionTests
{
    private const double Px = 0.5; // 화면 1픽셀 = 0.5pt (200% 근처)

    private static async Task<(MainViewModel Vm, DocumentViewModel Doc, MarkupInteraction Ui, TempDir Dir)> OpenAsync()
    {
        var dir = new TempDir();
        var vm = new MainViewModel(new PdfiumEngine(), new FakeDialogs());
        var doc = (await vm.OpenPathAsync(dir.WritePlanSet()))!;
        vm.ToolDefaults.Author = "Sam";
        return (vm, doc, vm.Interaction!, dir);
    }

    private static async Task Drag(MarkupInteraction ui, ToolKind tool, PagePoint from, PagePoint to, PointerModifiers mods = PointerModifiers.None)
    {
        ui.PointerDown(from, tool, mods, Px);
        ui.PointerMove(new PagePoint((from.X + to.X) / 2, (from.Y + to.Y) / 2), mods);
        await ui.PointerUpAsync(to, mods);
    }

    [Fact]
    public async Task Dragging_rectangle_creates_markup_with_tool_defaults()
    {
        var (vm, doc, ui, dir) = await OpenAsync();
        using var _ = dir;
        vm.ActiveTool = ToolKind.Rectangle;
        await Drag(ui, ToolKind.Rectangle, new PagePoint(500, 500), new PagePoint(700, 600));
        var m = Assert.Single(doc.Markups, x => x.Kind == MarkupKind.Rectangle);
        Assert.Equal("Sam", m.Author);
        Assert.Equal("Rectangle", m.Subject);
        Assert.Equal(new PageRect(500, 500, 700, 600), ((RectangleDraft)m.Draft!).Rect);
        Assert.Equal(ToolKind.Select, vm.ActiveTool); // 연속 배치 꺼짐 → 선택 도구로 복귀
        Assert.Empty(ui.Previews);
    }

    [Fact]
    public async Task Keep_tool_active_stays_on_tool()
    {
        var (vm, _, ui, dir) = await OpenAsync();
        using var _d = dir;
        vm.KeepToolActive = true;
        vm.ActiveTool = ToolKind.Cloud;
        await Drag(ui, ToolKind.Cloud, new PagePoint(100, 100), new PagePoint(300, 200));
        Assert.Equal(ToolKind.Cloud, vm.ActiveTool);
    }

    [Fact]
    public async Task Click_without_drag_does_not_create_shape()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        ui.PointerDown(new PagePoint(100, 100), ToolKind.Ellipse, PointerModifiers.None, Px);
        await ui.PointerUpAsync(new PagePoint(100.5, 100.5), PointerModifiers.None);
        Assert.DoesNotContain(doc.Markups, m => m.Kind == MarkupKind.Ellipse);
    }

    [Fact]
    public async Task Preview_follows_pointer_and_shift_snaps_line()
    {
        var (_, _, ui, dir) = await OpenAsync();
        using var _d = dir;
        ui.PointerDown(new PagePoint(0, 0), ToolKind.Line, PointerModifiers.None, Px);
        ui.PointerMove(new PagePoint(100, 7), PointerModifiers.Shift);
        var preview = (LineDraft)Assert.Single(ui.Previews);
        Assert.Equal(0, preview.End.Y, 6);
        ui.Cancel();
        Assert.Empty(ui.Previews);
        Assert.False(ui.IsBusy);
    }

    [Fact]
    public async Task Pen_collects_points()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        ui.PointerDown(new PagePoint(100, 100), ToolKind.Pen, PointerModifiers.None, Px);
        for (int i = 1; i <= 20; i++) ui.PointerMove(new PagePoint(100 + i * 5, 100 + (i % 2) * 10), PointerModifiers.None);
        await ui.PointerUpAsync(new PagePoint(210, 100), PointerModifiers.None);
        var ink = (InkDraft)Assert.Single(doc.Markups, m => m.Kind == MarkupKind.Ink).Draft!;
        Assert.True(ink.Strokes[0].Count >= 20);
    }

    [Fact]
    public async Task Text_tools_request_text_then_commit_creates_markup()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        TextEditRequest? req = null;
        ui.TextEditRequested += (_, r) => req = r;
        await Drag(ui, ToolKind.Callout, new PagePoint(300, 400), new PagePoint(500, 300));
        Assert.NotNull(req);
        Assert.Null(req!.Existing);
        Assert.IsType<CalloutDraft>(req.Draft);
        Assert.Equal(new PagePoint(300, 400), ((CalloutDraft)req.Draft).ArrowTip);

        await ui.CommitTextAsync(req, "   "); // 빈 글자는 만들지 않음
        Assert.DoesNotContain(doc.Markups, m => m.Kind == MarkupKind.Callout);
        await ui.CommitTextAsync(req, "VERIFY CLEARANCE");
        var created = Assert.Single(doc.Markups, m => m.Kind == MarkupKind.Callout);
        Assert.Equal("VERIFY CLEARANCE", created.Comment);
        Assert.Same(created, doc.SelectedMarkup);
    }

    [Fact]
    public async Task Click_with_text_box_tool_uses_default_size()
    {
        var (_, _, ui, dir) = await OpenAsync();
        using var _d = dir;
        TextEditRequest? req = null;
        ui.TextEditRequested += (_, r) => req = r;
        ui.PointerDown(new PagePoint(100, 100), ToolKind.TextBox, PointerModifiers.None, Px);
        await ui.PointerUpAsync(new PagePoint(100, 100), PointerModifiers.None);
        Assert.Equal(MarkupInteraction.DefaultTextWidthPixels * Px, req!.TextRect.Width, 3);
    }

    [Fact]
    public async Task Select_click_then_drag_moves_markup()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        var item = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(500, 500, 600, 600)));
        await Drag(ui, ToolKind.Select, new PagePoint(550, 550), new PagePoint(650, 560));
        Assert.Same(item, doc.SelectedMarkup);
        var moved = (RectangleDraft)doc.Markups.Single(m => m.Id == item.Id).Draft!;
        Assert.Equal(600, moved.Rect.X0, 1);
        Assert.Equal(510, moved.Rect.Y0, 1);
        Assert.Equal("Move markup", doc.Undo.UndoName);
    }

    [Fact]
    public async Task Locked_markup_can_be_selected_but_not_moved()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        var item = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(500, 500, 600, 600)));
        await doc.SetLockedAsync([item], true);
        await Drag(ui, ToolKind.Select, new PagePoint(550, 550), new PagePoint(650, 560));
        Assert.Same(doc.Markups.Single(m => m.Id == item.Id), doc.SelectedMarkup);
        Assert.Equal(500, ((RectangleDraft)doc.SelectedMarkup!.Draft!).Rect.X0, 1);
    }

    [Fact]
    public async Task Handle_drag_resizes_selected_markup()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        var item = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(500, 500, 600, 600)));
        doc.Select(item);
        Assert.Equal(HandleKind.BottomRight, ui.HandleUnder(new PagePoint(601, 601), Px));
        await Drag(ui, ToolKind.Select, new PagePoint(600, 600), new PagePoint(700, 650));
        var r = (RectangleDraft)doc.Markups.Single(m => m.Id == item.Id).Draft!;
        Assert.Equal(new PageRect(500, 500, 700, 650), r.Rect);
    }

    [Fact]
    public async Task Rubber_band_selects_markups_inside_and_ctrl_click_toggles()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        var a = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(1000, 1000, 1050, 1050)));
        var b = await doc.AddMarkupAsync(0, new EllipseDraft(new PageRect(1100, 1000, 1150, 1050)));
        await doc.AddMarkupAsync(0, new EllipseDraft(new PageRect(1500, 1500, 1550, 1550)));
        await Drag(ui, ToolKind.Select, new PagePoint(990, 990), new PagePoint(1200, 1100));
        Assert.Equal(2, doc.SelectedMarkups.Count);
        ui.PointerDown(new PagePoint(1125, 1025), ToolKind.Select, PointerModifiers.Control, Px);
        await ui.PointerUpAsync(new PagePoint(1125, 1025), PointerModifiers.Control);
        Assert.Equal([a], doc.SelectedMarkups);
        _ = b;
    }

    [Fact]
    public async Task Double_click_edits_existing_text()
    {
        var (_, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        var item = await doc.AddMarkupAsync(0, new TextBoxDraft(new PageRect(500, 500, 700, 550), "OLD"));
        TextEditRequest? req = null;
        ui.TextEditRequested += (_, r) => req = r;
        ui.DoubleClick(new PagePoint(600, 520), Px);
        Assert.Same(item, req!.Existing);
        Assert.Equal("OLD", req.InitialText);
        await ui.CommitTextAsync(req, "NEW 한글");
        Assert.Equal("NEW 한글", doc.Markups.Single(m => m.Id == item.Id).Comment);
    }

    [Fact]
    public async Task Copy_paste_offsets_on_same_page_and_is_one_undo()
    {
        var (vm, doc, _, dir) = await OpenAsync();
        using var _d = dir;
        doc.Viewport.SetView(new Viewing.ViewSize(1000, 800));
        var a = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(500, 500, 600, 600)));
        var b = await doc.AddMarkupAsync(0, new EllipseDraft(new PageRect(700, 500, 800, 600)));
        doc.SelectMany([a, b]);
        vm.CopyCommand.Execute(null);
        await vm.PasteCommand.ExecuteAsync(null);
        Assert.Equal(5, doc.Markups.Count);
        Assert.Equal(2, doc.SelectedMarkups.Count);
        Assert.True(((RectangleDraft)doc.SelectedMarkups.First(m => m.Kind == MarkupKind.Rectangle).Draft!).Rect.X0 > 500);
        await doc.Undo.UndoAsync();
        Assert.Equal(3, doc.Markups.Count);

        doc.GoToPage(1);
        await vm.PasteCommand.ExecuteAsync(null);
        Assert.Equal(500, ((RectangleDraft)doc.Markups.Single(m => m.PageIndex == 1 && m.Kind == MarkupKind.Rectangle).Draft!).Rect.X0, 1);
    }

    [Fact]
    public async Task Escape_cancels_then_returns_to_select_then_clears_selection()
    {
        var (vm, doc, ui, dir) = await OpenAsync();
        using var _d = dir;
        var item = await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(500, 500, 600, 600)));
        doc.Select(item);
        vm.ActiveTool = ToolKind.Line;
        ui.PointerDown(new PagePoint(0, 0), ToolKind.Line, PointerModifiers.None, Px);
        vm.CancelToolCommand.Execute(null);
        Assert.False(ui.IsBusy);
        Assert.Equal(ToolKind.Line, vm.ActiveTool);
        vm.CancelToolCommand.Execute(null);
        Assert.Equal(ToolKind.Select, vm.ActiveTool);
        vm.CancelToolCommand.Execute(null);
        Assert.Null(doc.SelectedMarkup);
    }
}
