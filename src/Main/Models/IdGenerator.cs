using System.Collections.Concurrent;
using System.Text.Json;

namespace FMO.Models;

public static class IdGenerator
{
    private static readonly ConcurrentDictionary<string, int> _idCounters = new();
    private static readonly Lock _fileLock = new ();
    private const string StorageFile = "config\\id_store.json";
    private const string TempExtension = ".tmp";

    static IdGenerator()
    {
        LoadFromFile();
    }

    public static int GetNextId(string key)
    {
        int newId = _idCounters.AddOrUpdate(
            key,
            _ => 1,
            (_, current) => Interlocked.Increment(ref current)
        );

        SchedulePersist();
        return newId;
    }

    #region 优化后的持久化实现
    private static void LoadFromFile()
    {
        lock (_fileLock)
        {
            if (!File.Exists(StorageFile)) return;

            using var fs = new FileStream(
                StorageFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            );
            var data = JsonSerializer.Deserialize<ConcurrentDictionary<string, int>>(fs) ?? new();
            foreach (var kvp in data)
            {
                _idCounters[kvp.Key] = kvp.Value;
            }
        }
    }

    //private static int _persistCount;
    private static void SchedulePersist()
    {
        // 每10次变更触发一次持久化（可调节阈值）
        //if (Interlocked.Increment(ref _persistCount) % 10 != 0) return;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                SaveToFile();
            }
            catch //(Exception ex)
            {
                // 添加日志记录
                //Log.Error($"持久化失败: {ex.Message}");
            }
        });
    }

    private static void SaveToFile()
    {
        lock (_fileLock)
        {
            var tempPath = StorageFile + TempExtension;

            try
            {
                // 序列化到临时文件
                using (var fs = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    JsonSerializer.Serialize(fs, _idCounters);
                }

                // 原子替换操作
                File.Replace(
                    sourceFileName: tempPath,
                    destinationFileName: StorageFile,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            finally
            {
                // 清理残留临时文件
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
    #endregion
}