namespace DrawingReview.App.Core.Services;

/// <summary>
/// 한 문서에 대한 PDF 엔진 호출을 한 번에 하나씩, 화면(UI) 스레드 밖에서 실행한다.
/// MuPDF 문서 객체는 여러 스레드가 동시에 쓰면 안 되고, 렌더링은 오래 걸릴 수 있어 화면이 멈추면 안 되기 때문이다.
/// </summary>
public sealed class EngineGate : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task RunAsync(Action work, CancellationToken cancellationToken = default) =>
        RunAsync(() => { work(); return true; }, cancellationToken);

    public void Dispose() => _lock.Dispose();
}
