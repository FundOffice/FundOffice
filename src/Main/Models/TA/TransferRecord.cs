using FMO.Utilities;
using System.ComponentModel;

namespace FMO.Models;

public record ManualLinkOrder(int Id, int OrderId, string ExternalId, string ExternalRequestId);

/// <summary>
/// TA 记录
/// 更新后应该 调用更新  db.BuildFundShareRecord 
/// </summary>
[Description("TA记录")]
[UpdateBy(typeof(TransferRecord))]
public class TransferRecord// : IEquatable<TransferRecord>
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int RequestId { get; set; }

    public int FundId { get; set; }

    public string? FundName { get; set; }

    public string? FundCode { get; set; }


    public string? ExternalId { get; set; }

    public string? ExternalRequestId { get; set; }


    /// <summary>
    /// 份额类型
    /// </summary>
    public string? ShareClass { get; set; }


    /// <summary>
    /// 内部id
    /// </summary>
    public int InvestorId { get; set; }

    /// <summary>
    /// 客户Id
    /// </summary>
    [Description("证件号码")]
    public required string InvestorIdentity { get; set; }

    [Description("客户名称")]
    public required string InvestorName { get; set; }


    public DateOnly CreateDate { get; set; }

    /// <summary>
    /// 申请时间
    /// </summary>
    [Description("申请日期")]
    public DateOnly RequestDate { get; set; }

    /// <summary>
    /// 确认时间
    /// </summary>
    [Description("确认日期")]
    public DateOnly ConfirmedDate { get; set; }

    /// <summary>
    /// 交易类型
    /// </summary> 
    [Description("业务类型")]
    public TransferRecordType Type { get; set; }

    /// <summary>
    /// 申请份额
    /// </summary>
    [Description("申请份额")]
    public decimal RequestShare { get; set; }

    /// <summary>
    /// 申请金额
    /// </summary>
    [Description("申请金额")]
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 确认份额
    /// </summary>
    [Description("确认份额")]
    public decimal ConfirmedShare { get; set; }

    /// <summary>
    /// 确认金额
    /// </summary>
    [Description("确认金额")]
    public decimal ConfirmedAmount { get; set; }

    /// <summary>
    /// 确认净额（扣费）
    /// </summary>
    [Description("确认净额")]
    public decimal ConfirmedNetAmount { get; set; }

    /// <summary>
    /// 手续费用
    /// </summary>
    [Description("交易费用")]
    public decimal Fee { get; set; }

    /// <summary>
    /// 业绩报酬
    /// </summary>
    [Description("业绩报酬")]
    public decimal PerformanceFee { get; set; }

    [Description("销售机构")]
    public string? Agency { get; set; }

    /// <summary>
    /// 表示是清盘时的赎回记录
    /// 没有order 和 request 
    /// </summary>
    public bool IsLiquidating { get; set; }


    /// <summary>
    /// 数据来源
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 确认函文件名
    /// </summary>
    public string? ConfirmFile { get; set; }

    /// <summary>
    /// 确认失败
    /// </summary>
    public bool IsFailed { get; set; }

    /// <summary>
    /// 后台生成的TA
    ///  互投的产品，托管后台赎回付费，没有order request
    ///  自动申购母基金等
    /// </summary>
    public bool Background { get; set; }


    public bool IsOrderRequired => TAHelper.RequiredOrder(Type);

    public override int GetHashCode()
    {
        return FundId ^ InvestorName.GetHashCode() ^ RequestDate.GetHashCode() ^ Type.GetHashCode();
    }
    //public bool Equals(TransferRecord? other)
    //{
    //    if (other is null) return false;

    //    return FundId == other.FundId && InvestorIdentity == other.InvestorIdentity && RequestDate == other.RequestDate && Type == other.Type;
    //}

    public decimal ShareChange()
    {
        switch (Type)
        {
            case TransferRecordType.Subscription:
            case TransferRecordType.Purchase:
            case TransferRecordType.Increase:
            case TransferRecordType.Distribution:
                return ConfirmedShare;
            case TransferRecordType.Redemption:
            case TransferRecordType.ForceRedemption:
            case TransferRecordType.Decrease:
                return -ConfirmedShare;
            default:
                return 0;
        }
    }
    public decimal AmountChange()
    {
        switch (Type)
        {
            case TransferRecordType.Subscription:
            case TransferRecordType.Purchase:
            case TransferRecordType.Increase:
            case TransferRecordType.Distribution:
                return ConfirmedAmount;
            case TransferRecordType.Redemption:
            case TransferRecordType.ForceRedemption:
            case TransferRecordType.Decrease:
                return -ConfirmedAmount;
            default:
                return 0;
        }
    }


    public bool IsTypePair(TransferOrderType type)
    {
        if ((Type == TransferRecordType.Subscription || Type == TransferRecordType.Purchase) && (type == TransferOrderType.FirstTrade || type == TransferOrderType.Buy))
            return true;
        else if ((Type == TransferRecordType.Redemption || Type == TransferRecordType.ForceRedemption) && (type switch { TransferOrderType.Amount or TransferOrderType.Share or TransferOrderType.RemainAmout => true, _ => false }))
            return true;

        return false;
    }

    public bool IsOrderPair(TransferOrder order)
    {
        switch (Type)
        {
            case TransferRecordType.Subscription:
            case TransferRecordType.Purchase:
                return order.Type switch { TransferOrderType.FirstTrade or TransferOrderType.Buy => true, _ => false } && order.Number == RequestAmount;

            case TransferRecordType.Redemption:
            case TransferRecordType.ForceRedemption:
                switch (order.Type)
                {
                    case TransferOrderType.Share:
                        return order.Number == RequestShare;
                    case TransferOrderType.Amount:
                    case TransferOrderType.RemainAmout:
                        return order.Number == RequestAmount;
                    default:
                        return false;
                }
            default:
                return false;
        }

    }
}

