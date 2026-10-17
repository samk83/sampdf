using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdfium;

namespace DrawingReview.App.Core.Tests;

public class ReviewListTests
{
    private static async Task<(MainViewModel Vm, DocumentViewModel Doc, TempDir Dir, FakeDialogs Dialogs)> OpenWithMarkupsAsync()
    {
        var dir = new TempDir();
        var dialogs = new FakeDialogs();
        var vm = new MainViewModel(new PdfiumEngine(), dialogs);
        var doc = (await vm.OpenPathAsync(dir.WritePlanSet()))!;
        await doc.AddMarkupAsync(0, new RectangleDraft(new PageRect(500, 500, 600, 600)) { Subject = "Structural", Comment = "check beam", Author = "Sam" });
        var b = await doc.AddMarkupAsync(0, new CloudDraft([new(100, 100), new(200, 100), new(200, 200)], 2) { Subject = "Coordination", Comment = "duct, beam", Author = "Kim" });
        var c = await doc.AddMarkupAsync(2, new TextBoxDraft(new PageRect(50, 50, 200, 90), "한글 주석") { Subject = "Structural", Author = "Sam" });
        await doc.SetStatusAsync([b], ReviewStatus.Resolved);
        await doc.SetStatusAsync([c], ReviewStatus.InProgress);
        return (vm, doc, dir, dialogs);
    }

    [Fact]
    public async Task Lists_all_markups_in_reading_order()
    {
        var (vm, _, dir, _) = await OpenWithMarkupsAsync();
        using var _d = dir;
        var list = vm.ReviewList;
        Assert.Equal(4, list.TotalCount); // 기존 메모 + 새 3개
        Assert.Equal(4, list.Items.Count);
        // 같은 페이지 안에서는 위→아래: cloud(y100) < note(y300) < rect(y500)
        Assert.Equal(["Cloud", "Text", "Rectangle", "Text Box"], list.Items.Select(i => i.KindName));
        Assert.Equal("4 markups", list.SummaryText);
    }

    [Fact]
    public async Task Filters_report_hidden_count()
    {
        var (vm, _, dir, _) = await OpenWithMarkupsAsync();
        using var _d = dir;
        var list = vm.ReviewList;
        list.StatusFilter = ReviewListViewModel.NotResolved;
        Assert.Equal(3, list.VisibleCount);
        Assert.Equal("Showing 3 of 4 — 1 hidden by filter", list.SummaryText);

        list.StatusFilter = ReviewListViewModel.AllStatuses;
        list.TextFilter = "beam";
        Assert.Equal(2, list.VisibleCount);
        list.TextFilter = "한글";
        Assert.Single(list.Items);

        list.ClearFiltersCommand.Execute(null);
        list.CurrentPageOnly = true;
        Assert.Equal(3, list.VisibleCount);
        Assert.True(list.HasHidden);
    }

    [Fact]
    public async Task Sorting_by_column_and_toggling_direction()
    {
        var (vm, _, dir, _) = await OpenWithMarkupsAsync();
        using var _d = dir;
        var list = vm.ReviewList;
        list.SortByCommand.Execute(ReviewSortColumn.Author);
        Assert.Equal(["Kim", "Other Reviewer", "Sam", "Sam"], list.Items.Select(i => i.Author));
        list.SortByCommand.Execute(ReviewSortColumn.Author);
        Assert.True(list.SortDescending);
        Assert.Equal("Sam", list.Items[0].Author);
    }

    [Fact]
    public async Task List_updates_when_markups_change()
    {
        var (vm, doc, dir, _) = await OpenWithMarkupsAsync();
        using var _d = dir;
        await doc.AddMarkupAsync(1, new EllipseDraft(new PageRect(1, 1, 20, 20)));
        Assert.Equal(5, vm.ReviewList.TotalCount);
        await doc.Undo.UndoAsync();
        Assert.Equal(4, vm.ReviewList.TotalCount);
    }

    [Fact]
    public async Task Row_selection_selects_markup_and_navigates_to_its_page()
    {
        var (vm, doc, dir, _) = await OpenWithMarkupsAsync();
        using var _d = dir;
        doc.Viewport.SetView(new Viewing.ViewSize(1200, 800));
        var textBox = vm.ReviewList.Items.Single(i => i.Kind == MarkupKind.TextBox);
        vm.ReviewList.OnRowsSelected([textBox]);
        Assert.Same(textBox, doc.SelectedMarkup);
        Assert.Equal(2, doc.CurrentPageIndex);
        var visible = doc.Viewport.VisiblePageRegion()!.Value;
        Assert.True(visible.X0 <= textBox.Bounds.Center.X && textBox.Bounds.Center.X <= visible.X1);
    }

    [Fact]
    public async Task Canvas_selection_is_mirrored_and_bulk_status_applies_to_selection()
    {
        var (vm, doc, dir, _) = await OpenWithMarkupsAsync();
        using var _d = dir;
        var editable = doc.Markups.Where(m => m.IsEditable).ToList();
        doc.SelectMany(editable);
        Assert.Equal(3, vm.ReviewList.SelectedItems.Count);
        await vm.ReviewList.SetStatusCommand.ExecuteAsync(ReviewStatus.Resolved);
        Assert.All(editable, m => Assert.Equal(ReviewStatus.Resolved, m.Status));
        await doc.Undo.UndoAsync();
        Assert.Equal(ReviewStatus.Open, editable.Single(m => m.Kind == MarkupKind.Rectangle).Status);
    }

    [Fact]
    public async Task Csv_export_includes_header_and_escapes_fields()
    {
        var (vm, _, dir, dialogs) = await OpenWithMarkupsAsync();
        using var _d = dir;
        var path = dir.File("report.csv");
        dialogs.SavePath = path;
        vm.ReviewList.StatusFilter = ReviewListViewModel.NotResolved;
        await vm.ReviewList.ExportCsvCommand.ExecuteAsync(null);
        var text = await File.ReadAllTextAsync(path);
        Assert.StartsWith("Document,plan-set.pdf", text.TrimStart('﻿'));
        Assert.Contains("1 markups hidden by the current filter are not included", text);
        Assert.Contains("ID,Page,Page label,Type,Subject,Comment,Author,Status,Modified", text);
        Assert.Contains("한글 주석", text);
        Assert.DoesNotContain("\"duct, beam\"", text); // Resolved라 제외됨
        var csv = ReviewListViewModel.BuildCsv(vm.ReviewList.Items.Take(0), "a,b.pdf", DateTimeOffset.Now);
        Assert.StartsWith("Document,\"a,b.pdf\"", csv);
    }
}
