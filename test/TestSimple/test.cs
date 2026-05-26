using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestSimple;

[TestClass]
public class MyTestClass
{
    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "app_logs.db");

    [TestInitialize]
    public void Setup()
    {
        // 每次测试前清理数据库，确保环境干净
        
    }

    [TestMethod]
    public async Task Test1MillionLogs()
    {
        int total = 1000000;
        Console.WriteLine($"=== 开始 {total} 条日志并发写入压测 ===\n");

        // ==========================================
        // 测试 1：异步队列模式 (当前生成器架构)
        // ==========================================
        var sw = Stopwatch.StartNew();

        // 1. 多线程疯狂入队
        Parallel.For(1, total + 1, i =>
        {
            Logg.Logg.Write(new Info { Id = i });
        });
        long enqueueTime = sw.ElapsedMilliseconds;

        // 2. 等待后台消费者全部落盘
        await Logg.Logg.FlushAsync();
        long asyncTotalTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"[异步队列模式 (生产者-消费者)]");
        Console.WriteLine($" -> 业务线程入队耗时: {enqueueTime} ms (业务几乎不阻塞)");
        Console.WriteLine($" -> 后台完全落盘耗时: {asyncTotalTime} ms");
        Console.WriteLine($" -> 综合吞吐量: {total * 1000 / asyncTotalTime} 条/秒\n");


        return;
        // ==========================================
        // 测试 2：传统同步直写模式 (对比组)
        // ==========================================
        // 清理数据库
        Setup();

        // 预热一下 SQLite 连接池
        SyncWrite(new Info { Id = 0 });

        sw.Restart();
        Parallel.For(1, total + 1, i =>
        {
            SyncWrite(new Info { Id = i });
        });
        long syncTotalTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"[传统同步直写模式 (带全局锁)]");
        Console.WriteLine($" -> 总耗时: {syncTotalTime} ms");
        Console.WriteLine($" -> 综合吞吐量: {total * 1000 / syncTotalTime} 条/秒\n");

        Console.WriteLine($"🚀 性能提升倍数: {(double)syncTotalTime / asyncTotalTime:F2} 倍");
    }

    // 模拟传统的同步写入 (必须加锁，否则 SQLite 会报 database is locked)
    private static readonly object _syncLock = new object();
    private static void SyncWrite(Info v)
    {
        lock (_syncLock)
        {
            using var conn = new SqliteConnection($"Data Source={DbPath};Pooling=True;");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO [Info] ([Time], [Level], [Id]) VALUES (@Time, @Level, @Id);";
            cmd.Parameters.AddWithValue("@Time", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@Level", "INFO");
            cmd.Parameters.AddWithValue("@Id", v.Id);
            cmd.ExecuteNonQuery();
        }
    }
}

public class Info
{
    public int Id { get; set; }
}