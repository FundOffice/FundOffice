using System.ComponentModel;

namespace FMO.TPL;


public class ExportInfo
{
    public ExportInfo()
    {
    }

    public ExportInfo(string v)
    {
        FailedReason = v;
    }



    public string? FileName { get; set; }

    public string Filter { get; set; } = "文件|*.*";

    public object? Data { get; set; }


    public string? FailedReason { get; set; }
}

[Flags]
public enum ExportTypeFlag
{
    None,

    /// <summary>
    /// 单一基金净值列表
    /// 需要支持以下属性 
    /// 集合名为dy
    /// - Date: 日期
    /// - NetValue: 单位净值
    /// - CumNetValue: 累计净值
    /// - Asset: 资产净值
    /// - NetAsset: 净资产
    /// </summary>
    SingleFundNetValueList,

    MultiFundElementSheet = 2,

    MultiFundSummary = 4,

    /// <summary>
    /// 持有人名册
    /// </summary>
    SingleFundHolderSheet = 8,


    Custom = 99999,
}

public interface IExporter
{

    string Id { get; }

    string Name { get; }

    string Description { get; }

    ExportTypeFlag Suit { get; }

    ExportParameterMeta[]? Meta { get; }

    ExportInfo Generate(object? parameter = null);
}




/// <summary>
/// 导出的参数元数据
/// 比如 Fund Investor 等
/// 如果未定义parameter，可以通过meta选择参数
/// </summary>
/// <param name="Type"></param>
/// <param name="Multiple"></param>
/// <param name="Direction"></param>
public record ExportParameterMeta(string Type, bool Multiple = true, ListSortDirection Direction = ListSortDirection.Ascending);

public record TemplateInfo(string Id, string Name, string Description, string Type, ExportTypeFlag Suit, ExportParameterMeta[]? Meta, string Entry);
