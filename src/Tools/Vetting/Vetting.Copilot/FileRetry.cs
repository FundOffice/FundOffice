namespace Vetting.Copilot;

public static class FileRetry
{
    public static T Run<T>(Func<T> action, string description = "文件操作", int maxRetries = 3, int delayMs = 1000, Action<string>? onRetry = null)
    {
        for (int i = 0; ; i++)
        {
            try { return action(); }
            catch (IOException ex) when (i < maxRetries)
            {
                var waitSec = delayMs * (i + 1) / 1000.0;
                onRetry?.Invoke($"{description} 失败，{waitSec:F1}秒后重试 ({i + 1}/{maxRetries}): {ex.Message}");
                System.Threading.Thread.Sleep(delayMs * (i + 1));
            }
            catch (UnauthorizedAccessException ex) when (i < maxRetries)
            {
                var waitSec = delayMs * (i + 1) / 1000.0;
                onRetry?.Invoke($"{description} 失败，{waitSec:F1}秒后重试 ({i + 1}/{maxRetries}): {ex.Message}");
                System.Threading.Thread.Sleep(delayMs * (i + 1));
            }
        }
    }

    public static void Run(Action action, string description = "文件操作", int maxRetries = 3, int delayMs = 1000, Action<string>? onRetry = null)
    {
        Run(() => { action(); return 0; }, description, maxRetries, delayMs, onRetry);
    }
}
