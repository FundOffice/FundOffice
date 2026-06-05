using FMO.Utilities;

namespace FMO.Models;


/// <summary>
/// 交易申请
/// </summary>
[UpdateBy(typeof(TransferRequest))]
public class TransferRequest
{
    public int Id { get; set; }

    public int OrderId { get; set; }



    public string FlowId { get; set; } = null!;

    //public string FlowId => RequestType switch
    //{
    //    TransferRequestType.InitialOffer => TransferFlow.MakeId(TransferFlowType.SetUp, FundId, RequestDate),
    //    TransferRequestType.Distribution => TransferFlow.MakeId(TransferFlowType.Dividend, FundId, RequestDate),
    //    TransferRequestType.BonusType => TransferFlow.MakeId(TransferFlowType.Desire, FundId, RequestDate),
    //    TransferRequestType.Abort => "Canceled",
    //    TransferRequestType.Purchase or TransferRequestType.Subscription or TransferRequestType.Redemption or TransferRequestType.ForceRedemption =>
    //            OrderId != 0 ? TransferFlow.MakeId(TransferFlowType.Order, OrderId, RequestDate) : TransferFlow.MakeId(TransferFlowType.OrderMissing, Id, RequestDate),
    //    TransferRequestType.Increase or TransferRequestType.Decrease => TransferFlow.MakeId(TransferFlowType.Adjustment, FundId, RequestDate),
    //    TransferRequestType.TransferIn or TransferRequestType.TransferOut => TransferFlow.MakeId(TransferFlowType.Transfer, FundId, RequestDate),
    //    TransferRequestType.SwitchIn or TransferRequestType.SwitchOut => TransferFlow.MakeId(TransferFlowType.Convert, FundId, RequestDate),
    //    _ => "Unknown"
    //};

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
    /// 代码
    /// </summary>
    public required string FundCode { get; set; }

    public string? ShareClass { get; set; }

    public string? ShareCode { get; set; }


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

    /// <summary>
    /// 是否被取消
    /// </summary>
    public bool IsCanceled { get; set; }

    /// <summary>
    /// 清盘时的申请，托管自动生成
    /// 不在使用
    /// </summary>
    //public bool IsLiquidating { get; set; }

    /// <summary>
    /// 需要order
    /// </summary>
    public bool IsOrderRequired => TAHelper.RequiredOrder(RequestType);
}

