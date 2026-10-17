using ACadSharp.Entities;
using ACadSharp.IO;
using DrawingReview.App.Core.Comparison;
using DrawingReview.Cad;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Pdfium;
using DrawingReview.TestData;
using Rev = DrawingReview.TestData.CadSamples.Rev;

namespace DrawingReview.App.Core.Tests;

/// <summary>비교 창의 CAD(DWG/DXF) 모드.</summary>
public class CadCompareViewModelTests
{
    private static async Task<(CompareViewModel Vm, TempDir Dir, FakeDialogs Dialogs, string OldPath, string NewPath)> OpenAsync(Rev newRev)
    {
        var dir = new TempDir();
        var oldPath = CadSamples.Save(CadSamples.Build(new Rev()), dir.File("S-101 rev A.dxf"));
        var newPath = CadSamples.Save(CadSamples.Build(newRev), dir.File("S-101 rev B.dwg"));
        var dialogs = new FakeDialogs();
        var vm = new CompareViewModel(new PdfiumEngine(), dialogs);
        Assert.True(await vm.OpenCadAsync(oldPath, newPath), string.Join("\n", dialogs.Errors));
        return (vm, dir, dialogs, oldPath, newPath);
    }

    [Fact]
    public async Task Cad_changes_are_listed_with_descriptions_on_identity_alignment()
    {
        var (vm, dir, _, oldPath, newPath) = await OpenAsync(new Rev { DimEndX = 12500, AddLine = true });
        using var _d = dir;
        using var _v = vm;
        Assert.True(vm.IsCadMode);
        Assert.Equal(oldPath, vm.OldPath);       // 화면은 임시 PDF지만 경로는 원래 CAD 파일
        Assert.Equal("S-101 rev B.pdf", vm.NewDocument!.FileName);

        await vm.RunCompareAsync();
        Assert.Equal(2, vm.Changes.Count);
        Assert.Equal(AlignmentTransform.Identity, vm.Alignment!.Transform);
        Assert.NotNull(vm.OverlayImage);
        var dim = vm.Changes.Single(c => c.Description.Contains("Dimension"));
        Assert.Contains("\"6000\" → \"6500\"", dim.Description);
        Assert.Equal(ChangeKind.Modified, dim.Kind);
        Assert.Equal(ChangeKind.Added, vm.Changes.Single(c => c != dim).Kind);
        Assert.Equal(dim.Description, dim.Location);  // 목록 둘째 줄에 설명
        Assert.Contains("Description", vm.BuildCsv());
        Assert.Contains("6500", vm.BuildCsv());
        Assert.Contains("Dimension", vm.BuildJson());
        _ = newPath;
    }

    [Fact]
    public async Task Accepted_changes_export_to_a_dxf_copy_and_pdf()
    {
        var (vm, dir, dialogs, _, newPath) = await OpenAsync(new Rev { DimEndX = 12500, AddLine = true });
        using var _d = dir;
        using var _v = vm;
        await vm.RunCompareAsync();
        var before = File.ReadAllBytes(newPath);

        await vm.ExportCadCloudsAsync();                      // 수락한 것이 없으면 안내만
        Assert.Contains("Accept changes first", vm.Status);

        vm.AcceptAllHighConfidenceCommand.Execute(null);
        dialogs.SavePath = newPath;                          // 원본 덮어쓰기는 거부
        await vm.ExportCadCloudsAsync();
        Assert.Single(dialogs.Errors);

        dialogs.SavePath = dir.File("clouds.dxf");
        await vm.ExportCadCloudsAsync();
        Assert.Equal(before, File.ReadAllBytes(newPath));
        var doc = DxfReader.Read(dialogs.SavePath);
        var clouds = doc.Entities.OfType<LwPolyline>().Where(e => e.Layer?.Name == CadCloudExporter.LayerName).ToList();
        Assert.Equal(2, clouds.Count);
        // 치수 변경 Cloud는 개정 치수(6000~12500, y -2000~-1000)를 감싼다
        Assert.Contains(clouds, p => p.Vertices.Min(v => v.Location.X) < 6000 && p.Vertices.Max(v => v.Location.X) > 12500
                                     && p.Vertices.Min(v => v.Location.Y) < -2000);

        // PDF Cloud와 PDF 내보내기도 기존 방식 그대로 동작
        await vm.CreateCloudsAsync();
        Assert.All(vm.Changes, c => Assert.NotNull(c.CloudMarkupId));
        dialogs.SavePath = dir.File("compared.pdf");
        await vm.ExportPdfAsync();
        Assert.True(File.Exists(dialogs.SavePath));
    }

    [Fact]
    public async Task Cad_project_round_trips()
    {
        var (vm, dir, _, _, _) = await OpenAsync(new Rev { Note = "VERIFY CLEARANCE AT COLUMN" });
        using var _d = dir;
        await vm.RunCompareAsync();
        vm.AcceptCommand.Execute(vm.Changes[0]);
        var project = dir.File("cmp" + CompareProject.Extension);
        vm.SaveProject(project);
        vm.Dispose();

        using var vm2 = new CompareViewModel(new PdfiumEngine(), new FakeDialogs());
        Assert.True(await vm2.LoadProjectAsync(project));
        Assert.True(vm2.IsCadMode);
        var c = Assert.Single(vm2.Changes);
        Assert.Equal(ChangeStatus.Accepted, c.Status);
        Assert.Contains("VERIFY CLEARANCE AT COLUMN", c.Description);
    }

    [Fact]
    public async Task Unreadable_cad_file_shows_error()
    {
        using var dir = new TempDir();
        var bad = dir.File("bad.dwg");
        await File.WriteAllTextAsync(bad, "not a drawing");
        var dialogs = new FakeDialogs();
        using var vm = new CompareViewModel(new PdfiumEngine(), dialogs);
        Assert.False(await vm.OpenCadAsync(bad, bad));
        Assert.Single(dialogs.Errors);
        Assert.False(vm.IsCadMode);
    }
}
