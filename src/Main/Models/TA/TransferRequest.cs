using FMO.Utilities;
using System.ComponentModel;

namespace FMO.Models;


/// <summary>
/// 交易申请
/// </summary>
[UpdateBy(typeof(TransferRequest))]
public class TransferRequest
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    /// <summary>
    /// 在托管外包系统中的id
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// 平台identifier
    /// </summary>
    public string? Source { get; set; }


    /// <summary>
    /// 内部id
    /// </summary>
    public int InvestorId { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public required string InvestorName { get; set; }

    /// <summary>
    /// 客户证件
    /// </summary>
    public required string InvestorIdentity { get; set; }

    /// <summary>
    /// 创建日期
    /// </summary>
    public DateOnly CreateDate { get; set; }

    /// <summary>
    /// 申请日期（开放日）
    /// </summary>
    public DateOnly RequestDate { get; set; }

    /// <summary>
    /// 内部Id
    /// </summary>
    public int FundId { get; set; }

    /// <summary>
    /// 基金名称 存在xxxA\ xxxxB等形式
    /// </summary>
    public required string FundName { get; set; }

    /// <summary>
    /// 代码 存在xxxxxA、xxxxxB与基金代码不一致
    /// </summary>
    public string? FundCode { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    public TransferRequestType RequestType { get; set; }

    /// <summary>
    /// 申请份额
    /// </summary> 
    public decimal RequestShare { get; set; }

    /// <summary>
    /// 申请金额
    /// </summary> 
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 巨额赎回处理方式
    /// </summary>
    public LargeRedemptionFlag LargeRedemptionFlag { get; set; }

    /// <summary>
    /// 费用折扣
    /// </summary>
    public decimal FeeDiscount { get; set; }


    /// <summary>
    /// 费用折扣
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// 销售机构
    /// </summary>
    public string? Agency { get; set; }

    public string? ShareClass { get; set; }

    /// <summary>
    /// 是否被取消
    /// </summary>
    public bool IsCanceled { get; set; }

    /// <summary>
    /// 清盘时的申请，托管自动生成
    /// </summary>
    public bool IsLiquidating { get; set; }

    /// <summary>
    /// 需要order
    /// </summary>
    public bool IsOrderRequired => TAHelper.RequiredOrder(RequestType);
}

