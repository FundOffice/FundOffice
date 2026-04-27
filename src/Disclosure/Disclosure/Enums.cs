using FMO.Models;
using System.ComponentModel;

namespace FMO.Disclosure;

/// <summary>
/// 报告格式
/// </summary>
[Flags]
public enum DisclosureFormat
{
    None = 0,
    Excel = 1 << 0,
    Pdf = 1 << 1,
    Word = 1 << 2,
    Xbrl = 1 << 3,
    Sealed = 1 << 4,  // 用印PDF（年报专用）
}

/// <summary>
/// 信批状态
/// </summary>
[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum DisclosureStatus
{
    /// <summary>
    /// 待提交 / 未执行
    /// </summary>
    [Description("初始化")]
    Create,
    Pending=Create,

    [Description("等待执行")]
    Waiting,


    /// <summary>
    /// 已取消
    /// </summary>
    [Description("已停止")]
    Stopped,

    /// <summary>
    /// 处理中
    /// </summary>
    [Description("处理中")]
    Processing,

    

    /// <summary>
    /// 已发布
    /// </summary>
    [Description("已发布")]
    Successed,

    /// <summary>
    /// 执行失败
    /// </summary>
    [Description("失败")]
    Failed,
}

/// <summary>
/// 信批通道代码
/// </summary>
public static class DisclosureChannelCode
{
    public const string Email = "email";
    public const string Pfid = "pfid";
    public const string MeiShi = "meishi";
    public const string AMAC = "amac";      // 基金业协会
    public const string Custom = "custom";  // 自定义/其他平台

    public const string QuarterlyUpdate = "quarterly_update"; // 季度更新（特殊通道，非公告披露）
}

/// <summary>
/// 扩展方法
/// </summary>
public static class DisclosureTypeExtensions
{
    /// <summary>
    /// 获取指定信批类型支持的格式
    /// </summary>
    public static DisclosureFormat GetSupportedFormats(this DisclosureType type)
    {
        return type switch
        {
            DisclosureType.Monthly or DisclosureType.Quarterly or DisclosureType.SemiAnnually =>
                DisclosureFormat.Excel | DisclosureFormat.Pdf,
            DisclosureType.Annually =>
                DisclosureFormat.Excel | DisclosureFormat.Pdf | DisclosureFormat.Word | DisclosureFormat.Xbrl | DisclosureFormat.Sealed,
            DisclosureType.Temporary =>
                DisclosureFormat.Pdf | DisclosureFormat.Word,
            _ => DisclosureFormat.None
        };
    }

    /// <summary>
    /// 获取类型名称
    /// </summary>
    public static string GetName(this DisclosureType type)
    {
        return type switch
        {
            DisclosureType.Monthly => "月报",
            DisclosureType.Quarterly => "季报",
            DisclosureType.SemiAnnually => "半年报",
            DisclosureType.Annually => "年报",
            DisclosureType.Temporary => "临时报告",
            DisclosureType.TemporaryOpen => "临时开放",
            DisclosureType.HugeRedemption => "巨额赎回",
            DisclosureType.FundSetup => "基金成立",
            _ => type.ToString()
        };
    }
}
