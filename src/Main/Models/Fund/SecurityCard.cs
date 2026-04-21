namespace FMO.Models;


public enum SecurityCardType
{
    Unk,

    ShangHai,

    ShenZhen
}



/// <summary>
/// 股东卡
/// </summary>
public class SecurityCard  
{
    public string Id => CardNo;

    public int FundId { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    public required string SerialNo { get; set; }

    /// <summary>
    /// 子账户号
    /// </summary>
    public required string CardNo { get; set; }

    /// <summary>
    /// 一码通
    /// </summary>
    public required string UniversalNo { get; set; }

    public required string Name { get; set; }

    public string? FundCode { get; set; }

    /// <summary>
    /// 沪、深
    /// </summary>
    public SecurityCardType Type { get; set; }

    /// <summary>
    /// 申请日期
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// 是否注销
    /// </summary>
    public bool IsDeregistered { get; set; }

    /// <summary>
    /// 组
    /// </summary>
    public int Group { get; set; }
}


public class SecurityCardChange
{
    public string Id => SerialNo;

    public DateOnly Date { get; set; }
     

    public int FundId { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    public required string SerialNo { get; set; }

    public required string Name { get; set; }

    public required string UniversalNo { get; set; }
}

