using DrawingReview.Cad;
using DrawingReview.Compare.Model;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;

namespace DrawingReview.App.Core.Comparison;

/// <summary>
/// CAD(DWG/DXF) 비교 한 번의 결과와 화면용 PDF.
/// 두 도면을 같은 좌표 변환으로 PDF로 그려서 기존 비교 화면(겹쳐 보기·Cloud·내보내기)을 그대로 쓴다.
/// 변경 판정은 픽셀이 아니라 CAD 객체 비교(CadComparer) 결과를 쓴다.
/// </summary>
public sealed class CadComparison : IDisposable
{
    /// <summary>변경 영역이 너무 작으면 화면에서 보이지 않으므로 최소 크기(pt)로 넓힌다.</summary>
    public const double MinRegionPt = 8;

    public string OldCadPath { get; }
    public string NewCadPath { get; }
    public CadCompareResult Result { get; }
    public CadPageMapping Mapping { get; }
    public string OldPdfPath { get; }
    public string NewPdfPath { get; }
    private readonly string _workDir;

    private CadComparison(string oldCad, string newCad, CadCompareResult result, CadPageMapping mapping, string oldPdf, string newPdf, string workDir)
    {
        (OldCadPath, NewCadPath, Result, Mapping, OldPdfPath, NewPdfPath, _workDir) = (oldCad, newCad, result, mapping, oldPdf, newPdf, workDir);
    }

    /// <summary>두 CAD 파일을 읽고 비교한 뒤, 화면용 PDF를 임시 폴더에 만든다. 원본은 읽기만 한다.</summary>
    public static CadComparison Run(IPdfEngine engine, string oldPath, string newPath)
    {
        oldPath = Path.GetFullPath(oldPath);
        newPath = Path.GetFullPath(newPath);
        var oldDrawing = CadLoader.Load(oldPath);
        var newDrawing = CadLoader.Load(newPath);
        var result = CadComparer.Compare(oldDrawing, newDrawing);
        var mapping = CadPageMapping.Fit(CadPlotter.Extents(oldDrawing, newDrawing));

        var work = Path.Combine(Path.GetTempPath(), "DrawingReview", "cad-" + Guid.NewGuid().ToString("N"));
        // 화면 탭에 원래 파일 이름이 보이도록 old/new 폴더에 "<이름>.pdf"로 만든다
        var oldPdf = Path.Combine(work, "old", Path.GetFileNameWithoutExtension(oldPath) + ".pdf");
        var newPdf = Path.Combine(work, "new", Path.GetFileNameWithoutExtension(newPath) + ".pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(oldPdf)!);
        Directory.CreateDirectory(Path.GetDirectoryName(newPdf)!);
        engine.WriteVectorPdf(oldPdf, [CadPlotter.Plot(oldDrawing, mapping)]);
        engine.WriteVectorPdf(newPdf, [CadPlotter.Plot(newDrawing, mapping)]);
        return new CadComparison(oldPath, newPath, result, mapping, oldPdf, newPdf, work);
    }

    /// <summary>CAD 변경을 비교 화면의 변경 영역(개정 도면 PDF 좌표)으로 바꾼다. 두 PDF가 같은 좌표라 이전 위치도 같다.</summary>
    public List<ChangeRegion> Regions(IReadOnlyList<PageRect>? ignore = null)
    {
        var list = new List<ChangeRegion>();
        foreach (var c in Result.Changes)
        {
            var r = Mapping.ToPage(c.Bounds);
            var grow = Math.Max(0, (MinRegionPt - Math.Min(r.Width, r.Height)) / 2);
            r = r.Inflate(Math.Max(grow, 2));
            if (ignore != null && ignore.Any(i => i.X0 <= r.X0 && i.Y0 <= r.Y0 && i.X1 >= r.X1 && i.Y1 >= r.Y1)) continue;
            var kind = c.Kind switch
            {
                CadChangeKind.Added => ChangeKind.Added,
                CadChangeKind.Removed => ChangeKind.Removed,
                _ => ChangeKind.Modified,
            };
            list.Add(new ChangeRegion(list.Count + 1, kind, r, r, 0, 0, ChangeConfidence.High)
            {
                Description = $"{c.Description} [{c.Layer}]",
            });
        }
        return list;
    }

    /// <summary>비교 화면의 영역(pt)을 CAD 모델 좌표(mm)로.</summary>
    public Box ToModel(PageRect r) => Mapping.ToModel(r);

    public static bool IsCadFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".dwg" or ".dxf";

    public void Dispose()
    {
        try { Directory.Delete(_workDir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
