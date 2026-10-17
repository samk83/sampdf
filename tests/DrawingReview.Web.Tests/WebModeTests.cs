using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DrawingReview.Engine.MuPdf;
using DrawingReview.TestData;
using DrawingReview.Web.Api;
using DrawingReview.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DrawingReview.Web.Tests;

/// <summary>웹 모드 (설계서 11-4 W0): 사용자마다 파일을 나누고, 오래 안 쓴 파일은 지운다.</summary>
public class WebModeTests(WebModeTests.WebApp factory) : IClassFixture<WebModeTests.WebApp>
{
    public sealed class WebApp : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Hosting:Mode", "Web");
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    private static async Task<FileDto> Upload(HttpClient client, byte[] bytes, string name)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "file", name);
        var response = await client.PostAsync("/api/files", form);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<FileDto>(Json))!;
    }

    [Fact]
    public async Task Another_browser_cannot_see_my_files_or_comparisons()
    {
        var me = factory.CreateClient();
        var other = factory.CreateClient();
        var a = await Upload(me, ComparePairs.Sheet(), "a.pdf");
        var b = await Upload(me, ComparePairs.Sheet(new ComparePairs.Revision { AddLine = true }), "b.pdf");
        var compare = await me.PostAsJsonAsync("/api/compare", new CompareRequest(a.Id, b.Id));
        Assert.True(compare.IsSuccessStatusCode);
        var result = (await compare.Content.ReadFromJsonAsync<CompareDto>(Json))!;

        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync($"/api/files/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/files/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/files/{a.Id}/pages/0.png")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/compare/{result.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/compare", new CompareRequest(a.Id, b.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/compare/{result.Id}/export", new ExportRequest("1", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/files/{a.Id}")).StatusCode);
        // 다른 사람이 지우려 해도 내 파일은 그대로
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync($"/api/files/{a.Id}")).StatusCode);
    }

    [Fact]
    public async Task Owner_cookie_is_http_only_and_security_headers_are_sent()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"mode\":\"web\"", await response.Content.ReadAsStringAsync());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(Owners.CookieName + "=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age", cookie, StringComparison.OrdinalIgnoreCase);   // 브라우저를 닫을 때까지
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task A_made_up_owner_cookie_is_replaced()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Cookie", $"{Owners.CookieName}=../../etc");
        var response = await client.SendAsync(request);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Matches($"^{Owners.CookieName}=[0-9a-f]{{32}};", cookie);
    }

    private sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Idle_files_are_removed_and_used_files_stay()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero));
        using var store = new DocumentStore(new MuPdfEngine(), time);
        var old = store.Add("old.pdf", ComparePairs.Sheet(), "u1");
        var used = store.Add("used.pdf", ComparePairs.Sheet(), "u1");
        time.Now += TimeSpan.FromMinutes(20);
        Assert.NotNull(store.Get(used.Id, "u1"));          // 쓰면 시간이 새로 시작
        time.Now += TimeSpan.FromMinutes(20);
        Assert.Equal(1, store.RemoveIdle(time.Now - TimeSpan.FromMinutes(30)));
        Assert.Null(store.Get(old.Id, "u1"));
        Assert.NotNull(store.Get(used.Id, "u1"));
    }

    [Fact]
    public void Each_owner_has_their_own_file_limit()
    {
        using var store = new DocumentStore(new MuPdfEngine(), TimeProvider.System);
        var bytes = ComparePairs.Sheet();
        var mine = store.Add("mine.pdf", bytes, "u1");
        for (int i = 0; i < DocumentStore.MaxDocuments + 2; i++) store.Add($"x{i}.pdf", bytes, "u2");
        Assert.NotNull(store.Get(mine.Id, "u1"));             // 다른 사람이 많이 올려도 내 파일은 남는다
        Assert.Equal(DocumentStore.MaxDocuments + 1, store.Count);
    }

    [Fact]
    public async Task A_request_waiting_for_a_closed_file_ends_instead_of_hanging()
    {
        using var store = new DocumentStore(new MuPdfEngine(), TimeProvider.System);
        var doc = store.Add("a.pdf", ComparePairs.Sheet(), "u1");
        Assert.True(await doc.EnterAsync());                 // 다른 요청이 쓰는 중
        var waiting = doc.EnterAsync();                        // 지우기 전부터 기다리던 요청
        Assert.True(store.Remove(doc.Id, "u1"));               // 그 사이 지움
        doc.Gate.Release();                                    // 쓰던 요청 끝
        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));   // 먼저 기다린 요청은 아직 열린 문서로 끝냄
        Assert.False(doc.Closed);
        doc.Gate.Release();
        // 그 뒤 오는 요청은 멈추지 않고 "닫힘"을 받는다
        Assert.False(await doc.EnterAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(doc.Closed);
    }

    [Fact]
    public void A_full_server_refuses_new_files_instead_of_closing_other_peoples_files()
    {
        var bytes = ComparePairs.Sheet();
        using var store = new DocumentStore(new MuPdfEngine(), TimeProvider.System) { MaxTotalBytes = bytes.Length * 2L };
        var mine = store.Add("a.pdf", bytes, "u1");
        store.Add("b.pdf", bytes, "u2");
        Assert.Throws<ServerFullException>(() => store.Add("c.pdf", bytes, "u3"));
        Assert.NotNull(store.Get(mine.Id, "u1"));
        Assert.True(store.Remove(mine.Id, "u1"));
        store.Add("c.pdf", bytes, "u3");                       // 자리가 나면 다시 받는다
    }

    [Fact]
    public async Task A_comparison_whose_file_was_closed_answers_404_not_500()
    {
        var me = factory.CreateClient();
        var a = await Upload(me, ComparePairs.Sheet(), "a.pdf");
        var b = await Upload(me, ComparePairs.Sheet(new ComparePairs.Revision { AddLine = true }), "b.pdf");
        var result = (await (await me.PostAsJsonAsync("/api/compare", new CompareRequest(a.Id, b.Id))).Content.ReadFromJsonAsync<CompareDto>(Json))!;
        Assert.Equal(HttpStatusCode.NoContent, (await me.DeleteAsync($"/api/files/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await me.GetAsync($"/api/compare/{result.Id}/pages/0/overlay.png")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await me.PostAsJsonAsync($"/api/compare/{result.Id}/export", new ExportRequest("1", null))).StatusCode);
    }

    [Fact]
    public async Task Looking_at_a_comparison_keeps_its_files()
    {
        var time = new FakeTime(DateTimeOffset.UtcNow);
        using var docs = new DocumentStore(new MuPdfEngine(), time);
        var compares = new CompareStore(docs, new HostingOptions { Mode = HostingMode.Web }, time);
        var a = docs.Add("a.pdf", ComparePairs.Sheet(), "u1");
        var b = docs.Add("b.pdf", ComparePairs.Sheet(new ComparePairs.Revision { AddLine = true }), "u1");
        var session = await compares.RunAsync(a, b, false, CancellationToken.None);
        time.Now += TimeSpan.FromMinutes(20);
        Assert.NotNull(compares.Get(session.Id, "u1"));      // 결과 목록만 보고 있어도
        time.Now += TimeSpan.FromMinutes(20);
        Assert.Equal(0, docs.RemoveIdle(time.Now - TimeSpan.FromMinutes(30)));
        Assert.NotNull(compares.Original(session));
        Assert.NotNull(compares.Revision(session));
    }
}
