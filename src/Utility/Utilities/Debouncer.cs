public class Debouncer
{
    private readonly Action _action;
    private readonly int _dueTime;
    private Timer? _timer;
    private Lock _lock = new();

    public Debouncer(Action action, int milseconds = 200)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _dueTime = milseconds > 0 ? milseconds : throw new ArgumentOutOfRangeException(nameof(milseconds));
        //_timer = new Timer(_ => Work(), null, Timeout.Infinite, _dueTime);
    }

    public void Invoke()
    {
        lock (_lock)
        {
            if (_timer is null)
                _timer = new Timer(_ => Work(), null, _dueTime, Timeout.Infinite);
            else
                _timer.Change(_dueTime, Timeout.Infinite);
        }
    }

    private void Work()
    {
        lock(_lock)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _action();
            _timer?.Dispose();
            _timer = null;
        }
    }

}


public class AsyncDebouncer : IDisposable
{
    private readonly Func<Task> _asyncAction;
    private readonly int _dueTime;
    private CancellationTokenSource? _currentCts;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isDisposed;

    public AsyncDebouncer(Func<Task> asyncAction, int milliseconds = 200)
    {
        _asyncAction = asyncAction ?? throw new ArgumentNullException(nameof(asyncAction));
        _dueTime = milliseconds > 0 ? milliseconds : throw new ArgumentOutOfRangeException(nameof(milliseconds));
    }

    public async Task InvokeAsync()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(AsyncDebouncer));

        // 创建新的 CTS 用于当前调用
        var newCts = new CancellationTokenSource();

        // 原子性地替换旧的 CTS 并取消它
        var oldCts = Interlocked.Exchange(ref _currentCts, newCts);
        oldCts?.Cancel();
        oldCts?.Dispose();

        try
        {
            // 等待指定的防抖时间
            await Task.Delay(_dueTime, newCts.Token);

            // 使用 SemaphoreSlim 确保操作互斥执行
            await _semaphore.WaitAsync(newCts.Token);

            try
            {
                // 再次检查是否仍然是最新的 CTS
                if (_currentCts == newCts)
                {
                    // 执行异步操作
                    await _asyncAction();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，这是正常现象，不需要处理
        }
        finally
        {
            // 如果当前的 CTS 仍然是最新的，将其置为 null
            if (Interlocked.CompareExchange(ref _currentCts, null, newCts) == newCts)
            {
                newCts.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                var cts = Interlocked.Exchange(ref _currentCts, null);
                cts?.Cancel();
                cts?.Dispose();

                _semaphore.Dispose();
            }

            _isDisposed = true;
        }
    }
}