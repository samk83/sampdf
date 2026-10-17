using DrawingReview.App.Core.Comparison;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdfium;
using DrawingReview.TestData;
using R = DrawingReview.TestData.ComparePairs.Revision;

namespace DrawingReview.App.Core.Tests;

public class CompareViewModelTests
{
    private static async Task<(CompareViewModel Vm, TempDir Dir, FakeDialogs Dialogs, string OldPath, string NewPath)> OpenAsync(R revision)
    {
        var dir = new TempDir();
        var oldPath = dir.File("old.pdf");
        var newPath = dir.File("new.pdf");
        await File.WriteAllBytesAsync(oldPath, ComparePairs.Sheet());
        await File.WriteAllBytesAsync(newPath, ComparePairs.Sheet(revision));
        var dialogs = new FakeDialogs();
        var vm = new CompareViewModel(new PdfiumEngine(), dialogs);
        Assert.True(await vm.OpenAsync(oldPath, newPath));
        return (vm, dir, dialogs, oldPath, newPath);
    }

    [Fact]
    public async Task Open_creates_page_pairs_and_compare_finds_changes()
    {
        var (vm, dir, _, _, _) = await OpenAsync(new R { AddFarDetails = true });
        using var _d = dir;
        using var _v = vm;
        Assert.Single(vm.PagePairs);
        Assert.Equal(new PagePair(0, 0), vm.SelectedPair);
        await vm.RunCompareAsync();
        Assert.Equal(3, vm.Changes.Count);
        Assert.True(vm.AlignmentReliable);
        Assert.NotNull(vm.OverlayImage);
        Assert.Equal("3 changes", vm.ChangeCountText);
    }

    [Fact]
    public async Task Accept_dismiss_and_merge_split()
    {
        var (vm, dir, _, _, _) = await OpenAsync(new R { AddFarDetails = true });
        using var _d = dir;
        using var _v = vm;
        await vm.RunCompareAsync();
        vm.AcceptCommand.Execute(vm.Changes[0]);
        vm.DismissCommand.Execute(vm.Changes[1]);
        Assert.Equal("2 changes (1 dismissed)", vm.ChangeCountText);

        vm.Merge([vm.Changes[0], vm.Changes[1]]); // 왼쪽 위 사각형 + 오른쪽 원
        Assert.Equal(2, vm.Changes.Count);
        var merged = vm.Changes.Single(c => c.Status == ChangeStatus.Accepted);
        Assert.True(merged.Bounds.Width > 500); // 멀리 떨어진 두 영역을 감싸는 큰 영역
        await vm.SplitAsync(merged);
        // 최소 원래 두 영역으로 나뉜다 (원은 벽선에 가려진 부분 때문에 더 잘게 나뉠 수 있음)
        Assert.True(vm.Changes.Count >= 3, $"{vm.Changes.Count}");
        Assert.Equal(Enumerable.Range(1, vm.Changes.Count), vm.Changes.Select(c => c.Number));
    }

    [Fact]
    public async Task Clouds_are_created_for_accepted_changes_in_the_copy_only()
    {
        var (vm, dir, dialogs, oldPath, newPath) = await OpenAsync(new R { AddLine = true });
        using var _d = dir;
        using var _v = vm;
        var newBytes = await File.ReadAllBytesAsync(newPath);
        await vm.RunCompareAsync();
        await vm.CreateCloudsAsync();
        Assert.Contains("Accept changes first", vm.Status);

        vm.AcceptAllHighConfidenceCommand.Execute(null);
        await vm.CreateCloudsAsync();
        var change = Assert.Single(vm.Changes);
        Assert.NotNull(change.CloudMarkupId);
        var cloud = vm.NewDocument!.Markups.Single(m => m.Id == change.CloudMarkupId);
        Assert.Equal(MarkupKind.Cloud, cloud.Kind);
        Assert.True(cloud.Bounds.Inflate(1).Contains(change.Bounds)); // 구름이 변경 영역을 감싼다 (기획서 12장 11)
        Assert.Contains(vm.NewDocument.Markups, m => m.Kind == MarkupKind.TextBox && m.Comment == "1"); // 번호
        await vm.CreateCloudsAsync(); // 다시 눌러도 중복 생성 안 함
        Assert.Single(vm.NewDocument.Markups, m => m.Kind == MarkupKind.Cloud);

        // 원본 덮어쓰기 거부
        dialogs.SavePath = newPath;
        await vm.ExportPdfAsync();
        Assert.Single(dialogs.Errors);
        Assert.Equal(newBytes, await File.ReadAllBytesAsync(newPath));

        // 새 파일로 내보내기 → 구름이 표준 주석으로 들어 있음 (기획서 12장 15)
        var outPath = dir.File("compared.pdf");
        dialogs.SavePath = outPath;
        await vm.ExportPdfAsync();
        using var exported = new PdfiumEngine().Open(outPath);
        var cloudOut = Assert.Single(exported.GetMarkups(0), m => m.Kind == MarkupKind.Cloud);
        Assert.True(cloudOut.Bounds.Inflate(1).Contains(change.Bounds));
        Assert.Equal(newBytes, await File.ReadAllBytesAsync(newPath));
        Assert.True(File.Exists(oldPath));
    }

    [Fact]
    public async Task Rectangle_shape_and_margin_options_apply()
    {
        var (vm, dir, _, _, _) = await OpenAsync(new R { AddLine = true });
        using var _d = dir;
        using var _v = vm;
        vm.CloudOptions = new CloudOptions { Shape = CloudShape.Rectangle, MarginPt = 20, Numbering = false, Color = RgbColor.Blue };
        await vm.RunCompareAsync();
        vm.AcceptCommand.Execute(vm.Changes[0]);
        await vm.CreateCloudsAsync();
        var rect = Assert.Single(vm.NewDocument!.Markups, m => m.Kind == MarkupKind.Rectangle);
        Assert.True(rect.Bounds.X0 <= vm.Changes[0].Bounds.X0 - 19);
        Assert.Equal(RgbColor.Blue, rect.Draft!.Style.StrokeColor);
        Assert.DoesNotContain(vm.NewDocument.Markups, m => m.Kind == MarkupKind.TextBox);
    }

    [Fact]
    public async Task Change_list_exports_csv_and_json()
    {
        var (vm, dir, dialogs, _, _) = await OpenAsync(new R { Dimension = "3500" });
        using var _d = dir;
        using var _v = vm;
        await vm.RunCompareAsync();
        dialogs.SavePath = dir.File("changes.csv");
        await vm.ExportChangeListAsync();
        var csv = await File.ReadAllTextAsync(dir.File("changes.csv"));
        Assert.Contains("Number,Kind,Confidence,Status", csv);
        Assert.Contains("1,Modified,High,Pending", csv);
        dialogs.SavePath = dir.File("changes.json");
        await vm.ExportChangeListAsync();
        Assert.Contains("\"Kind\": \"Modified\"", await File.ReadAllTextAsync(dir.File("changes.json")));
        dialogs.SavePath = dir.File("cmp.png");
        await vm.ExportImageAsync();
        Assert.Equal(0x89, (await File.ReadAllBytesAsync(dir.File("cmp.png")))[0]); // PNG 서명
    }

    [Fact]
    public async Task Project_save_and_reopen_restores_review_state()
    {
        var (vm, dir, _, _, _) = await OpenAsync(new R { AddFarDetails = true });
        using var _d = dir;
        await vm.RunCompareAsync();
        vm.AcceptCommand.Execute(vm.Changes[0]);
        vm.DismissCommand.Execute(vm.Changes[2]);
        vm.ToggleVisibilityCommand.Execute(vm.Changes[1]);
        var project = dir.File("review" + CompareProject.Extension);
        vm.SaveProject(project);
        var alignment = vm.Alignment!;
        vm.Dispose();

        var vm2 = new CompareViewModel(new PdfiumEngine(), new FakeDialogs());
        using var _v2 = vm2;
        Assert.True(await vm2.LoadProjectAsync(project));
        Assert.Equal(3, vm2.Changes.Count);
        Assert.Equal(ChangeStatus.Accepted, vm2.Changes[0].Status);
        Assert.Equal(ChangeStatus.Dismissed, vm2.Changes[2].Status);
        Assert.False(vm2.Changes[1].IsVisible);
        Assert.Equal(alignment.Method, vm2.Alignment!.Method);
    }

    [Fact]
    public async Task Manual_alignment_with_four_clicks_reruns_comparison()
    {
        var dir = new TempDir();
        using var _d = dir;
        var oldPath = dir.File("old.pdf");
        var newPath = dir.File("new.pdf");
        var oldBytes = ComparePairs.Sheet();
        await File.WriteAllBytesAsync(oldPath, oldBytes);
        await File.WriteAllBytesAsync(newPath, ComparePairs.Transformed(oldBytes, 0.9, 0, 40, 30));
        using var vm = new CompareViewModel(new PdfiumEngine(), new FakeDialogs());
        await vm.OpenAsync(oldPath, newPath);

        vm.BeginManualAlignmentCommand.Execute(null);
        Assert.Equal(CompareViewMode.SideBySide, vm.Mode);
        Assert.Contains("OLD", vm.ManualAlignmentHint);
        await vm.AddAlignmentPointAsync(isOld: false, new PagePoint(1, 1)); // 순서가 틀리면 무시
        Assert.Empty(vm.ManualPoints);
        await vm.AddAlignmentPointAsync(true, new PagePoint(100, 62));
        await vm.AddAlignmentPointAsync(false, new PagePoint(100 * 0.9 + 40, 62 * 0.9 + 30));
        await vm.AddAlignmentPointAsync(true, new PagePoint(1060, 650));
        await vm.AddAlignmentPointAsync(false, new PagePoint(1060 * 0.9 + 40, 650 * 0.9 + 30));
        Assert.False(vm.IsPickingAlignment);
        Assert.Equal(AlignmentMethod.Manual, vm.Alignment!.Method);
        Assert.Equal(0.9, vm.Alignment.Transform.Scale, 3);
        Assert.DoesNotContain(vm.Changes, c => c.Confidence == ChangeConfidence.High);
    }

    [Fact]
    public async Task Manual_cloud_region_can_be_added()
    {
        var (vm, dir, _, _, _) = await OpenAsync(new R());
        using var _d = dir;
        using var _v = vm;
        await vm.RunCompareAsync();
        Assert.Empty(vm.Changes);
        var item = vm.AddManualChange(new PageRect(100, 100, 200, 150));
        Assert.Equal(ChangeStatus.Accepted, item.Status);
        await vm.CreateCloudsAsync();
        Assert.Single(vm.NewDocument!.Markups, m => m.Kind == MarkupKind.Cloud);
    }
}
