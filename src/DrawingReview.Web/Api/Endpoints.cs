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
        // 작업 중 파일이 닫힘 → 404, 웹 모드 서버 메모리 가득 → 503 (원인과 할 일을 알려 줌)
        api.AddEndpointFilter(async (ctx, next) =>
        {
            try { return await next(ctx); }
            catch (DocumentClosedException ex) { return Results.NotFound(Error(ex.Message)); }
            catch (ServerFullException ex) { return Results.Json(Error(ex.Message), statusCode: StatusCodes.Status503ServiceUnavailable); }
        });

        // 서버가 살아 있는지 (배포한 곳의 상태 검사용). 파일 내용은 알려 주지 않는다.
        api.MapGet("/health", (HostingOptions hosting) => Results.Ok(new { status = "ok", mode = hosting.Mode.ToString().ToLowerInvariant() }));

        // 이번 달 사용량 (웹 모드 한도). 화면이 "이번 달 무료 사용량 끝"을 미리 알 수 있게.
        api.MapGet("/usage", (UsageMeter usage) => Results.Ok(usage.Status()));

        api.MapPost("/files", async (HttpRequest request, DocumentStore store, UsageMeter usage) =>
        {
            if (usage.IsOverLimit) return LimitReached(usage);
            if (!request.HasFormContentType) return Results.BadRequest(Error("Send the file as multipart form data."));
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null || file.Length == 0) return Results.BadRequest(Error("No file was sent."));
            if (!DocumentStore.IsSupported(file.FileName)) return Results.BadRequest(Error("Only PDF, PNG, JPG, TIFF and BMP files are supported."));
            var bytes = new byte[file.Length];
            await using (var input = file.OpenReadStream()) await input.ReadExactlyAsync(bytes, request.HttpContext.RequestAborted);
            try
            {
                var doc = await Task.Run(() => store.Add(file.FileName, bytes, Owners.Of(request.HttpContext)));
                return Results.Ok(FileDto.Of(doc));
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not ServerFullException)
            {
                return Results.BadRequest(Error($"Could not open {file.FileName}: {ex.Message}"));
            }
        }).DisableAntiforgery();

        api.MapGet("/files/{id}", (string id, HttpContext http, DocumentStore store) =>
            store.Get(id, Owners.Of(http)) is { } d ? Results.Ok(FileDto.Of(d)) : Results.NotFound(Error("File not found.")));

        api.MapDelete("/files/{id}", (string id, HttpContext http, DocumentStore store) => store.Remove(id, Owners.Of(http)) ? Results.NoContent() : Results.NotFound());

        // 페이지 그림 (표시 없이). scale 1 = 72 dpi.
        api.MapGet("/files/{id}/pages/{page:int}.png", async (string id, int page, double? scale, HttpContext http, DocumentStore store) =>
        {
            if (store.Get(id, Owners.Of(http)) is not { } d) return Results.NotFound();
            if (!await d.EnterAsync(http.RequestAborted)) return Results.NotFound();
            try
            {
                if (page < 0 || page >= d.Document.PageCount) return Results.NotFound();
                var zoom = ClampScale(d.Document.GetPageInfo(page), scale ?? 1);
                var png = await Task.Run(() => d.Document.RenderPagePng(page, zoom, includeMarkups: false));
                return Results.File(png, "image/png");
            }
            finally { d.Gate.Release(); }
        });

        api.MapPost("/compare", async (CompareRequest req, HttpContext http, DocumentStore docs, CompareStore compares, UsageMeter usage, CancellationToken cancel) =>
        {
            if (usage.IsOverLimit) return LimitReached(usage);
            var owner = Owners.Of(http);
            if (docs.Get(req.OriginalId, owner) is not { } original || docs.Get(req.RevisionId, owner) is not { } revision)
                return Results.NotFound(Error("Open both files again."));
            var session = await compares.RunAsync(original, revision, req.IgnoreTitleBlock, cancel);
            return Results.Ok(CompareDto.Of(session));
        });

        api.MapGet("/compare/{id}", (string id, HttpContext http, CompareStore compares) =>
            compares.Get(id, Owners.Of(http)) is { } s ? Results.Ok(CompareDto.Of(s)) : Results.NotFound());

        // 지워진 선 층: 흰 바탕에 지워진 선만 빨강 (리비전 페이지 좌표). 화면에서 곱하기 혼합으로 겹친다.
        api.MapGet("/compare/{id}/pages/{index:int}/removed.png", async (string id, int index, double? scale, HttpContext http, CompareStore compares) =>
        {
            if (compares.Get(id, Owners.Of(http)) is not { } s || index < 0 || index >= s.Result.Pages.Count) return Results.NotFound();
            var pair = s.Result.Pages[index];
            if (pair.Result is not { } result || pair.OldPage is not int op || pair.NewPage is not int np) return Results.NotFound();
            if (compares.Original(s) is not { } original || compares.Revision(s) is not { } revision) return Results.NotFound(Error(FilesGone));
            var png = await CompareStore.WithBothAsync(original, revision, () => Task.Run(() =>
            {
                var zoom = ClampScale(revision.Document.GetPageInfo(np), scale ?? 1);
                var layer = PageComparer.RenderRemovedLayer(PageSource.From(original.Document, op), PageSource.From(revision.Document, np),
                    result.Alignment.Transform, zoom, result.Settings);
                return PageComparer.EncodePng(layer);
            }), http.RequestAborted);
            return Results.File(png, "image/png");
        });

        // 겹쳐 보기(색 구분): 원본만 빨강, 리비전만 파랑, 공통은 회색. 정렬 변환을 반영한다.
        api.MapGet("/compare/{id}/pages/{index:int}/overlay.png", async (string id, int index, double? scale, HttpContext http, CompareStore compares) =>
        {
            if (compares.Get(id, Owners.Of(http)) is not { } s || index < 0 || index >= s.Result.Pages.Count) return Results.NotFound();
            var pair = s.Result.Pages[index];
            if (pair.Result is not { } result || pair.OldPage is not int op || pair.NewPage is not int np) return Results.NotFound();
            if (compares.Original(s) is not { } original || compares.Revision(s) is not { } revision) return Results.NotFound(Error(FilesGone));
            var png = await CompareStore.WithBothAsync(original, revision, () => Task.Run(() =>
            {
                var zoom = ClampScale(revision.Document.GetPageInfo(np), scale ?? 1);
                var image = PageComparer.RenderOverlay(PageSource.From(original.Document, op), PageSource.From(revision.Document, np),
                    result.Alignment.Transform, zoom, inkThreshold: result.Settings.InkThreshold);
                return PageComparer.EncodePng(image);
            }), http.RequestAborted);
            return Results.File(png, "image/png");
        });

        // 표시가 들어간 리비전 PDF. 원본 파일은 그대로 두고, 같은 바이트로 연 사본에 표시를 넣는다.
        api.MapPost("/compare/{id}/export", async (string id, ExportRequest req, HttpContext http, CompareStore compares, DocumentStore docs, HostingOptions hosting, UsageMeter usage) =>
        {
            if (usage.IsOverLimit) return LimitReached(usage);
            if (compares.Get(id, Owners.Of(http)) is not { } s) return Results.NotFound(Error("The comparison is no longer available. Compare again."));
            if (compares.Revision(s) is not { } revision) return Results.NotFound(Error(FilesGone));
            var excluded = new HashSet<string>(req.Excluded ?? []);
            var label = string.IsNullOrWhiteSpace(req.RevisionLabel) ? "1" : req.RevisionLabel.Trim();
            if (label.Length > 4) return Results.BadRequest(Error("The revision number can be up to 4 characters."));
            if (req.Edits?.Values.Any(e => e.Label is { Length: > 4 }) == true) return Results.BadRequest(Error("The revision number can be up to 4 characters."));
            var bytes = await Task.Run(() =>
            {
                using var copy = docs.OpenCopy(revision);
                var options = new RevisionMarkOptions { RevisionLabel = label, Author = hosting.IsWeb ? "Sam Drawing Tools" : Environment.UserName, CloudTextChanges = req.CloudTextChanges };
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

    /// <summary>월 사용량 한도에 닿음 → 429 (원인과 다시 되는 날짜를 알려 줌).</summary>
    private static IResult LimitReached(UsageMeter usage) => Results.Json(Error(usage.LimitMessage()), statusCode: StatusCodes.Status429TooManyRequests);

    private const string FilesGone = "The compared files are no longer open. Open them and compare again.";
}
