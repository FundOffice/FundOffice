namespace Vetting.Copilot;

public static class FileRetry
{
    public static T Run<T>(Func<T> action, string description = "文件操作", int maxRetries = 3, int delayMs = 1000, Action<string>? onRetry = null)
    {
        for (int i = 0; ; i++)
        {
            try { return action(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && i < maxRetries)
            {
                onRetry?.Invoke($"{description} 失败，{delayMs * (i + 1) / 1000}秒后重试 ({i + 1}/{maxRetries}): {ex.Message}");
                System.Threading.Thread.Sleep(delayMs * (i + 1));
            }
        }
    }

    public static void Run(Action action, string description = "文件操作", int maxRetries = 3, int delayMs = 1000, Action<string>? onRetry = null)
    {
        Run(() => { action(); return 0; }, description, maxRetries, delayMs, onRetry);
    }
}
