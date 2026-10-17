using System.Text.Json;

namespace DrawingReview.Web.Services;

/// <summary>사용량 상태 (/api/usage). 시간 단위.</summary>
public sealed record UsageStatus(double UsedHours, double LimitHours, bool Blocked, DateTimeOffset ResetsAt);

/// <summary>
/// 웹 모드 월 사용량 한도. Cloud Run(요청 처리 중에만 과금)은 "요청이 하나라도 처리 중인 시간"만큼 돈이 나간다.
/// 그 시간(바쁜 시간)을 세다가 이번 달 한도(HostingOptions.MonthlyLimitHours)에 닿으면 새 파일 올리기와 비교를 막는다.
/// 이미 연 파일 보기·내려받기는 계속 된다 (파일은 30분 안 쓰면 지워지므로 그 뒤 사용은 짧다).
/// 매달 1일 0시(UTC)에 0으로 돌아간다. HostingOptions.UsageFile을 주면 껐다 켜도 이어서 센다.
/// 서버가 여러 대 뜨면 대마다 따로 세므로 배포 때 최대 서버 수를 1로 둔다.
/// 안전장치: 한도의 1.2배(HardLimitFactor)에 닿으면 /api 전체를 막는다 (보기·내려받기도). 사용량 파일 위치가 없거나
/// 못 읽으면 0부터 세지 않고 새 작업을 막는다 (기록을 잃어 무료 분량을 넘기는 것보다 안전).
/// Cloud Run은 서버 켜는 시간과 100ms 올림도 과금하므로 그만큼 더해서 센다.
/// </summary>
public sealed class UsageMeter
{
    private readonly object _lock = new();
    private readonly HostingOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<UsageMeter>? _log;
    private string _month;
    private double _savedSeconds;          // 이번 달 끝난 바쁜 시간
    private int _inFlight;
    private DateTimeOffset _busySince;
    private bool _dirty;
    private bool _broken;                  // 사용량 파일을 못 읽음 → 새 작업 막음, 저장 안 함
    private DateTimeOffset _lastSave;

    /// <summary>한도의 이 배수에 닿으면 /api 전체를 막는다.</summary>
    public const double HardLimitFactor = 1.2;

    public UsageMeter(HostingOptions options, TimeProvider time, ILogger<UsageMeter>? log = null)
    {
        _options = options;
        _time = time;
        _log = log;
        _month = MonthOf(time.GetUtcNow());
        _lastSave = time.GetUtcNow();
        Load();
        if (Enabled && string.IsNullOrWhiteSpace(options.UsageFile))
            _log?.LogWarning("Hosting:UsageFile is not set. The monthly usage count starts at 0 every time the server restarts.");
    }

    /// <summary>웹 모드이고 한도가 0보다 클 때만 센다.</summary>
    public bool Enabled => _options.IsWeb && _options.MonthlyLimitHours > 0;

    /// <summary>요청 하나가 시작될 때 부르고, 끝나면 Dispose한다.</summary>
    public IDisposable Track()
    {
        if (!Enabled) return Nothing.Instance;
        lock (_lock)
        {
            if (_inFlight++ == 0) _busySince = _time.GetUtcNow();
        }
        return new Request(this);
    }

    private void End()
    {
        bool save;
        lock (_lock)
        {
            if (--_inFlight > 0) return;
            var now = _time.GetUtcNow();
            AddBusy(now);
            // Cloud Run은 요청이 없을 때 CPU를 거의 안 줘서 1분 타이머가 늦을 수 있다 → 요청이 끝날 때 1분 넘었으면 저장
            save = now - _lastSave >= TimeSpan.FromMinutes(1);
        }
        if (save) Save();
    }

    /// <summary>서버가 켜지는 데 걸린 시간 (Cloud Run은 이것도 과금). 시작이 끝났을 때 한 번 부른다.</summary>
    public void AddStartup(TimeSpan startup)
    {
        if (!Enabled || startup <= TimeSpan.Zero) return;
        lock (_lock)
        {
            Rollover(_time.GetUtcNow());
            _savedSeconds += Math.Min(startup.TotalSeconds, 600);
            _dirty = true;
        }
    }

    /// <summary>바쁜 구간을 now까지 더한다. 달이 바뀌었으면 새 달 시작부터만 센다. _lock 안에서 부른다.</summary>
    private void AddBusy(DateTimeOffset now)
    {
        Rollover(now);
        var start = Max(_busySince, MonthStart(now));
        // Cloud Run은 과금 시간을 100ms 단위로 올린다
        _savedSeconds += Math.Ceiling(Math.Max(0, (now - start).TotalSeconds) * 10) / 10;
        _busySince = now;
        _dirty = true;
    }

    /// <summary>달이 바뀌었으면 0으로. _lock 안에서 부른다.</summary>
    private bool Rollover(DateTimeOffset now)
    {
        var month = MonthOf(now);
        if (month == _month) return false;
        _month = month;
        _savedSeconds = 0;
        _dirty = true;
        return true;
    }

    /// <summary>이번 달 바쁜 시간 (지금 처리 중인 요청 포함), 초.</summary>
    public double UsedSeconds
    {
        get
        {
            lock (_lock)
            {
                var now = _time.GetUtcNow();
                Rollover(now);
                var running = _inFlight > 0 ? Math.Max(0, (now - Max(_busySince, MonthStart(now))).TotalSeconds) : 0;
                return _savedSeconds + running;
            }
        }
    }

    /// <summary>새 파일 올리기·비교·내려받기를 막을 때.</summary>
    public bool IsOverLimit => Enabled && (_broken || UsedSeconds >= _options.MonthlyLimitHours * 3600);

    /// <summary>/api 전체를 막을 때 (한도의 HardLimitFactor배).</summary>
    public bool IsOverHardLimit => Enabled && UsedSeconds >= _options.MonthlyLimitHours * 3600 * HardLimitFactor;

    public UsageStatus Status()
    {
        var used = UsedSeconds;
        return new UsageStatus(Math.Round(used / 3600, 2), _options.MonthlyLimitHours,
            IsOverLimit, MonthStart(_time.GetUtcNow()).AddMonths(1));
    }

    /// <summary>한도에 닿았을 때 사용자에게 보여 줄 말.</summary>
    public string LimitMessage() => _broken
        ? "The server could not read its usage record, so uploading and comparing are paused. Please try again later."
        : $"This month's free usage is used up. Uploading and comparing will work again on {MonthStart(_time.GetUtcNow()).AddMonths(1):MMMM d} (UTC).";

    /// <summary>UsageFile에 저장 (바뀐 게 있을 때만). 실패해도 서버는 계속 돈다.</summary>
    public void Save()
    {
        if (!Enabled || string.IsNullOrWhiteSpace(_options.UsageFile)) return;
        string json;
        lock (_lock)
        {
            if (_broken) return;               // 못 읽은 기록을 작은 숫자로 덮어쓰지 않는다
            var now = _time.GetUtcNow();
            _lastSave = now;
            if (_inFlight > 0) AddBusy(now);   // 처리 중인 시간도 지금까지 반영
            else Rollover(now);
            if (!_dirty) return;
            json = JsonSerializer.Serialize(new Saved(_month, _savedSeconds));
            _dirty = false;
        }
        try
        {
            // 폴더는 만들지 않는다: 저장소(버킷)가 안 붙어 있으면 임시 디스크에 써서 기록을 잃게 되므로 오류로 남긴다
            var path = Path.GetFullPath(_options.UsageFile);
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (_lock) _dirty = true;
            _log?.LogError(ex, "Could not save the usage count to {File}.", _options.UsageFile);
        }
    }

    private void Load()
    {
        if (!Enabled || string.IsNullOrWhiteSpace(_options.UsageFile)) return;
        var path = Path.GetFullPath(_options.UsageFile);
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            _broken = true;
            _log?.LogError("The folder for Hosting:UsageFile ({File}) does not exist. Is the storage attached? Uploading and comparing are paused.", path);
            return;
        }
        if (!File.Exists(path)) return;            // 처음 켜짐
        try
        {
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(path));
            if (saved == null || !double.IsFinite(saved.Seconds)) throw new JsonException("Unexpected usage record.");
            if (saved.Month == _month && saved.Seconds > 0) _savedSeconds = saved.Seconds;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _broken = true;
            _log?.LogError(ex, "Could not read the usage count from {File}. Uploading and comparing are paused until it is fixed.", path);
        }
    }

    private static string MonthOf(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM");
    private static DateTimeOffset MonthStart(DateTimeOffset t) => new(t.UtcDateTime.Year, t.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private sealed record Saved(string Month, double Seconds);

    private sealed class Request(UsageMeter meter) : IDisposable
    {
        private int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) meter.End(); }
    }

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();
        public void Dispose() { }
    }
}

/// <summary>웹 모드: 1분마다, 그리고 서버가 꺼질 때 사용량을 파일에 저장한다.</summary>
public sealed class UsageSaver(UsageMeter meter, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) meter.Save();
        }
        catch (OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        meter.Save();
    }
}
