using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoT; // 引用包含 Logg 类的命名空间

namespace TestSimple;

[TestClass]
public class TestLoggFunctionality
{
    public TestContext TestContext { get; set; } = null!;
    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "app_logs.db");

    private void Log(string message)
    {
        Console.WriteLine(message);
        TestContext?.WriteLine(message);
    }

    [TestInitialize]
    public void Setup()
    {
        // 1. 强制清空连接池，防止底层长连接锁住表导致 Drop 失败
        SqliteConnection.ClearAllPools();

        // 2. 重置 Logg 内部的内存状态 (清理“已建表”缓存，但保留“建表SQL”缓存)
        // 这样框架会认为表还没建，配合下面的物理 Drop，完美触发自动建表机制
        //Logg.ResetTestState();

        // 3. 连接数据库，删除所有用户表 (不删物理文件)
        using (var conn = new SqliteConnection($"Data Source={DbPath};Pooling=True;"))
        {
            conn.Open();

            // 查询所有非系统的用户表
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

            var tables = new List<string>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            // 遍历并 Drop 掉所有表
            foreach (var table in tables)
            {
                using var dropCmd = conn.CreateCommand();
                // 使用 [] 包裹表名，防止表名是 SQL 关键字导致报错
                dropCmd.CommandText = $"DROP TABLE IF EXISTS [{table}];";
                dropCmd.ExecuteNonQuery();
            }

            // 4. (可选) 清理 WAL 文件，将主文件恢复到最干净的 0 字节状态
            using var checkpointCmd = conn.CreateCommand();
            checkpointCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            checkpointCmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// 测试 1：验证类似 Serilog 的内置 Debug/Info/Error 结构化日志
    /// </summary>
    [TestMethod]
    public async Task TestBuiltinStructuredLogs()
    {
        Log("=== 测试内置 Debug/Info/Error 结构化日志 ===");
        
        // 写入不同级别的内置日志
        Logg.Information("用户 {UserId} 登录了系统, IP: {Ip}", 10086, "192.168.1.1");
        Logg.Debug("系统启动参数: {Params}", new { Threads = 4, Memory = "8GB" });
        Logg.Error(new InvalidOperationException("模拟异常"), "处理订单 {OrderId} 失败", "ORD-9981");
        
        await Logg.FlushAsync();

        int count = 0;
        string? message = null;
        string? properties = null;
        string? exception = null;

        // 验证数据落盘情况
        using (var conn = new SqliteConnection($"Data Source={DbPath};"))
        {
            conn.Open();
            using var cmdCount = conn.CreateCommand();
            cmdCount.CommandText = "SELECT COUNT(*) FROM [_def_logs_];";
            count = Convert.ToInt32(cmdCount.ExecuteScalar()!);
            
            using var cmdRead = conn.CreateCommand();
            cmdRead.CommandText = "SELECT Message, Properties, Exception FROM [_def_logs_] WHERE Message LIKE '%ORD-9981%';";
            using var reader = cmdRead.ExecuteReader();
            if (reader.Read())
            {
                message = reader.GetString(0);
                properties = reader.IsDBNull(1) ? null : reader.GetString(1);
                exception = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
        }

        Assert.AreEqual(3, count, "内置日志写入数量不对");
        Assert.AreEqual("处理订单 ORD-9981 失败", message);
        
        // 验证占位符被正确提取为 JSON 属性
        Assert.IsTrue(properties?.Contains("OrderId") == true && properties?.Contains("ORD-9981") == true, "属性 JSON 序列化失败");
        Assert.IsTrue(exception?.Contains("模拟异常"), "异常信息未保存");
        
        Log("✅ 成功验证内置结构化日志及 JSON 属性序列化！\n");
    }

    /// <summary>
    /// 测试 2：验证各种自定义泛型 T 的注册与写入
    /// </summary>
    [TestMethod]
    public async Task TestCustomGenericT()
    {
        Log("=== 测试自定义泛型 T 注册与写入 ===");

        //Logg.Register<CustomBusinessLog>(
        //    writer: log => {
        //        var job = new LogJob(
        //            "INSERT INTO [CustomLogs] ([Module], [Code]) VALUES ($m, $c)",
        //            cmd => {
        //                cmd.Parameters.AddWithValue("$m", log.Module);
        //                cmd.Parameters.AddWithValue("$c", log.Code);
        //            },
        //            "CustomLogs"
        //        );
        //        Logg.Enqueue(job);
        //    },
        //    "CustomLogs",
        //    "CREATE TABLE IF NOT EXISTS [CustomLogs] ([Id] INTEGER PRIMARY KEY, [Module] TEXT, [Code] INTEGER);"
        //);



        Logg.Write(new CustomBusinessLog { Module = "Payment", Code = 200 });
        Logg.Write(new CustomBusinessLog { Module = "Auth", Code = 401 });

        await Logg.FlushAsync();
          Logg.ForceCheckpoint();

        int count = 0;
        using (var conn = new SqliteConnection($"Data Source={DbPath};"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM [CustomBusinessLog];";
            count = Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        Assert.AreEqual(2, count, "自定义泛型数据未成功写入");
        Log("✅ 成功验证自定义泛型 T 注册与写入！\n");
    }

    /// <summary>
    /// 测试 3：核心兜底机制 - 当插入报错无表时，自动建表并重新插入
    /// </summary>
    [TestMethod]
    public async Task TestAutoCreateTableOnMissing()
    {
        Log("=== 测试“无表报错时，自动建表并重新插入”机制 ===");
        
        // 1. 注册自定义类型，此时 Logg 内部会缓存建表语句并物理创建表
        //Logg.Register<AuditLog>(
        //    writer: log => {
        //        var job = new LogJob(
        //            "INSERT INTO [AuditLogs] ([Action], [Time]) VALUES ($a, $t)",
        //            cmd => {
        //                cmd.Parameters.AddWithValue("$a", log.Action);
        //                cmd.Parameters.AddWithValue("$t", log.Time.ToString("o"));
        //            },
        //            "AuditLogs" // 关键：传入表名以激活自动建表重试机制
        //        );
        //        Logg.Enqueue(job);
        //    },
        //    "AuditLogs",
        //    "CREATE TABLE IF NOT EXISTS [AuditLogs] ([Id] INTEGER PRIMARY KEY, [Action] TEXT, [Time] TEXT);"
        //);

        // 2. 手动把表删掉，模拟表意外丢失或外部工具删除的情况
        using (var conn = new SqliteConnection($"Data Source={DbPath};"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP TABLE IF EXISTS [AuditLog];";
            cmd.ExecuteNonQuery();
            Log(" -> 已手动 DROP 掉 AuditLog 表");
        }

        // 3. 尝试写入数据。此时会触发 SQLite "no such table" 异常
        // 后台线程应自动捕获该异常，从缓存读取 SQL 重建表，然后重试插入
        Logg.Write(new AuditLog { Action = "UserDeleted", Time = DateTime.UtcNow });
        Logg.Write(new AuditLog { Action = "SystemRestart", Time = DateTime.UtcNow });

        await Logg.FlushAsync();

        // 4. 验证表是否被悄悄重建，且数据成功落盘
        int count = 0;
        using (var conn = new SqliteConnection($"Data Source={DbPath};"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM [AuditLog];";
            count = Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        Assert.AreEqual(2, count, "自动建表机制失效，数据丢失！");
        Log("✅ 成功验证无表时自动捕获异常、重建表并保证数据零丢失！\n");
    }
}

// 测试用的实体类
public class CustomBusinessLog
{
    public string Module { get; set; } = "";
    public int Code { get; set; }
}

public class AuditLog
{
    public string Action { get; set; } = "";
    public DateTime Time { get; set; }
}