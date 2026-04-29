using FMO.Utilities;
using LiteDB;
using System.Collections.Concurrent;

namespace FMO.Trustee;

public class JsonBase
{
    private static ILiteDatabase _db { get; } = new LiteDatabase(@$"FileName=data\platformlog.db;Connection=Shared");

    private static ConcurrentBag<UnusualType> _unusualTypes = new();

    private static Debouncer debouncer = new(SaveUnusual);
     

    public int Id { get; set; }


    public virtual string? JsonId => null;

    protected static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        if (decimal.TryParse(value, out var result))
            return result;

        throw new FormatException($"无法将 '{value}' 解析为decimal类型");
    }

    public static void ReportJsonUnexpected(string identifier, string method, string info)
    {
        _db.GetCollection<TrusteeJsonUnexpected>().Insert(new TrusteeJsonUnexpected(identifier, method, info));
    }

    public static void ReportSpecialType(UnusualType types)
    {
        using var db = DbHelper.Base();
        db.GetCollection<UnusualType>().Insert(types);
        //_unusualTypes.Add(types);
       // debouncer.Invoke();
    }
    private static void SaveUnusual()
    {
        
        using var db = DbHelper.Base();
        db.GetCollection<UnusualType>().Insert(_unusualTypes);
    }


    protected static string ParseCurrency(string currencyCode)
    {
        switch (currencyCode)
        {
            case "156":
                return "CNY"; // 人民币
            case "250":
                return "CHF"; // 瑞士法郎
            case "280":
                return "DEM"; // 德国马克（已停用）
            case "344":
                return "HKD"; // 港元
            case "392":
                return "JPY"; // 日元
            case "826":
                return "GBP"; // 英镑
            case "840":
                return "USD"; // 美元
            case "954":
                return "EUR"; // 欧元
            default:
                return "";  // 或者抛出异常，根据需要处理无效编码
        }
    }
}

