using Microsoft.Data.Sqlite;
using SQLitePCL;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace MoT;

public static class Logg
{
    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "app_logs.db");
    private static readonly string _connStr = $"Data Source={DbPath};Pooling=True;";

    private static readonly Channel<LogJob> _channel = Channel.CreateBounded<LogJob>(
        new BoundedChannelOptions(100000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    private static readonly HashSet<string> _createdTables = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _tableSqlCache = new(StringComparer.OrdinalIgnoreCase);

    // 用于从异常信息中提取表名的正则 (兼容 main.AuditLog 或 [AuditLog] 等格式)
    private static readonly Regex _noTableRegex = new Regex(@"no such table:\s*(?:\w+\.)?[\[""']?(\w+)[\]""']?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool _isWalSet = false;

    static Logg()
    {
        try { Batteries.Init(); } catch { }
        Task.Factory.StartNew(Consume, TaskCreationOptions.LongRunning);
    }

    private static void EnsureWal()
    {
        if (!_isWalSet)
        {
            lock (_createdTables)
            {
                if (!_isWalSet)
                {
                    try
                    {
                        using var c = new SqliteConnection(_connStr);
                        c.Open();
                        using var cmd = c.CreateCommand();
                        cmd.CommandText = "PRAGMA journal_mode=WAL;";
                        cmd.ExecuteNonQuery();
                        _isWalSet = true;
                    }
                    catch { }
                }
            }
        }
    }

    public static void Register<T>(Action<T> writer, string tableName, string createTableSql)
    {
        Router<T>.Writer = writer;
        lock (_tableSqlCache) { _tableSqlCache[tableName] = createTableSql; }
        EnsureTableCreated(tableName, createTableSql);
    }

    public static void EnsureTableCreated(string tableName, string createTableSql)
    {
        EnsureWal();
        bool added;
        lock (_createdTables) { added = _createdTables.Add(tableName); }

        if (added)
        {
            try
            {
                using var c = new SqliteConnection(_connStr);
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = createTableSql;
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }

    internal static class Router<T> { public static Action<T>? Writer; }

    public static void Write<T>(T v)
    {
        var w = Router<T>.Writer;
        w?.Invoke(v);
    }

    public static void Enqueue(LogJob job)
    {
        var spinWait = new SpinWait();
        while (!_channel.Writer.TryWrite(job))
        {
            spinWait.SpinOnce();
        }
    }

    public static Task FlushAsync()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new LogJob(tcs));
        return tcs.Task;
    }

    private static void Consume()
    {
        var batch = new List<LogJob>(1000);
        while (_channel.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (_channel.Reader.TryRead(out var job))
            {
                if (job.FlushTcs != null)
                {
                    bool success = true;
                    if (batch.Count > 0)
                    {
                        success = WriteBatch(batch);
                        batch.Clear();
                    }

                    // 🚨 核心优化：在显式 Flush 时，执行 PASSIVE Checkpoint
                    // 将 WAL 文件中的数据非阻塞地合并到主 .db 文件中，满足"Flush即完全可见"的语义
                    if (success)
                    {
                        try
                        {
                            using var conn = new SqliteConnection(_connStr);
                            conn.Open();
                            using var cmd = conn.CreateCommand();
                            // PASSIVE: 不阻塞并发读写，尽可能多地合并页
                            cmd.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Logg Warning] Checkpoint 失败: {ex.Message}");
                        }
                    }

                    if (success)
                        job.FlushTcs.TrySetResult(true);
                    else
                        job.FlushTcs.TrySetException(new Exception("Logg WriteBatch failed, check Debug output."));
                    continue;
                }
                batch.Add(job);
                if (batch.Count >= 1000) break;
            }
            if (batch.Count > 0)
            {
                WriteBatch(batch);
                batch.Clear();
            }
        }
    }

    /// <summary>
    /// 核心修复：返回 bool 标识是否成功，并输出异常日志
    /// </summary>
    private static bool WriteBatch(List<LogJob> batch)
    {
        try
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            using var tran = conn.BeginTransaction();
            SqliteCommand? cmd = null;
            string? currentSql = null;

            foreach (var job in batch)
            {
                if (job.InsertSql != currentSql)
                {
                    cmd?.Dispose();
                    cmd = conn.CreateCommand();
                    cmd.Transaction = tran;
                    cmd.CommandText = job.InsertSql!;
                    currentSql = job.InsertSql;
                }

                cmd!.Parameters.Clear();
                job.Binder!(cmd);

                try
                {
                    cmd.ExecuteNonQuery();
                }
                // 🚨 核心修复：不再依赖 job.TableName，只要是无表异常就拦截
                catch (Exception ex) when (
                    ex.GetType().Name.Contains("SqliteException", StringComparison.OrdinalIgnoreCase) &&
                    ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
                {
                    // 1. 从异常信息中正则提取表名 (例如提取出 "AuditLog")
                    var match = _noTableRegex.Match(ex.Message);
                    if (!match.Success)
                    {
                        Console.WriteLine($"[Logg Error] 无法从异常中解析表名: {ex.Message}");
                        throw;
                    }

                    string missingTableName = match.Groups[1].Value;
                    Console.WriteLine($"[Logg Trace] 捕获到无表异常 ({missingTableName})，正在尝试自动建表...");

                    // 2. 从缓存中获取建表 SQL
                    string? createSql;
                    lock (_tableSqlCache) { _tableSqlCache.TryGetValue(missingTableName, out createSql); }

                    if (string.IsNullOrEmpty(createSql))
                    {
                        Console.WriteLine($"[Logg Error] 缓存中未找到表 {missingTableName} 的建表语句，无法自动重建。");
                        throw;
                    }

                    // 3. 执行建表
                    using var createCmd = conn.CreateCommand();
                    createCmd.Transaction = tran;
                    createCmd.CommandText = createSql;
                    createCmd.ExecuteNonQuery();

                    lock (_createdTables) { _createdTables.Add(missingTableName); }

                    // 4. 新建 Command 重试 (避免 SQLite 底层 Prepared Statement 缓存导致二次报错)
                    using var retryCmd = conn.CreateCommand();
                    retryCmd.Transaction = tran;
                    retryCmd.CommandText = job.InsertSql!;

                    job.Binder!(retryCmd);
                    retryCmd.ExecuteNonQuery();

                    Console.WriteLine($"[Logg Trace] 表 {missingTableName} 自动重建成功，数据已补录！");
                }
            }
            tran.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logg Error] 批量写入最终失败，事务已回滚: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 调试辅助：强制将 WAL 文件中的数据合并到主 .db 文件中
    /// (注意：这会阻塞线程并降低性能，仅建议在测试或程序退出时调用)
    /// </summary>
    public static void ForceCheckpoint()
    {
        try
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }



    private static readonly string _defaultTableName = "_def_logs_";
    private static bool _isDefaultRegistered = false;
    private static readonly object _regLock = new object();
    // 支持 {Name} 或带格式化 {Time:yyyy-MM-dd} 的占位符
    private static readonly Regex _propRegex = new Regex(@"\{(\w+)(?:[:#][^\}]*)?\}", RegexOptions.Compiled);

    private static void EnsureDefaultRegistered()
    {
        if (_isDefaultRegistered) return;
        lock (_regLock)
        {
            if (_isDefaultRegistered) return;
            var sql = $"CREATE TABLE IF NOT EXISTS {_defaultTableName} (" +
                      "Timestamp TEXT NOT NULL, " +
                      "Level INTEGER NOT NULL, " +
                      "Message TEXT NOT NULL, " +
                      "Exception TEXT, " +
                      "Properties TEXT)";
            Register<LogEvent>(writer: WriteLogEvent, _defaultTableName, sql);
            _isDefaultRegistered = true;
        }
    }

    private static void WriteLogEvent(LogEvent evt)
    {
        var job = new LogJob(
            $"INSERT INTO {_defaultTableName} (Timestamp, Level, Message, Exception, Properties) VALUES ($t, $l, $m, $e, $p)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("$t", evt.Timestamp.ToString("o"));
                cmd.Parameters.AddWithValue("$l", (int)evt.Level);
                cmd.Parameters.AddWithValue("$m", evt.Message);
                cmd.Parameters.AddWithValue("$e", (object?)evt.Exception ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$p", (object?)evt.Properties ?? DBNull.Value);
            },
            _defaultTableName
        );
        Enqueue(job);
    }

    private static string FormatMessage(string? template, object?[]? values, out string propertiesJson)
    {
        if (string.IsNullOrEmpty(template))
        {
            propertiesJson = "{}";
            return string.Empty;
        }

        var matches = _propRegex.Matches(template);
        if (matches.Count == 0)
        {
            propertiesJson = "{}";
            return template;
        }

        var dict = new Dictionary<string, object?>();
        var result = template;
        int i = 0;
        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            var val = (values != null && i < values.Length) ? values[i] : null;
            dict[name] = val;
            result = result.Replace(match.Value, val?.ToString() ?? "null");
            i++;
        }

        try { propertiesJson = JsonSerializer.Serialize(dict); }
        catch { propertiesJson = "{}"; }

        return result;
    }

    private static void WriteLog(LogLevel level, Exception? ex, string? messageTemplate, params object?[]? propertyValues)
    {
        EnsureDefaultRegistered();
        var message = FormatMessage(messageTemplate, propertyValues, out var props);
        var evt = new LogEvent
        {
            Level = level,
            Message = message,
            Exception = ex?.ToString(),
            Properties = props
        };
        Write(evt);
    }

    // 对外 API
    public static void Verbose(string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Verbose, null, messageTemplate, propertyValues);
    public static void Debug(string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Debug, null, messageTemplate, propertyValues);
    public static void Information(string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Information, null, messageTemplate, propertyValues);
    public static void Warning(string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Warning, null, messageTemplate, propertyValues);
    public static void Error(Exception? exception, string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Error, exception, messageTemplate, propertyValues);
    public static void Error(string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Error, null, messageTemplate, propertyValues);
    public static void Fatal(Exception? exception, string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Fatal, exception, messageTemplate, propertyValues);
    public static void Fatal(string? messageTemplate, params object?[]? propertyValues) => WriteLog(LogLevel.Fatal, null, messageTemplate, propertyValues);
}

public readonly struct LogJob
{
    public readonly string? InsertSql;
    public readonly Action<SqliteCommand>? Binder;
    public readonly TaskCompletionSource<bool>? FlushTcs;
    public readonly string? TableName;

    public LogJob(string i, Action<SqliteCommand> b, string? tableName = null)
    {
        InsertSql = i; Binder = b; FlushTcs = null;
        TableName = tableName;
    }

    public LogJob(TaskCompletionSource<bool> tcs)
    {
        InsertSql = null; Binder = null; FlushTcs = tcs;
        TableName = null;
    }
}

// ================= 泛型结构化日志 (Serilog 风格) =================

public enum LogLevel
{
    Verbose = 0, Debug = 1, Information = 2, Warning = 3, Error = 4, Fatal = 5
}

public class LogEvent
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    public string? Properties { get; set; }
}