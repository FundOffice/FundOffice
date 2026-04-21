namespace FMO.Models;

public class Manager : Institution
{ 
    /// <summary>
    /// Amac中的id
    /// </summary>
    public required string AmacId { get; set; }

    /// <summary>
    /// 是否是主体
    /// </summary>
    public bool IsMaster { get; set; }


    /// <summary>
    /// 注册号
    /// </summary>
    public required string RegisterNo { get; init; }

    /// <summary>
    /// 注册日期
    /// </summary>
    public DateOnly RegisterDate { get; set; }

    /// <summary>
    /// 基金数
    /// </summary>
    public int FundCount { get; set; }
    
    /// <summary>
    /// 有没有信用提示
    /// </summary>
    public bool HasCreditTips { get;  set; }
    
    /// <summary>
    /// 有没有特殊提示
    /// </summary>
    public bool HasSpecialTips { get;  set; }

    /// <summary>
    /// 会员类型
    /// </summary>
    public string? MemberType { get;  set; }

    // 是否可以是投顾
    public bool Advisorable { get; set; }

    /// <summary>
    /// 规模
    /// </summary>
    public string? ScaleRange { get; set; }

    /// <summary>
    /// 在AMAC中的注册资本
    /// </summary>
    public decimal RegisterCapitalAmac { get; set; }

    /// <summary>
    /// amac中的实缴
    /// </summary>
    public decimal RealCapitalAmac { get; set; }

    /// <summary>
    /// AMAC中的股份比例
    /// </summary>
    //public s[]? ShareInfoAmac { get; set; }

    /// 介绍
    public string? Description { get; set; }
}
