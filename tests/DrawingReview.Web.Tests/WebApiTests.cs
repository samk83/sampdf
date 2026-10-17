using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DrawingReview.Engine.Markups;
using DrawingReview.Engine.Pdfium;
using DrawingReview.TestData;
using DrawingReview.Web.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using R = DrawingReview.TestData.ComparePairs.Revision;

namespace DrawingReview.Web.Tests;

/// <summary>웹 서버 API: 파일 올리기 → 페이지 그림 → 비교 → 지워진 선 층 → 표시 PDF 내려받기.</summary>
public class WebApiTests(WebApiTests.App factory) : IClassFixture<WebApiTests.App>
{
    public sealed class App : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }

    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);
    private static readonly R Revision = new() { Note = "VERIFY CLEARANCE AT COLUMN", Dimension = "3500", AddLine = true, RemoveBeam = true };

    private async Task<FileDto> Upload(HttpClient client, byte[] bytes, string name)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(name.EndsWith(".png") ? "image/png" : "application/pdf");
        form.Add(content, "file", name);
        var response = await client.PostAsync("/api/files", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<FileDto>(Json))!;
    }

    private async Task<CompareDto> Compare(HttpClient client, FileDto a, FileDto b, bool ignoreTitle = false)
    {
        var response = await client.PostAsJsonAsync("/api/compare", new CompareRequest(a.Id, b.Id, ignoreTitle));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CompareDto>(Json))!;
    }

    [Fact]
    public async Task Upload_render_compare_and_export()
    {
        var client = factory.CreateClient();
        var original = await Upload(client, ComparePairs.Sheet(), "S-101 rev0.pdf");
        var revision = await Upload(client, ComparePairs.Sheet(Revision), "S-101 rev1.pdf");
        Assert.Equal("S-101 rev1.pdf", revision.Name);
        Assert.Single(revision.Pages);
        Assert.Equal(1191, revision.Pages[0].Width, 0);

        var png = await client.GetAsync($"/api/files/{original.Id}/pages/0.png?scale=0.5");
        Assert.Equal("image/png", png.Content.Headers.ContentType!.MediaType);
        Assert.True((await png.Content.ReadAsByteArrayAsync()).Length > 1000);

        var result = await Compare(client, original, revision);
        Assert.Equal(4, result.Total.Total);
        Assert.Equal(2, result.Total.Text);
        Assert.Equal(2, result.Total.Graphic);
        var page = Assert.Single(result.Pages);
        Assert.Contains(page.Changes, c => c.Category == "text" && c.Description.Contains("\"BEAM\" → \"COLUMN\"") && c.Highlights.Count > 0);
        Assert.Contains(page.Changes, c => c.Category == "graphic" && c.Kind == "removed");

        var removed = await client.GetAsync($"/api/compare/{result.Id}/pages/0/removed.png?scale=1");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);

        // 텍스트 변경 하나를 지우고 리비전 "B"로 내려받기
        var excluded = page.Changes.First(c => c.Category == "text");
        var export = await client.PostAsJsonAsync($"/api/compare/{result.Id}/export", new ExportRequest("B", [excluded.Id]));
        Assert.True(export.IsSuccessStatusCode, await export.Content.ReadAsStringAsync());
        Assert.Equal("application/pdf", export.Content.Headers.ContentType!.MediaType);
        Assert.Contains("S-101 rev1 (compared).pdf", export.Content.Headers.ContentDisposition!.ToString());

        using var marked = new PdfiumEngine().Open(await export.Content.ReadAsByteArrayAsync());
        var marks = marked.GetMarkups(0);
        Assert.Equal(2, marks.Count(m => m.Draft is CloudDraft { Intensity: > 0 }));      // 그림 변경 2개
        Assert.Equal(2, marks.Count(m => m.Draft is TextBoxDraft { Text: "B" }));          // Cloud마다 △B
        var keptText = page.Changes.Single(c => c.Category == "text" && c.Id != excluded.Id);
        Assert.Equal(keptText.Highlights.Count, marks.Count(m => m.Draft is HighlightDraft)); // 지운 글자 변경은 빠짐
    }

    [Fact]
    public async Task Export_applies_numbering_cloud_text_and_moved_marks()
    {
        var client = factory.CreateClient();
        var original = await Upload(client, ComparePairs.Sheet(), "a.pdf");
        var revision = await Upload(client, ComparePairs.Sheet(Revision), "b.pdf");
        var result = await Compare(client, original, revision);
        var changes = result.Pages[0].Changes;
        var overlay = await client.GetAsync($"/api/compare/{result.Id}/pages/0/overlay.png?scale=0.5");
        Assert.Equal(HttpStatusCode.OK, overlay.StatusCode);

        // 하나씩 올라가는 번호 + 첫 변경의 Cloud를 옮김
        var edits = changes.Select((c, i) => (c, i)).ToDictionary(x => x.c.Id, x => new MarkEditDto(Label: (x.i + 1).ToString()));
        edits[changes[0].Id] = edits[changes[0].Id] with { Cloud = new RectDto(50, 50, 150, 120), Delta = new PointDto(170, 40) };
        var export = await client.PostAsJsonAsync($"/api/compare/{result.Id}/export", new ExportRequest("1", null, CloudTextChanges: true, Edits: edits));
        Assert.True(export.IsSuccessStatusCode, await export.Content.ReadAsStringAsync());
        using var marked = new PdfiumEngine().Open(await export.Content.ReadAsByteArrayAsync());
        var marks = marked.GetMarkups(0);
        Assert.Equal(4, marks.Count(m => m.Draft is CloudDraft { Intensity: > 0 }));       // 글자 변경도 Cloud
        Assert.Equal(["1", "2", "3", "4"], marks.Select(m => m.Draft).OfType<TextBoxDraft>().Select(t => t.Text).Order());
        Assert.Contains(marks, m => m.Draft is CloudDraft { Intensity: > 0 } c && Math.Abs(c.Vertices.Min(v => v.X) - 50) < 0.5 && Math.Abs(c.Vertices.Max(v => v.Y) - 120) < 0.5);
    }

    [Fact]
    public async Task Png_files_compare_too()
    {
        var client = factory.CreateClient();
        var engine = new PdfiumEngine();
        byte[] Png(R? r) { using var d = engine.Open(ComparePairs.Sheet(r)); return d.RenderPagePng(0, 150 / 72.0, includeMarkups: false); }
        var a = await Upload(client, Png(null), "old.png");
        var b = await Upload(client, Png(new R { AddLine = true }), "new.png");
        Assert.True(b.IsImage);
        var result = await Compare(client, a, b);
        Assert.Equal(1, result.Total.Graphic);
    }

    [Fact]
    public async Task Ignore_title_block_hides_title_changes()
    {
        var client = factory.CreateClient();
        var a = await Upload(client, ComparePairs.Sheet(), "a.pdf");
        var b = await Upload(client, ComparePairs.Sheet(new R { TitleDate = "2026-10-16" }), "b.pdf");
        Assert.True((await Compare(client, a, b)).Total.Total > 0);
        Assert.Equal(0, (await Compare(client, a, b, ignoreTitle: true)).Total.Total);
    }

    [Fact]
    public async Task Bad_requests_are_rejected_with_messages()
    {
        var client = factory.CreateClient();
        using var form = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "notes.docx" } };
        var r1 = await client.PostAsync("/api/files", form);
        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);
        Assert.Contains("Only PDF", await r1.Content.ReadAsStringAsync());

        using var broken = new MultipartFormDataContent { { new ByteArrayContent("not a pdf"u8.ToArray()), "file", "broken.pdf" } };
        var r2 = await client.PostAsync("/api/files", broken);
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
        Assert.Contains("Could not open", await r2.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/compare", new CompareRequest("x", "y"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/files/nope/pages/0.png")).StatusCode);
    }

    [Fact]
    public void Render_scale_is_capped()
    {
        var page = new Engine.Geometry.PageInfo(0, 3000, 2000, 0, null);
        Assert.Equal(2, Endpoints.ClampScale(page, 100), 3);       // 6000px / 3000pt
        Assert.Equal(0.1, Endpoints.ClampScale(page, 0), 3);
    }
}
