using FMO.Models;
using LiteDB;

namespace FMO.Utilities;

/// <summary>
/// 文件索引
/// </summary>
public static class FileIndexService
{
    public static FileIndexDatabase Database { get; }

    public static ILiteCollection<FileStorageInfo> Collection { get; }

    public static FileStorageInfo? Find(int id) => Collection.FindById(id);


    public static bool Store(FileStorageInfo f) => Collection.Upsert(f);


    static FileIndexService()
    {
        Database = new();
        Collection = Database.GetCollection<FileStorageInfo>();
    }






}