using System.ComponentModel;

namespace FMO.Models;


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum TransctionDirection { [Description("付")] Pay, [Description("收")] Receive, [Description("取消")] Cancel };

/// <summary>
/// 银行流水
/// </summary>
public class BankTransaction
{
    public required string Id { get; set; }

    /// <summary>
    /// 方向
    /// </summary>
    public TransctionDirection Direction { get; set; }

    /// <summary>
    /// 本方账号
    /// </summary>
    public required string AccountNo { get; set; }

    public required string AccountName { get; set; }

    public required string AccountBank { get; set; }


    /// <summary>
    /// 对方账号
    /// id 为BankAccount id
    /// </summary>
    public required string CounterNo { get; set; }

    public required string CounterName { get; set; }

    public required string CounterBank { get; set; }


    public DateTime Time { get; set; }

    public string Currency { get; set; } = "RMB";

    /// <summary>
    /// 余额
    /// </summary>
    public decimal? Balance { get; set; }

    /// <summary>
    /// 发生金额
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    public required string Serial { get; set; }
}


/// <summary>
/// 本方账号关联基金
/// </summary>
public class RaisingBankTransaction : BankTransaction
{

    public int FundId { get; set; }

    public string? FundCode { get; set; }

}