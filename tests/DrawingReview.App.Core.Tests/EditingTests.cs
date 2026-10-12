using DrawingReview.App.Core.Editing;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.MuPdf;
using Pages = DrawingReview.TestData.VirtualDrawings.PlanSetPages;

namespace DrawingReview.App.Core.Tests;

public class DraftGeometryTests
{
    private static readonly PageRect Box = new(100, 100, 200, 150);

    [Fact]
    public void Translate_moves_every_point()
    {
        var c = new CalloutDraft(Box, "x", new PagePoint(50, 50), new PagePoint(80, 80));
        var moved = (CalloutDraft)DraftGeometry.Translate(c, 10, -5);
        Assert.Equal(new PageRect(110, 95, 210, 145), moved.TextRect);
        Assert.Equal(new PagePoint(60, 45), moved.ArrowTip);
        Assert.Equal(new PagePoint(90, 75), moved.Knee);
    }

    [Fact]
    public void Resize_scales_points_into_new_bounds()
    {
        var ink = new InkDraft([[new(0, 0), new(10, 10)], [new(5, 0)]]);
        var r = (InkDraft)DraftGeometry.Resize(ink, new PageRect(100, 100, 120, 140));
        Assert.Equal(new PagePoint(100, 100), r.Strokes[0][0]);
        Assert.Equal(new PagePoint(120, 140), r.Strokes[0][1]);
        Assert.Equal(new PagePoint(110, 100), r.Strokes[1][0]);
    }

    [Fact]
    public void Bounds_include_callout_tip()
    {
        var c = new CalloutDraft(Box, "x", new PagePoint(50, 300));
        Assert.Equal(new PageRect(50, 100, 200, 300), DraftGeometry.Bounds(c));
    }

    [Fact]
    public void Hit_test_lines_by_distance_and_shapes_by_area()
    {
        var line = new LineDraft(new PagePoint(0, 0), new PagePoint(100, 0));
        Assert.True(DraftGeometry.HitTest(line, new PagePoint(50, 2), 3));
        Assert.False(DraftGeometry.HitTest(line, new PagePoint(50, 10), 3));
        Assert.True(DraftGeometry.HitTest(new RectangleDraft(Box), new PagePoint(150, 120), 1));
        Assert.False(DraftGeometry.HitTest(new RectangleDraft(Box), new PagePoint(250, 120), 1));
        var callout = new CalloutDraft(Box, "x", new PagePoint(0, 300));
        Assert.True(DraftGeometry.HitTest(callout, new PagePoint(1, 299), 3)); // 지시선 위
        Assert.False(DraftGeometry.HitTest(callout, new PagePoint(20, 120), 3));
    }

    [Fact]
    public void Corner_handle_drag_resizes_and_respects_min_size()
    {
        var r = new RectangleDraft(Box);
        Assert.Equal(HandleKind.BottomRight, DraftGeometry.HandleAt(r, new PagePoint(199, 151), 3));
        var bigger = (RectangleDraft)DraftGeometry.DragHandle(r, HandleKind.BottomRight, new PagePoint(300, 250));
        Assert.Equal(new PageRect(100, 100, 300, 250), bigger.Rect);
        var collapsed = (RectangleDraft)DraftGeometry.DragHandle(r, HandleKind.Left, new PagePoint(500, 120));
        Assert.Equal(4, collapsed.Rect.Width, 6);
    }

    [Fact]
    public void Callout_tip_moves_without_changing_text_box()
    {
        var c = new CalloutDraft(Box, "KEEP", new PagePoint(50, 50));
        var moved = (CalloutDraft)DraftGeometry.DragHandle(c, HandleKind.CalloutTip, new PagePoint(10, 400));
        Assert.Equal(Box, moved.TextRect);
        Assert.Equal("KEEP", moved.Text);
        Assert.Equal(new PagePoint(10, 400), moved.ArrowTip);
        var resized = (CalloutDraft)DraftGeometry.DragHandle(c, HandleKind.Right, new PagePoint(260, 120));
        Assert.Equal(new PagePoint(50, 50), resized.ArrowTip);
        Assert.Equal(260, resized.TextRect.X1);
    }

    [Fact]
    public void Shift_snapping()
    {
        var p = DraftGeometry.SnapAngle(new PagePoint(0, 0), new PagePoint(100, 8));
        Assert.Equal(0, p.Y, 6);
        var d = DraftGeometry.SnapAngle(new PagePoint(0, 0), new PagePoint(100, 90));
        Assert.Equal(d.X, d.Y, 6);
        Assert.Equal(new PagePoint(50, -50), DraftGeometry.SnapSquare(new PagePoint(0, 0), new PagePoint(50, -20)));
    }
}

public class UndoStackTests
{
    [Fact]
    public async Task Undo_redo_order_and_new_action_clears_redo()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        IUndoableAction A(string n) => new DelegateAction(n, () => { log.Add("undo " + n); return Task.CompletedTask; }, () => { log.Add("redo " + n); return Task.CompletedTask; });
        stack.Push(A("1"));
        stack.Push(A("2"));
        Assert.Equal("2", stack.UndoName);
        await stack.UndoAsync();
        await stack.UndoAsync();
        Assert.False(await stack.UndoAsync());
        await stack.RedoAsync();
        stack.Push(A("3"));
        Assert.False(stack.CanRedo);
        Assert.Equal(["undo 2", "undo 1", "redo 1"], log);
    }

    [Fact]
    public void Limit_drops_oldest()
    {
        var stack = new UndoStack(limit: 3);
        for (int i = 0; i < 5; i++) stack.Push(new DelegateAction(i.ToString(), () => Task.CompletedTask, () => Task.CompletedTask));
        Assert.Equal("4", stack.UndoName);
    }
}

public class MarkupEditingTests
{
    private static async Task<(DocumentViewModel Doc, TempDir Dir)> OpenAsync()
    {
        var dir = new TempDir();
        var vm = new MainViewModel(new MuPdfEngine(), new FakeDialogs());
        var doc = (await vm.OpenPathAsync(dir.WritePlanSet()))!;
        return (doc, dir);
    }

    private static readonly RectangleDraft Rect = new(new PageRect(300, 300, 500, 400)) { Author = "Sam", Subject = "Structural" };

    [Fact]
    public async Task Existing_markups_are_loaded_on_open()
    {
        var (doc, dir) = await OpenAsync();
        using var _ = dir;
        var note = Assert.Single(doc.Markups);
        Assert.Equal("Other Reviewer", note.Author);
        Assert.False(note.IsEditable);
        Assert.Equal(1, doc.Pages[0].MarkupCount);
    }

    [Fact]
    public async Task Add_then_undo_redo()
    {
        var (doc, dir) = await OpenAsync();
        using var _ = dir;
        var item = await doc.AddMarkupAsync(0, Rect);
        Assert.Equal(ReviewStatus.Open, item.Status);
        Assert.True(doc.IsModified);
        Assert.Equal(2, doc.Markups.Count);
        Assert.True(doc.UndoCommand.CanExecute(null));

        await doc.Undo.UndoAsync();
        Assert.Single(doc.Markups);
        await doc.Undo.RedoAsync();
        var back = Assert.Single(doc.Markups, m => m.Kind == MarkupKind.Rectangle);
        Assert.Equal(item.Id, back.Id);
    }

    [Fact]
    public async Task Delete_skips_locked_and_foreign_and_undo_restores_everything()
    {
        var (doc, dir) = await OpenAsync();
        using var _ = dir;
        var a = await doc.AddMarkupAsync(0, Rect);
        var b = await doc.AddMarkupAsync(0, new EllipseDraft(new PageRect(10, 10, 50, 50)));
        await doc.SetStatusAsync([a], ReviewStatus.Resolved);
        await doc.SetLockedAsync([b], true);

        var deleted = await doc.DeleteMarkupsAsync(doc.Markups.ToList());
        Assert.Equal(1, deleted); // a만 삭제 (b는 잠김, 기존 메모는 다른 프로그램 주석)
        Assert.DoesNotContain(doc.Markups, m => m.Id == a.Id);

        await doc.Undo.UndoAsync();
        var restored = Assert.Single(doc.Markups, m => m.Id == a.Id);
        Assert.Equal(ReviewStatus.Resolved, restored.Status);
    }

    [Fact]
    public async Task Move_many_is_one_undo_step()
    {
        var (doc, dir) = await OpenAsync();
        using var _ = dir;
        var a = await doc.AddMarkupAsync(0, Rect);
        var b = await doc.AddMarkupAsync(0, new LineDraft(new PagePoint(0, 0), new PagePoint(100, 100)));
        await doc.ReplaceMarkupsAsync(
            [(a, DraftGeometry.Translate(a.Draft!, 100, 0)), (b, DraftGeometry.Translate(b.Draft!, 100, 0))], "Move");
        Assert.Equal(400, ((RectangleDraft)doc.Markups.First(m => m.Id == a.Id).Draft!).Rect.X0, 1);
        Assert.Equal(100, ((LineDraft)doc.Markups.First(m => m.Id == b.Id).Draft!).Start.X, 1);

        await doc.Undo.UndoAsync();
        Assert.Equal(300, ((RectangleDraft)doc.Markups.First(m => m.Id == a.Id).Draft!).Rect.X0, 1);
        Assert.Equal(0, ((LineDraft)doc.Markups.First(m => m.Id == b.Id).Draft!).Start.X, 1);
    }

    [Fact]
    public async Task Bulk_status_change_is_undoable()
    {
        var (doc, dir) = await OpenAsync();
        using var _ = dir;
        var a = await doc.AddMarkupAsync(0, Rect);
        var b = await doc.AddMarkupAsync(1, Rect);
        await doc.SetStatusAsync([a, b], ReviewStatus.InProgress);
        Assert.All(doc.Markups.Where(m => m.IsEditable), m => Assert.Equal(ReviewStatus.InProgress, m.Status));
        await doc.Undo.UndoAsync();
        Assert.All(doc.Markups.Where(m => m.IsEditable), m => Assert.Equal(ReviewStatus.Open, m.Status));
    }

    [Fact]
    public async Task Selection_follows_deletion_and_drives_delete_command()
    {
        var (doc, dir) = await OpenAsync();
        using var _ = dir;
        var a = await doc.AddMarkupAsync(0, Rect);
        Assert.False(doc.DeleteSelectedCommand.CanExecute(null));
        doc.Select(a);
        Assert.Same(a, doc.SelectedMarkup);
        Assert.True(doc.DeleteSelectedCommand.CanExecute(null));
        await doc.DeleteSelectedCommand.ExecuteAsync(null);
        Assert.Null(doc.SelectedMarkup);
        Assert.DoesNotContain(doc.Markups, m => m.Id == a.Id);
    }

    [Fact]
    public async Task Edits_survive_save_and_reopen()
    {
        var dir = new TempDir();
        using var _ = dir;
        var vm = new MainViewModel(new MuPdfEngine(), new FakeDialogs());
        var path = dir.WritePlanSet();
        var doc = (await vm.OpenPathAsync(path))!;
        var a = await doc.AddMarkupAsync(2, Rect);
        await doc.SetStatusAsync([a], ReviewStatus.InProgress);
        Assert.True(await vm.SaveDocumentAsync(doc));
        Assert.False(doc.IsModified);
        await vm.CloseDocumentAsync(doc);

        var again = (await vm.OpenPathAsync(path))!;
        var m = Assert.Single(again.Markups, x => x.Id == a.Id);
        Assert.Equal(ReviewStatus.InProgress, m.Status);
        Assert.Equal(2, m.PageIndex);
    }

    [Fact]
    public void Pdf_date_parsing()
    {
        var d = MarkupItemViewModel.ParsePdfDate("D:20261012083015+09'00'")!.Value;
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 8, 30, 15, TimeSpan.FromHours(9)), d);
        Assert.Null(MarkupItemViewModel.ParsePdfDate("garbage"));
        Assert.NotNull(MarkupItemViewModel.ParsePdfDate("D:2026"[..6] + "1012"));
    }
}
