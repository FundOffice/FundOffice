using System.IO;
using Microsoft.Extensions.Logging;

namespace Vetting.Logging;

/// <summary>
/// 文件日志 Provider — 将 ILogger 输出写入 files/vetting/logs/{file}_{providerId}.txt
/// </summary>
internal sealed class VettingLogProvider(string logPath) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new VettingLogger(logPath);

    public void Dispose() { }
}

internal sealed class VettingLogger(string logPath) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null) return;

        var levelTag = logLevel switch
        {
            LogLevel.Error => "ERR",
            LogLevel.Warning => "WRN",
            LogLevel.Information => "INF",
            LogLevel.Debug => "DBG",
            LogLevel.Trace => "TRC",
            LogLevel.Critical => "CRT",
            _ => ""
        };

        var line = exception != null
            ? $"[{DateTime.Now:HH:mm:ss}] [{levelTag}] {message}{Environment.NewLine}  {exception}"
            : $"[{DateTime.Now:HH:mm:ss}] [{levelTag}] {message}";

        try
        {
            var dir = Path.GetDirectoryName(logPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch { }
    }
}
