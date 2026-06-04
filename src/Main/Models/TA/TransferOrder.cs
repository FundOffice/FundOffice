using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum TransferOrderType
{
    [Description("首次买入")] FirstTrade,

    [Description("追加申购")] Buy,

    [Description("份额赎回")] Share,

    [Description("金额赎回")] Amount,

    [Description("赎回至指定金额")] RemainAmout,
}


[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum OrderStatus
{
    None,

    /// <summary>
    /// 投资人签署
    /// </summary>
    [Description("已签署")] Signed,

    /// <summary>
    /// 管理人接受申请
    /// </summary>
    [Description("已受理")] Accepted
}


/// <summary>
/// 交易订单
/// </summary>
public class TransferOrder
{
    public int Id { get; set; }

    public string FlowId => TransferFlow.MakeId(TransferFlowType.Order, Id, OpenDate); 

    public int InvestorId { get; set; }

    public int FundId { get; set; }

    /// <summary>
    /// 份额类型
    /// </summary>
    public string? ShareClass { get; set; }

    public required string FundName { get; set; }

    public required string FundCode { get; set; }


    /// <summary>
    /// sign date
    /// </summary>
    public DateOnly Date { get; set; }


    public DateOnly OpenDate { get; set; }

    public TransferOrderType Type { get; set; }

    public decimal Number { get; set; }

    /// <summary>
    /// 交易费
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// 客户Id
    /// </summary>
    [Description("证件号码")]
    public required string InvestorIdentity { get; set; }

    [Description("客户名称")]
    public required string InvestorName { get; set; }


    public DateOnly CreateDate { get; set; }


    public OrderStatus Status { get; set; }

    public string? ExternalId { get; set; }

    public string? Source { get; set; }

    /// <summary>
    /// 已废弃
    /// </summary>
    public bool IsAborted { get; set; }


    /// <summary>
    /// 合同
    /// </summary>
    public SimpleFile? Contract { get; set; }

    /// <summary>
    /// 风险揭示
    /// </summary>
    public SimpleFile? RiskDiscloure { get; set; }

    /// <summary>
    /// 认申购、赎回单
    /// </summary>
    public SimpleFile? OrderSheet { get; set; }

    /// <summary>
    /// 风险匹配
    /// </summary>
    public SimpleFile? RiskPair { get; set; }

    /// <summary>
    /// 双录
    /// </summary>
    public SimpleFile? Videotape { get; set; }


    /// <summary>
    /// 回访
    /// </summary>
    public SimpleFile? Review { get; set; }

  
}
