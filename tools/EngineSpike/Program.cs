// 0단계 엔진 검증 실행기.
// 가상 도면을 만들고, 모든 마크업 도구를 적용해 저장한 뒤 다시 열어 결과 PDF·미리보기·보고서를 남긴다.
// 사용법: dotnet run --project tools/EngineSpike -- [저장소 루트 경로]
using System.Diagnostics;
using System.Globalization;
using System.Text;
using DrawingReview.Engine.Documents;
using DrawingReview.Engine.Geometry;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.MuPdf;
using DrawingReview.TestData;
using Pages = DrawingReview.TestData.VirtualDrawings.PlanSetPages;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
var root = Path.GetFullPath(args.Length > 0 ? args[0] : FindRepoRoot());
var samplesDir = Path.Combine(root, "samples", "virtual");
var outDir = Path.Combine(root, "validation", "output");
var previewDir = Path.Combine(outDir, "previews");
Directory.CreateDirectory(samplesDir);
Directory.CreateDirectory(previewDir);

var engine = new MuPdfEngine();
var report = new StringBuilder();
report.AppendLine("# Stage 0 validation run");
report.AppendLine();
report.AppendLine($"- Engine: {engine.Name} (MuPDF.NET {typeof(MuPDF.NET.Document).Assembly.GetName().Version})");
report.AppendLine($"- Runtime: .NET {Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
report.AppendLine($"- Run at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}");
report.AppendLine();

// 1) 가상 도면 생성
var planSetPath = Path.Combine(samplesDir, "plan-set.pdf");
var densePath = Path.Combine(samplesDir, "dense-vector.pdf");
File.WriteAllBytes(planSetPath, VirtualDrawings.CreatePlanSet());
File.WriteAllBytes(densePath, VirtualDrawings.CreateDenseVectorSheet());

// 1-1) 도면 비교 시험용 쌍: 원본 + 리비전 (글자 변경 2곳, 선 추가 1곳, 보 삭제 1곳)
var compareDir = Path.Combine(root, "samples", "compare");
Directory.CreateDirectory(compareDir);
File.WriteAllBytes(Path.Combine(compareDir, "original.pdf"), ComparePairs.Sheet());
File.WriteAllBytes(Path.Combine(compareDir, "revision.pdf"), ComparePairs.Sheet(new ComparePairs.Revision
{
    Note = "VERIFY CLEARANCE AT COLUMN",
    Dimension = "3500",
    AddLine = true,
    RemoveBeam = true,
}));

// 2) 모든 마크업 도구를 페이지마다 적용
var roundTripPath = Path.Combine(outDir, "markup-roundtrip.pdf");
using (var doc = engine.Open(planSetPath))
{
    foreach (var page in new[] { Pages.FloorPlan, Pages.Rotated90, Pages.CroppedA3, Pages.Rotated180, Pages.Rotated270 })
        AddFullToolSet(doc, page);
    AddFullToolSet(doc, Pages.KoreanSpec, scale: 0.45);
    doc.SaveTo(roundTripPath);
}

// 3) 다시 열어 확인 + 미리보기
report.AppendLine("## Files");
report.AppendLine();
foreach (var f in new[] { planSetPath, densePath, roundTripPath })
    report.AppendLine($"- `{Path.GetRelativePath(root, f)}` — {new FileInfo(f).Length / 1024.0:0.0} KB");
report.AppendLine();

using (var doc = engine.Open(roundTripPath))
{
    report.AppendLine("## Markups after save → reopen");
    report.AppendLine();
    report.AppendLine("| Page | Rotation | Kind | PDF subtype | Author | Subject | Comment |");
    report.AppendLine("|---|---|---|---|---|---|---|");
    for (int i = 0; i < doc.PageCount; i++)
    {
        var info = doc.GetPageInfo(i);
        foreach (var m in doc.GetMarkups(i))
            report.AppendLine($"| {i + 1} | {info.Rotation} | {m.Kind} | {m.PdfSubtype} | {m.Author} | {m.Subject} | {Escape(m.Comment)} |");
        var zoom = 1400 / Math.Max(info.Width, info.Height);
        File.WriteAllBytes(Path.Combine(previewDir, $"page-{i + 1}.png"), doc.RenderPagePng(i, zoom));
    }
    report.AppendLine();
}

// 4) 성능 참고치
report.AppendLine("## Performance (reference only — re-measure on the Windows reference PC)");
report.AppendLine();
report.AppendLine("| File | Open | Fit-to-screen render (1600px) | 400% tile render |");
report.AppendLine("|---|---|---|---|");
foreach (var f in new[] { planSetPath, densePath })
{
    var sw = Stopwatch.StartNew();
    using var doc = engine.Open(f);
    var open = sw.ElapsedMilliseconds;
    var info = doc.GetPageInfo(0);
    sw.Restart();
    doc.RenderPagePng(0, 1600 / info.Width);
    var fit = sw.ElapsedMilliseconds;
    sw.Restart();
    doc.RenderPagePng(0, 4, new PageRect(500, 500, 900, 800));
    var tile = sw.ElapsedMilliseconds;
    report.AppendLine($"| {Path.GetFileName(f)} | {open} ms | {fit} ms | {tile} ms |");
}

File.WriteAllText(Path.Combine(outDir, "report.md"), report.ToString());
Console.WriteLine(report);
return 0;

static void AddFullToolSet(IPdfDocument doc, int page, double scale = 1.0)
{
    PagePoint P(double x, double y) => new(x * scale, y * scale);
    PageRect R(double x0, double y0, double x1, double y1) => new(x0 * scale, y0 * scale, x1 * scale, y1 * scale);
    var style = MarkupStyle.Default with { FontSize = Math.Max(8, 14 * scale) };
    var meta = (Author: "Sam", Subject: "Structural");

    MarkupDraft[] drafts =
    [
        CloudDraft.FromRect(R(200, 200, 520, 380)) with { Comment = "Cloud: revision area", Style = style with { LineWidth = 2 } },
        new CalloutDraft(R(560, 160, 960, 230), "VERIFY CLEARANCE BETWEEN NEW TRUSS AND EXISTING BEAM.", P(480, 300), P(540, 240)) { Style = style },
        new TextBoxDraft(R(560, 420, 960, 480), "한글 주석: 간섭 확인 필요") { Style = style },
        new LineDraft(P(200, 440), P(500, 440)) { Comment = "Line", Style = style },
        new LineDraft(P(200, 500), P(500, 560), ArrowAtEnd: true) { Comment = "Arrow", Style = style with { StrokeColor = RgbColor.Blue } },
        new RectangleDraft(R(200, 600, 360, 700)) { Comment = "Rectangle", Style = style },
        new EllipseDraft(R(400, 600, 560, 700)) { Comment = "Ellipse", Style = style },
        new InkDraft([[P(600, 600), P(650, 560), P(700, 640), P(760, 580), P(820, 650)]]) { Comment = "Pen", Style = style with { LineWidth = 2.5 } },
        new HighlightDraft(R(180, 420, 520, 460)) { Comment = "Highlight", Style = MarkupStyle.Highlighter },
    ];
    foreach (var d in drafts)
        doc.AddMarkup(page, d with { Author = meta.Author, Subject = meta.Subject });
}

static string Escape(string? s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DrawingReview.sln"))) dir = dir.Parent;
    return dir?.FullName ?? Directory.GetCurrentDirectory();
}
