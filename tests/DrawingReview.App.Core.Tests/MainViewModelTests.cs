using DrawingReview.App.Core.Tools;
using DrawingReview.App.Core.ViewModels;
using DrawingReview.Engine.MuPdf;
using DrawingReview.TestData;
using MuPDF.NET;

namespace DrawingReview.App.Core.Tests;

public class MainViewModelTests
{
    private static MainViewModel Make(FakeDialogs dialogs) => new(new MuPdfEngine(), dialogs);

    [Fact]
    public async Task Open_adds_tab_with_pages_and_activates_it()
    {
        using var dir = new TempDir();
        var vm = Make(new FakeDialogs());
        var doc = await vm.OpenPathAsync(dir.WritePlanSet());

        Assert.NotNull(doc);
        Assert.Same(doc, vm.ActiveDocument);
        Assert.Equal(VirtualDrawings.PlanSetPages.Count, doc!.Pages.Count);
        Assert.Equal("plan-set.pdf", doc.Title);
        Assert.Contains("plan-set.pdf", vm.WindowTitle);
        Assert.True(doc.Pages[0].IsCurrent);
    }

    [Fact]
    public async Task Opening_same_file_twice_reuses_tab()
    {
        using var dir = new TempDir();
        var path = dir.WritePlanSet();
        var vm = Make(new FakeDialogs());
        var first = await vm.OpenPathAsync(path);
        await vm.OpenPathAsync(dir.WritePlanSet("other.pdf"));
        var again = await vm.OpenPathAsync(path);
        Assert.Same(first, again);
        Assert.Equal(2, vm.Documents.Count);
        Assert.Same(first, vm.ActiveDocument);
    }

    [Fact]
    public async Task Broken_file_shows_error_and_adds_no_tab()
    {
        using var dir = new TempDir();
        var path = dir.File("broken.pdf");
        File.WriteAllText(path, "this is not a pdf");
        var dialogs = new FakeDialogs();
        var vm = Make(dialogs);
        Assert.Null(await vm.OpenPathAsync(path));
        Assert.Empty(vm.Documents);
        Assert.Single(dialogs.Errors);
    }

    [Fact]
    public async Task Password_is_requested_until_correct_or_cancelled()
    {
        using var dir = new TempDir();
        var path = dir.File("locked.pdf");
        using (var raw = new Document(stream: VirtualDrawings.CreatePlanSet(), fileType: "pdf"))
            raw.Save(path, encryption: (int)PdfCrypt.PDF_ENCRYPT_AES_256, ownerPW: "o", userPW: "u");

        var dialogs = new FakeDialogs();
        dialogs.Passwords.Enqueue("wrong");
        dialogs.Passwords.Enqueue("u");
        var vm = Make(dialogs);
        Assert.NotNull(await vm.OpenPathAsync(path));

        var cancelled = Make(new FakeDialogs());
        Assert.Null(await cancelled.OpenPathAsync(path));
        Assert.Empty(cancelled.Documents);
    }

    [Fact]
    public async Task Closing_modified_document_asks_and_cancel_keeps_it_open()
    {
        using var dir = new TempDir();
        var dialogs = new FakeDialogs();
        var vm = Make(dialogs);
        var doc = (await vm.OpenPathAsync(dir.WritePlanSet()))!;
        doc.IsModified = true;
        Assert.Equal("plan-set.pdf *", doc.Title);

        dialogs.ConfirmAnswers.Enqueue(null); // 취소
        Assert.False(await vm.CloseDocumentAsync(doc));
        Assert.Single(vm.Documents);

        dialogs.ConfirmAnswers.Enqueue(false); // 저장하지 않고 닫기
        Assert.True(await vm.CloseDocumentAsync(doc));
        Assert.Empty(vm.Documents);
        Assert.Null(vm.ActiveDocument);
    }

    [Fact]
    public async Task Closing_active_tab_activates_neighbor()
    {
        using var dir = new TempDir();
        var vm = Make(new FakeDialogs());
        var a = (await vm.OpenPathAsync(dir.WritePlanSet("a.pdf")))!;
        var b = (await vm.OpenPathAsync(dir.WritePlanSet("b.pdf")))!;
        Assert.Same(b, vm.ActiveDocument);
        await vm.CloseDocumentAsync(b);
        Assert.Same(a, vm.ActiveDocument);
    }

    [Fact]
    public void Tool_selection_and_escape()
    {
        var vm = Make(new FakeDialogs());
        vm.SelectToolCommand.Execute(ToolKind.Cloud);
        Assert.Equal("Cloud", vm.ActiveToolName);
        vm.CancelToolCommand.Execute(null);
        Assert.Equal(ToolKind.Select, vm.ActiveTool);
        Assert.Equal(ToolKind.Callout, ToolInfo.FromShortcut('q'));
    }
}

public class DocumentViewModelTests
{
    private static async Task<(MainViewModel Vm, DocumentViewModel Doc, TempDir Dir)> OpenAsync()
    {
        var dir = new TempDir();
        var vm = new MainViewModel(new MuPdfEngine(), new FakeDialogs());
        var doc = (await vm.OpenPathAsync(dir.WritePlanSet()))!;
        return (vm, doc, dir);
    }

    [Fact]
    public async Task Page_navigation_updates_current_flags_and_commands()
    {
        var (_, doc, dir) = await OpenAsync();
        using var _ = dir;
        Assert.False(doc.PreviousPageCommand.CanExecute(null));
        doc.NextPageCommand.Execute(null);
        Assert.Equal(1, doc.CurrentPageIndex);
        Assert.False(doc.Pages[0].IsCurrent);
        Assert.True(doc.Pages[1].IsCurrent);
        doc.LastPageCommand.Execute(null);
        Assert.False(doc.NextPageCommand.CanExecute(null));
        Assert.StartsWith("Page 6 of 6", doc.PageStatusText);
        doc.GoToPage(99);
        Assert.Equal(5, doc.CurrentPageIndex);
    }

    [Fact]
    public async Task Changing_page_resets_viewport_to_new_page_size()
    {
        var (_, doc, dir) = await OpenAsync();
        using var _ = dir;
        doc.Viewport.SetView(new Viewing.ViewSize(1200, 800));
        doc.GoToPage(VirtualDrawings.PlanSetPages.KoreanSpec);
        Assert.Equal(VirtualDrawings.A4Width, doc.Viewport.PageWidth, 1);
        Assert.Equal(VirtualDrawings.A4Height, doc.Viewport.PageHeight, 1);
    }

    [Fact]
    public async Task Preview_is_cached_and_region_render_matches_request()
    {
        var (_, doc, dir) = await OpenAsync();
        using var _ = dir;
        var a = await doc.RenderPreviewAsync(0);
        var b = await doc.RenderPreviewAsync(0);
        Assert.Same(a, b);
        Assert.InRange(Math.Max(a.Width, a.Height), DocumentViewModel.PreviewLongEdge - 2, DocumentViewModel.PreviewLongEdge + 2);

        doc.InvalidateRenderCache(0);
        var c = await doc.RenderPreviewAsync(0);
        Assert.NotSame(a, c);

        var tile = await doc.RenderRegionAsync(0, new Engine.Geometry.PageRect(100, 100, 300, 200), 3);
        Assert.InRange(tile.Width, 599, 601);
        Assert.InRange(tile.Region.X0, 99.5, 100.5);
    }

    [Fact]
    public async Task Thumbnails_load_for_every_page()
    {
        var (_, doc, dir) = await OpenAsync();
        using var _ = dir;
        await doc.LoadThumbnailsAsync();
        Assert.All(doc.Pages, p => Assert.NotNull(p.Image));
        Assert.All(doc.Pages, p => Assert.InRange(Math.Max(p.Image!.Width, p.Image.Height), 178, 182));
    }

    [Fact]
    public async Task Concurrent_renders_are_serialized_safely()
    {
        var (_, doc, dir) = await OpenAsync();
        using var _ = dir;
        var tasks = Enumerable.Range(0, 12)
            .Select(i => doc.RenderRegionAsync(i % doc.Pages.Count, new Engine.Geometry.PageRect(0, 0, 200, 200), 1 + i % 3))
            .ToList();
        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.Width > 0));
    }
}
