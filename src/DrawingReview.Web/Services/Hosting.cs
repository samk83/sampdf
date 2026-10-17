namespace DrawingReview.Web.Services;

/// <summary>Pc = 내 PC 전용 (127.0.0.1, 기본) · Web = 인터넷 서버 (설계서 11-4 W0).</summary>
public enum HostingMode { Pc, Web }

/// <summary>
/// 실행 방식 설정. appsettings 또는 환경 변수 `Hosting__Mode=Web`, `Hosting__IdleMinutes=30`, `Hosting__MaxConcurrentCompares=2`.
/// 웹 모드에서는 사용자마다 파일을 나누고, 오래 안 쓴 파일은 지우고, 동시에 도는 비교 수를 제한한다.
/// </summary>
public sealed class HostingOptions
{
    public HostingMode Mode { get; set; } = HostingMode.Pc;

    /// <summary>웹 모드: 이 시간 동안 안 쓴 파일·비교 결과는 서버 메모리에서 지운다.</summary>
    public int IdleMinutes { get; set; } = 30;

    /// <summary>웹 모드: 동시에 도는 비교 수 (나머지는 순서를 기다림). 메모리를 지키기 위함.</summary>
    public int MaxConcurrentCompares { get; set; } = 2;

    /// <summary>웹 모드: 파일 하나의 최대 크기 (MB). PC 모드는 300MB.</summary>
    public int MaxUploadMb { get; set; } = 100;

    /// <summary>웹 모드: 서버 전체에 열어 둘 파일 크기 합 (MB). 넘으면 새 파일을 잠시 받지 않는다.</summary>
    public int MaxTotalMb { get; set; } = 1536;

    public bool IsWeb => Mode == HostingMode.Web;
    public TimeSpan IdleExpiry => TimeSpan.FromMinutes(Math.Max(1, IdleMinutes));
}

/// <summary>
/// 파일 주인 구분. 웹 모드에서는 브라우저마다 무작위 쿠키(로그인 없음)로 나눠, 다른 사람의 파일 번호를 알아도 열 수 없게 한다.
/// PC 모드에서는 모두 "local" 한 사람.
/// </summary>
public static class Owners
{
    public const string Local = "local";
    public const string CookieName = "sdt_owner";
    private const string ItemKey = "sdt.owner";

    public static string Of(HttpContext context) => context.Items[ItemKey] as string ?? Local;

    /// <summary>요청마다 주인을 정한다. 웹 모드에서 쿠키가 없거나 모양이 틀리면 새로 만들어 보낸다.</summary>
    public static Task Assign(HttpContext context, HostingOptions options, Func<Task> next)
    {
        if (options.IsWeb)
        {
            var id = context.Request.Cookies[CookieName];
            if (id is not { Length: 32 } || !id.All(char.IsAsciiHexDigitLower))
            {
                id = Guid.NewGuid().ToString("N");
                context.Response.Cookies.Append(CookieName, id, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps || context.Request.Headers["X-Forwarded-Proto"] == "https",
                    // Lax: 다른 사이트의 링크로 들어와도 쿠키가 유지됨. 바꾸는 요청(POST·DELETE)은 다른 사이트에서 오면 안 보냄
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    // 만료 없음 = 브라우저를 닫을 때까지 (파일은 30분 안 쓰면 어차피 지워짐)
                });
            }
            context.Items[ItemKey] = id;
        }
        return next();
    }
}

/// <summary>웹 모드: 1분마다 오래 안 쓴 파일·비교 결과를 지운다.</summary>
public sealed class ExpirySweeper(DocumentStore documents, CompareStore compares, HostingOptions options, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var cutoff = time.GetUtcNow() - options.IdleExpiry;
            compares.RemoveIdle(cutoff);
            documents.RemoveIdle(cutoff);
        }
    }
}
