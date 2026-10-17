using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DrawingReview.TestData;
using DrawingReview.Web.Api;
using DrawingReview.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DrawingReview.Web.Tests;

/// <summary>웹 모드 월 사용량 한도: 바쁜 시간을 세고, 한도에 닿으면 올리기·비교를 막고, 다음 달에 풀린다.</summary>
public class UsageLimitTests
{
    private sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset Oct17 = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static HostingOptions Web(double hours, string? file = null) =>
        new() { Mode = HostingMode.Web, MonthlyLimitHours = hours, UsageFile = file };

    [Fact]
    public void Only_time_with_a_request_in_progress_is_counted()
    {
        var time = new FakeTime(Oct17);
        var meter = new UsageMeter(Web(1), time);
        var a = meter.Track();
        time.Now += TimeSpan.FromMinutes(10);
        var b = meter.Track();                       // 겹친 요청은 두 번 세지 않는다
        time.Now += TimeSpan.FromMinutes(5);
        a.Dispose();
        time.Now += TimeSpan.FromMinutes(5);
        b.Dispose();
        time.Now += TimeSpan.FromHours(3);          // 아무 요청도 없는 시간은 안 센다
        Assert.Equal(20 * 60, meter.UsedSeconds, 3);
        Assert.False(meter.IsOverLimit);
    }

    [Fact]
    public void Reaching_the_limit_blocks_until_next_month()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero));
        var meter = new UsageMeter(Web(0.5), time);
        using (meter.Track()) time.Now += TimeSpan.FromMinutes(30);
        Assert.True(meter.IsOverLimit);
        Assert.Contains("November 1", meter.LimitMessage());
        time.Now = new DateTimeOffset(2026, 11, 1, 0, 0, 1, TimeSpan.Zero);
        Assert.False(meter.IsOverLimit);
        Assert.Equal(0, meter.UsedSeconds);
    }

    [Fact]
    public void A_request_running_over_midnight_counts_only_the_new_month_part()
    {
        var time = new FakeTime(new DateTimeOffset(2026, 10, 31, 23, 50, 0, TimeSpan.Zero));
        var meter = new UsageMeter(Web(1), time);
        using (meter.Track())
        {
            time.Now += TimeSpan.FromMinutes(5);
            Assert.Equal(300, meter.UsedSeconds, 3);    // 처리 중인 시간도 바로 보인다
            time.Now += TimeSpan.FromMinutes(15);
            Assert.Equal(600, meter.UsedSeconds, 3);    // 11월 몫 10분만
        }
        Assert.Equal(600, meter.UsedSeconds, 3);
    }

    [Fact]
    public void The_count_survives_a_restart_through_the_usage_file()
    {
        var file = Path.Combine(Path.GetTempPath(), "usage-" + Guid.NewGuid().ToString("N"), "usage.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);   // 배포에서는 붙여 둔 저장소 폴더
        try
        {
            var time = new FakeTime(Oct17);
            var first = new UsageMeter(Web(1, file), time);
            using (first.Track()) time.Now += TimeSpan.FromMinutes(12);
            first.Save();

            var restarted = new UsageMeter(Web(1, file), time);
            Assert.Equal(12 * 60, restarted.UsedSeconds, 3);

            time.Now = Oct17.AddMonths(1);              // 다음 달에 켜지면 지난달 기록은 안 쓴다
            Assert.Equal(0, new UsageMeter(Web(1, file), time).UsedSeconds);
        }
        finally { Directory.Delete(Path.GetDirectoryName(file)!, recursive: true); }
    }

    [Fact]
    public void A_broken_usage_file_pauses_new_work_and_is_not_overwritten()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "not json");
            var time = new FakeTime(Oct17);
            var meter = new UsageMeter(Web(20, file), time);
            Assert.True(meter.IsOverLimit);                // 기록을 잃고 0부터 세는 대신 막는다
            Assert.Contains("usage record", meter.LimitMessage());
            using (meter.Track()) time.Now += TimeSpan.FromMinutes(1);
            meter.Save();
            Assert.Equal("not json", File.ReadAllText(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void A_missing_storage_folder_pauses_new_work_instead_of_counting_from_zero()
    {
        var file = Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N"), "usage.json");
        var meter = new UsageMeter(Web(20, file), new FakeTime(Oct17));
        Assert.True(meter.IsOverLimit);
        meter.Save();
        Assert.False(Directory.Exists(Path.GetDirectoryName(file)));   // 임시 디스크에 폴더를 만들지 않는다
    }

    [Fact]
    public void Saving_while_a_request_runs_does_not_count_twice()
    {
        var time = new FakeTime(Oct17);
        var meter = new UsageMeter(Web(1), time);
        using (meter.Track())
        {
            time.Now += TimeSpan.FromMinutes(5);
            meter.Save();
            time.Now += TimeSpan.FromMinutes(5);
        }
        Assert.Equal(600, meter.UsedSeconds, 3);
    }

    [Fact]
    public void Short_requests_are_rounded_up_to_100ms_and_startup_is_counted()
    {
        var time = new FakeTime(Oct17);
        var meter = new UsageMeter(Web(1), time);
        for (int i = 0; i < 10; i++)
            using (meter.Track()) time.Now += TimeSpan.FromMilliseconds(2);
        Assert.Equal(1.0, meter.UsedSeconds, 3);
        meter.AddStartup(TimeSpan.FromSeconds(4));
        Assert.Equal(5.0, meter.UsedSeconds, 3);
    }

    [Fact]
    public async Task Many_parallel_requests_leave_the_meter_idle()
    {
        var meter = new UsageMeter(Web(1), TimeProvider.System);
        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() => { using (meter.Track()) Thread.SpinWait(1000); })));
        var after = meter.UsedSeconds;
        await Task.Delay(300);
        Assert.Equal(after, meter.UsedSeconds, 3);        // 처리 중인 요청이 남아 있으면 계속 늘어난다
    }

    [Fact]
    public void Pc_mode_and_a_zero_limit_never_block()
    {
        var time = new FakeTime(Oct17);
        foreach (var options in new[] { new HostingOptions { MonthlyLimitHours = 0.001 }, Web(0) })
        {
            var meter = new UsageMeter(options, time);
            using (meter.Track()) time.Now += TimeSpan.FromHours(5);
            Assert.False(meter.Enabled);
            Assert.False(meter.IsOverLimit);
        }
    }

    /// <summary>한도 1시간, 시간을 마음대로 돌리는 웹 서버.</summary>
    public sealed class LimitedApp : WebApplicationFactory<Program>
    {
        public FakeTimeProvider Time { get; } = new(Oct17);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Hosting:Mode", "Web");
            builder.UseSetting("Hosting:MonthlyLimitHours", "1");
            builder.ConfigureServices(s => s.AddSingleton<TimeProvider>(Time));
        }
    }

    public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, byte[] bytes, string name)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "file", name);
        return await client.PostAsync("/api/files", form);
    }

    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact]
    public async Task Over_the_limit_new_work_is_blocked_viewing_works_and_far_over_everything_stops()
    {
        await using var app = new LimitedApp();
        var client = app.CreateClient();
        var meter = app.Services.GetRequiredService<UsageMeter>();

        var a = (await (await Upload(client, ComparePairs.Sheet(), "a.pdf")).Content.ReadFromJsonAsync<FileDto>(Json))!;
        var b = (await (await Upload(client, ComparePairs.Sheet(new ComparePairs.Revision { AddLine = true }), "b.pdf")).Content.ReadFromJsonAsync<FileDto>(Json))!;
        var compared = await client.PostAsJsonAsync("/api/compare", new CompareRequest(a.Id, b.Id));
        Assert.Equal(HttpStatusCode.OK, compared.StatusCode);
        var result = (await compared.Content.ReadFromJsonAsync<CompareDto>(Json))!;

        using (meter.Track()) app.Time.Now += TimeSpan.FromHours(1.1);     // 한도 넘음 (안전장치 1.2배 전)
        var upload = await Upload(client, ComparePairs.Sheet(), "c.pdf");
        Assert.Equal(HttpStatusCode.TooManyRequests, upload.StatusCode);
        Assert.Contains("free usage is used up", await upload.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/compare", new CompareRequest(a.Id, b.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync($"/api/compare/{result.Id}/export", new ExportRequest("1", null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/files/{a.Id}/pages/0.png")).StatusCode);   // 이미 연 파일 보기는 됨
        Assert.Contains("\"blocked\":true", await client.GetStringAsync("/api/usage"));

        using (meter.Track()) app.Time.Now += TimeSpan.FromHours(0.2);     // 1.3시간: 안전장치
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync($"/api/files/{a.Id}/pages/0.png")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/usage")).StatusCode);
    }

    [Fact]
    public async Task Real_requests_are_counted_by_the_server()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("Hosting:Mode", "Web");
        });
        var client = app.CreateClient();
        await client.GetAsync("/api/health");
        Assert.True(app.Services.GetRequiredService<UsageMeter>().UsedSeconds > 0);   // 100ms 올림으로 최소 0.1초
    }

    [Fact]
    public async Task Pc_mode_counts_nothing()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        var client = app.CreateClient();
        await client.GetAsync("/api/health");
        var meter = app.Services.GetRequiredService<UsageMeter>();
        Assert.False(meter.Enabled);
        Assert.Equal(0, meter.UsedSeconds);
    }
}
