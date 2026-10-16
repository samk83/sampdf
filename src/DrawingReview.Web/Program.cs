using System.Diagnostics;
using DrawingReview.Engine.MuPdf;
using DrawingReview.Web.Api;
using DrawingReview.Web.Services;
using Microsoft.AspNetCore.Http.Features;

// 도면 비교 웹앱 (내 PC 전용).
// 실행: dotnet run --project src/DrawingReview.Web  →  브라우저가 http://127.0.0.1:5180 으로 열린다.
// 이 PC 안에서만 접속할 수 있다 (127.0.0.1). 다른 컴퓨터에서는 열리지 않는다.
var builder = WebApplication.CreateBuilder(args);

const string DefaultUrl = "http://127.0.0.1:5180";
if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls(DefaultUrl);

// 큰 도면 PDF도 올릴 수 있게 한도를 300MB로
const long MaxUpload = 300L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = MaxUpload);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUpload);

builder.Services.AddSingleton<MuPdfEngine>();
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<CompareStore>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
Endpoints.Map(app);
// 화면 주소(새로고침 등)는 모두 index.html로
app.MapFallbackToFile("index.html");

// 시작하면 기본 브라우저로 연다. --no-browser 로 끌 수 있다.
if (!args.Contains("--no-browser") && !app.Environment.IsEnvironment("Testing"))
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
