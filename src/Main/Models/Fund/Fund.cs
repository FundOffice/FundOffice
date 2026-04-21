using System.Text.RegularExpressions;

namespace FMO.Models;


/// <summary>
/// 产品
/// </summary>
public class Fund
{
    /// <summary>
    /// bson id
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }

    public string? ShortName { get; set; }

    /// <summary>
    /// 发起日期
    /// </summary>
    public DateOnly InitiateDate { get; set; }

    /// <summary>
    /// 成立日期
    /// </summary>
    public DateOnly SetupDate { get; set; }

    /// <summary>
    /// 备案日期
    /// </summary>
    public DateOnly AuditDate { get; set; }

    /// <summary>
    /// 备案号
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 最新更新日期
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// 清算日期
    /// </summary>
    public DateOnly ClearDate { get; set; }

    /// <summary>
    /// 在协会的id
    /// </summary>
    //public long AmacId { get; set; }

    public string? AmacID { get; set; }


    /// <summary>
    /// 公示网址
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public FundStatus Status { get; set; }

    /// <summary>
    /// 是否作为投资顾问
    /// </summary>
    public bool AsAdvisor { get; set; }


    /// <summary>
    /// 公示信息同步时间
    /// </summary>
    public DateTime PublicDisclosureSynchronizeTime { get; set; }

    /// <summary>
    /// 备案系统同步时间
    /// </summary>
    public DateTime AmbersSynchronizeTime { get; set; }

    /// <summary>
    /// 结束日期
    /// </summary>
    //public Mutable<DateOnly>? ExpirationDate { get; set; }

    /// <summary>
    /// 存续期
    /// </summary>
    //public Mutable<string>? SurvivalPeriod { get; set; }


    /// <summary>
    /// 是否结构化
    /// </summary>
    //public Mutable<bool>? IsStructured { get; set; } //= new Mutable<bool>(nameof(IsStructured), false);


    //public Mutable<ShareClass[]>? ShareClasses { get; set; }


    /// <summary>
    /// 基金类型
    /// </summary>
    public FundType Type { get; set; }

    /// <summary>
    /// 管理类型
    /// </summary>
    public ManageType ManageType { get; set; }

    /// <summary>
    /// 托管人
    /// </summary>
    public string? Trustee { get; set; }


     


    public static string? GetDefaultShortName(string? name) => string.IsNullOrWhiteSpace(name) ? null : Regex.Replace(name, @"私募\w+基金|集合\w+计划", "");
}
