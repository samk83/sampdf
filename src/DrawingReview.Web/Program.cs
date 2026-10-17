using System.Diagnostics;
using DrawingReview.Engine.Pdfium;
using DrawingReview.Web.Api;
using DrawingReview.Web.Services;
using Microsoft.AspNetCore.Http.Features;

// 도면 비교 웹앱. 두 가지 방식으로 돈다 (Services/Hosting.cs):
// - PC 모드 (기본): dotnet run --project src/DrawingReview.Web → 브라우저가 http://127.0.0.1:5180 으로 열린다.
//   이 PC 안에서만 접속할 수 있다 (127.0.0.1). 다른 컴퓨터에서는 열리지 않는다.
// - 웹 모드 (Hosting__Mode=Web, Docker 배포): 0.0.0.0:$PORT(기본 8080)에서 받고, 사용자마다 파일을 나누고, 오래 안 쓴 파일은 지운다.
var builder = WebApplication.CreateBuilder(args);
var hosting = builder.Configuration.GetSection("Hosting").Get<HostingOptions>() ?? new HostingOptions();

const string DefaultUrl = "http://127.0.0.1:5180";
if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    if (hosting.IsWeb && !int.TryParse(port, out _)) throw new InvalidOperationException($"PORT must be a number, not '{port}'.");
    builder.WebHost.UseUrls(hosting.IsWeb ? $"http://0.0.0.0:{port}" : DefaultUrl);
}

// 큰 도면 PDF도 올릴 수 있게 한도를 300MB로 (웹 모드는 HostingOptions.MaxUploadMb, 기본 100MB)
long MaxUpload = (hosting.IsWeb ? Math.Max(1, hosting.MaxUploadMb) : 300L) * 1024 * 1024;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxUpload);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUpload);

builder.Services.AddSingleton(hosting);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PdfiumEngine>();
builder.Services.AddSingleton(sp => new DocumentStore(sp.GetRequiredService<PdfiumEngine>(), sp.GetRequiredService<TimeProvider>())
{
    MaxTotalBytes = hosting.IsWeb ? Math.Max(1, hosting.MaxTotalMb) * 1024L * 1024 : long.MaxValue,
});
builder.Services.AddSingleton<CompareStore>();
if (hosting.IsWeb) builder.Services.AddHostedService<ExpirySweeper>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var app = builder.Build();

if (hosting.IsWeb)
{
    // 웹 모드 기본 보안 머리말: 다른 사이트 안에 넣지 못하게, 파일 종류 추측 금지, 주소 정보 최소
    app.Use((context, next) =>
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Content-Security-Policy"] = "frame-ancestors 'none'";
        return next();
    });
}
app.Use((context, next) => Owners.Assign(context, hosting, next));

app.UseDefaultFiles();
app.UseStaticFiles();
Endpoints.Map(app);
// 화면 주소(새로고침 등)는 모두 index.html로
app.MapFallbackToFile("index.html");

// 시작하면 기본 브라우저로 연다. --no-browser 로 끌 수 있다.
if (!hosting.IsWeb && !args.Contains("--no-browser") && !app.Environment.IsEnvironment("Testing"))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.FirstOrDefault() ?? DefaultUrl;
        Console.WriteLine($"Drawing Compare is running at {url}  (close this window to stop)");
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { /* 브라우저를 못 열면 위 주소를 직접 연다 */ }
    });
}

app.Run();

/// <summary>테스트(WebApplicationFactory)에서 이 프로그램을 띄우기 위한 선언.</summary>
public partial class Program;
