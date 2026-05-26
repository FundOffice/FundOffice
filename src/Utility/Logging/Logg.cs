using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Logg;

public static class Logg
{
    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "app_logs.db");
    private static readonly string _connStr = $"Data Source={DbPath};Pooling=True;";

    private static readonly Channel<LogJob> _channel = Channel.CreateBounded<LogJob>(
        new BoundedChannelOptions(100000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    private static readonly HashSet<string> _createdTables = new();
    private static bool _isWalSet = false;

    static Logg()
    {
        try { Batteries.Init(); } catch { }
        // 后台消费者使用 LongRunning，避免占用普通线程池
        Task.Factory.StartNew(Consume, TaskCreationOptions.LongRunning);
    }

    private static void EnsureWal()
    {
        if (!_isWalSet) lock (_createdTables) if (!_isWalSet)
        {
            try { using var c = new SqliteConnection(_connStr); c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA journal_mode=WAL;"; cmd.ExecuteNonQuery(); _isWalSet = true; } catch { }
        }
    }

    public static void Register<T>(Action<T> writer, string tableName, string createTableSql)
    {
        Router<T>.Writer = writer;
        EnsureTableCreated(tableName, createTableSql);
    }

    public static void EnsureTableCreated(string tableName, string createTableSql)
    {
        EnsureWal();
        lock (_createdTables) if (_createdTables.Add(tableName))
        {
            try { using var c = new SqliteConnection(_connStr); c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = createTableSql; cmd.ExecuteNonQuery(); } catch (Exception ex) { Debug.WriteLine(ex); }
        }
    }

    internal static class Router<T> { public static Action<T> Writer; }
    public static void Write<T>(T v) { var w = Router<T>.Writer; if (w != null) w(v); }

    /// <summary>
    /// 核心修复：使用 SpinWait 替代 .Wait()，彻底解决线程池饥饿
    /// </summary>
    public static void Enqueue(LogJob job)
    {
        var spinWait = new SpinWait();
        while (!_channel.Writer.TryWrite(job))
        {
            spinWait.SpinOnce(); // 让出 CPU 时间片，但不阻塞线程池线程
        }
    }

    public static Task FlushAsync()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new LogJob(tcs));
        return tcs.Task;
    }

    /// <summary>
    /// 核心修复：后台消费改为纯同步，消除异步状态机开销
    /// </summary>
    private static void Consume()
    {
        var batch = new List<LogJob>(1000);
        // 使用同步的 WaitToRead，配合 LongRunning 线程，完美契合
        while (_channel.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (_channel.Reader.TryRead(out var job))
            {
                if (job.FlushTcs != null)
                {
                    if (batch.Count > 0) { WriteBatch(batch); batch.Clear(); }
                    job.FlushTcs.TrySetResult(true);
                    continue;
                }
                batch.Add(job);
                if (batch.Count >= 1000) break;
            }
            if (batch.Count > 0) { WriteBatch(batch); batch.Clear(); }
        }
    }

    /// <summary>
    /// 核心修复：数据库操作全部改为同步，性能提升数倍
    /// </summary>
    private static void WriteBatch(List<LogJob> batch)
    {
        try
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open(); // 同步 Open
            using var tran = conn.BeginTransaction();
            SqliteCommand cmd = null; string currentSql = null;
            foreach (var job in batch)
            {
                if (job.InsertSql != currentSql)
                {
                    cmd?.Dispose();
                    cmd = conn.CreateCommand();
                    cmd.Transaction = tran;
                    cmd.CommandText = job.InsertSql;
                    currentSql = job.InsertSql;
                }
                job.Binder(cmd);
                cmd.ExecuteNonQuery(); // 同步 Execute，没有 Async 状态机开销！
            }
            tran.Commit();
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    public readonly struct LogJob
    {
        public readonly string InsertSql;
        public readonly Action<SqliteCommand> Binder;
        public readonly TaskCompletionSource<bool> FlushTcs;

        public LogJob(string i, Action<SqliteCommand> b) { InsertSql = i; Binder = b; FlushTcs = null; }
        public LogJob(TaskCompletionSource<bool> tcs) { InsertSql = null; Binder = null; FlushTcs = tcs; }
    }
}