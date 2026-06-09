namespace FMO.Models;

/// <summary>
/// 包含全部基金信息的聚合类
/// </summary>
public partial class ReadonlyFundInfo
{
    public int Id { get; set; }
     
    /// <summary>
    /// 管理人名称
    /// </summary>
    //public required string ManagerName { get; set; }

   
    /// <summary>
    /// 管理人名称
    /// </summary>
    //public string? ManagerEnglishName { get; set; }

    /// <summary>
    /// 管理人备案号
    /// </summary>
    //public required string ManagerAmacCode { get; set; }

    /// <summary>
    /// 管理人简介
    /// </summary>
    public string? ManagerProfile { get; set; }


    /// <summary>
    /// 成立日期 yyyy-MM-dd
    /// </summary>
    //public required string SetupDate { get; set; }

    /// <summary>
    /// 备案日期  yyyy-MM-dd
    /// </summary>
    public string? AuditDate { get; set; }

    /// <summary>
    /// 备案号
    /// </summary>
    //public required string Code { get; set; }



    /// <summary>
    /// 公示网址
    /// </summary>
    public string? Url { get; set; }
     

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
    public string? AmacID { get; set; }
     

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
 

 
}