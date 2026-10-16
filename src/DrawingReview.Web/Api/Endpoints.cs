using DrawingReview.Compare;
using DrawingReview.Compare.Model;
using DrawingReview.Web.Services;

namespace DrawingReview.Web.Api;

/// <summary>
/// 웹 화면이 부르는 주소 모음 (/api/...).
/// 파일: 올리기 → 페이지 그림 받기. 비교: 실행 → 지워진 선 층 → 표시가 들어간 PDF 내려받기.
/// </summary>
public static class Endpoints
{
    /// <summary>페이지 그림 한 장의 최대 크기 (긴 변 픽셀). 너무 큰 요청으로 메모리를 다 쓰지 않게 막는다.</summary>
    public const int MaxRenderPixels = 6000;

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/files", async (HttpRequest request, DocumentStore store) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(Error("Send the file as multipart form data."));
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null || file.Length == 0) return Results.BadRequest(Error("No file was sent."));
            if (!DocumentStore.IsSupported(file.FileName)) return Results.BadRequest(Error("Only PDF, PNG, JPG, TIFF and BMP files are supported."));
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            try
            {
                var doc = await Task.Run(() => store.Add(file.FileName, ms.ToArray()));
                return Results.Ok(FileDto.Of(doc));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.BadRequest(Error($"Could not open {file.FileName}: {ex.Message}"));
            }
        }).DisableAntiforgery();

        api.MapGet("/files/{id}", (string id, DocumentStore store) =>
            store.Get(id) is { } d ? Results.Ok(FileDto.Of(d)) : Results.NotFound(Error("File not found.")));

        api.MapDelete("/files/{id}", (string id, DocumentStore store) => store.Remove(id) ? Results.NoContent() : Results.NotFound());

        // 페이지 그림 (표시 없이). scale 1 = 72 dpi.
        api.MapGet("/files/{id}/pages/{page:int}.png", async (string id, int page, double? scale, DocumentStore store) =>
        {
            if (store.Get(id) is not { } d) return Results.NotFound();
            await d.Gate.WaitAsync();
            try
            {
                if (page < 0 || page >= d.Document.PageCount) return Results.NotFound();
                var zoom = ClampScale(d.Document.GetPageInfo(page), scale ?? 1);
                var png = await Task.Run(() => d.Document.RenderPagePng(page, zoom, includeMarkups: false));
                return Results.File(png, "image/png");
            }
            finally { d.Gate.Release(); }
        });

        api.MapPost("/compare", async (CompareRequest req, DocumentStore docs, CompareStore compares, CancellationToken cancel) =>
        {
            if (docs.Get(req.OriginalId) is not { } original || docs.Get(req.RevisionId) is not { } revision)
                return Results.NotFound(Error("Open both files again."));
            var session = await compares.RunAsync(original, revision, req.IgnoreTitleBlock, cancel);
            return Results.Ok(CompareDto.Of(session));
        });

        api.MapGet("/compare/{id}", (string id, CompareStore compares) =>
            compares.Get(id) is { } s ? Results.Ok(CompareDto.Of(s)) : Results.NotFound());

        // 지워진 선 층: 흰 바탕에 지워진 선만 빨강 (리비전 페이지 좌표). 화면에서 곱하기 혼합으로 겹친다.
        api.MapGet("/compare/{id}/pages/{index:int}/removed.png", async (string id, int index, double? scale, CompareStore compares) =>
        {
            if (compares.Get(id) is not { } s || index < 0 || index >= s.Result.Pages.Count) return Results.NotFound();
            var pair = s.Result.Pages[index];
            if (pair.Result is not { } result || pair.OldPage is not int op || pair.NewPage is not int np) return Results.NotFound();
            var original = compares.Original(s);
            var revision = compares.Revision(s);
            var png = await CompareStore.WithBothAsync(original, revision, () => Task.Run(() =>
            {
                var zoom = ClampScale(revision.Document.GetPageInfo(np), scale ?? 1);
                var layer = PageComparer.RenderRemovedLayer(PageSource.From(original.Document, op), PageSource.From(revision.Document, np),
                    result.Alignment.Transform, zoom, result.Settings);
                return PageComparer.EncodePng(layer);
            }));
            return Results.File(png, "image/png");
        });

        // 겹쳐 보기(색 구분): 원본만 빨강, 리비전만 파랑, 공통은 회색. 정렬 변환을 반영한다.
        api.MapGet("/compare/{id}/pages/{index:int}/overlay.png", async (string id, int index, double? scale, CompareStore compares) =>
        {
            if (compares.Get(id) is not { } s || index < 0 || index >= s.Result.Pages.Count) return Results.NotFound();
            var pair = s.Result.Pages[index];
            if (pair.Result is not { } result || pair.OldPage is not int op || pair.NewPage is not int np) return Results.NotFound();
            var original = compares.Original(s);
            var revision = compares.Revision(s);
            var png = await CompareStore.WithBothAsync(original, revision, () => Task.Run(() =>
            {
                var zoom = ClampScale(revision.Document.GetPageInfo(np), scale ?? 1);
                var image = PageComparer.RenderOverlay(PageSource.From(original.Document, op), PageSource.From(revision.Document, np),
                    result.Alignment.Transform, zoom, inkThreshold: result.Settings.InkThreshold);
                return PageComparer.EncodePng(image);
            }));
            return Results.File(png, "image/png");
        });

        // 표시가 들어간 리비전 PDF. 원본 파일은 그대로 두고, 같은 바이트로 연 사본에 표시를 넣는다.
        api.MapPost("/compare/{id}/export", async (string id, ExportRequest req, CompareStore compares, DocumentStore docs) =>
        {
            if (compares.Get(id) is not { } s) return Results.NotFound(Error("The comparison is no longer available. Compare again."));
            var revision = compares.Revision(s);
            var excluded = new HashSet<string>(req.Excluded ?? []);
            var label = string.IsNullOrWhiteSpace(req.RevisionLabel) ? "1" : req.RevisionLabel.Trim();
            if (label.Length > 4) return Results.BadRequest(Error("The revision number can be up to 4 characters."));
            if (req.Edits?.Values.Any(e => e.Label is { Length: > 4 }) == true) return Results.BadRequest(Error("The revision number can be up to 4 characters."));
            var bytes = await Task.Run(() =>
            {
                using var copy = docs.OpenCopy(revision);
                var options = new RevisionMarkOptions { RevisionLabel = label, Author = Environment.UserName, CloudTextChanges = req.CloudTextChanges };
                for (int i = 0; i < s.Result.Pages.Count; i++)
                {
                    var pair = s.Result.Pages[i];
                    if (pair.Result is not { } result || pair.NewPage is not int np) continue;
                    var index = i;
                    var keep = result.Regions.Where(r => !excluded.Contains(ChangeDto.IdOf(index, r.Number)));
                    RevisionMarks.Apply(copy, np, keep, options,
                        r => req.Edits != null && req.Edits.TryGetValue(ChangeDto.IdOf(index, r.Number), out var e) ? e.ToEdit() : null);
                }
                var temp = Path.Combine(Path.GetTempPath(), "drawing-compare-" + Guid.NewGuid().ToString("N") + ".pdf");
                try
                {
                    copy.SaveTo(temp);
                    return File.ReadAllBytes(temp);
                }
                finally { File.Delete(temp); }
            });
            return Results.File(bytes, "application/pdf", Path.GetFileNameWithoutExtension(revision.Name) + " (compared).pdf");
        });
    }

    /// <summary>요청한 배율을 0.1~8로, 긴 변이 MaxRenderPixels를 넘지 않게 줄인다.</summary>
    public static double ClampScale(Engine.Geometry.PageInfo page, double scale)
    {
        var s = Math.Clamp(double.IsFinite(scale) ? scale : 1, 0.1, 8);
        var longEdge = Math.Max(page.Width, page.Height);
        return Math.Min(s, MaxRenderPixels / Math.Max(longEdge, 1));
    }

    public static object Error(string message) => new { error = message };
}
