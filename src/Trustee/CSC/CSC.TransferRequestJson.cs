using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCSC;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

public class TransferRequestJson : JsonBase
{
    /// <summary>
    /// 申请日期（格式：yyyyMMdd）
    /// </summary>
    [JsonPropertyName("applyDate")]
    public string ApplyDate { get; set; }

    /// <summary>
    /// 基金代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 基金名称
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 销售商代码
    /// </summary>
    [JsonPropertyName("agencyNo")]
    public string AgencyNo { get; set; }

    /// <summary>
    /// 销售商名称
    /// </summary>
    [JsonPropertyName("agencyName")]
    public string AgencyName { get; set; }

    /// <summary>
    /// 投资者名称
    /// </summary>
    [JsonPropertyName("custName")]
    public string CustName { get; set; }

    /// <summary>
    /// 基金账号
    /// </summary>
    [JsonPropertyName("fundAcco")]
    public string FundAcco { get; set; }

    /// <summary>
    /// 交易账号
    /// </summary>
    [JsonPropertyName("tradeAcco")]
    public string TradeAcco { get; set; }

    /// <summary>
    /// 银行账号
    /// </summary>
    [JsonPropertyName("bankAcco")]
    public string BankAcco { get; set; }

    /// <summary>
    /// 银行编号
    /// </summary>
    [JsonPropertyName("bankNo")]
    public string BankNo { get; set; }

    /// <summary>
    /// 开户行名称
    /// </summary>
    [JsonPropertyName("openBankName")]
    public string OpenBankName { get; set; }

    /// <summary>
    /// 银行户名
    /// </summary>
    [JsonPropertyName("nameInBank")]
    public string NameInBank { get; set; }

    /// <summary>
    /// 客户类型
    /// </summary>
    [JsonPropertyName("custType")]
    public string CustType { get; set; }

    /// <summary>
    /// 证件类型
    /// </summary>
    [JsonPropertyName("certiType")]
    public string CertiType { get; set; }

    /// <summary>
    /// 证件号
    /// </summary>
    [JsonPropertyName("certiNo")]
    public string CertiNo { get; set; }

    /// <summary>
    /// TA发起标志
    /// </summary>
    [JsonPropertyName("taFlag")]
    public string TaFlag { get; set; }

    /// <summary>
    /// 分红方式
    /// </summary>
    [JsonPropertyName("bonusType")]
    public string BonusType { get; set; }

    /// <summary>
    /// 申请金额
    /// </summary>
    [JsonPropertyName("balance")]
    public string Balance { get; set; }

    /// <summary>
    /// 申请份额
    /// </summary>
    [JsonPropertyName("shares")]
    public string Shares { get; set; }

    /// <summary>
    /// 确认金额
    /// </summary>
    [JsonPropertyName("confBalance")]
    public string ConfBalance { get; set; }

    /// <summary>
    /// 手续费折扣率
    /// </summary>
    [JsonPropertyName("discountRate")]
    public string DiscountRate { get; set; }

    /// <summary>
    /// 申请业务类型
    /// </summary>
    [JsonPropertyName("appBusiFlag")]
    public string AppBusiFlag { get; set; }

    /// <summary>
    /// 原外部系统的申请流水号
    /// </summary>
    [JsonPropertyName("originalNo")]
    public string OriginalNo { get; set; }

    /// <summary>
    /// 确认状态
    /// </summary>
    [JsonPropertyName("confStatus")]
    public string ConfStatus { get; set; }

    /// <summary>
    /// 确认结果描述
    /// </summary>
    [JsonPropertyName("describe")]
    public string Describe { get; set; }

    public override string? JsonId => $"{CSC._Identifier}.{OriginalNo}";

    public TransferRequest ToObject()
    {
        return new TransferRequest
        {
            InvestorIdentity = CertiNo,
            InvestorName = CustName,
            FundName = FundName,
            FundCode = FundCode,
            Agency = AgencyName,
            CreateDate = DateOnly.FromDateTime(DateTime.Now),
            ExternalId = $"{CSC._Identifier}.{OriginalNo}", 
            RequestDate = DateOnly.ParseExact(ApplyDate, "yyyyMMdd"),
            RequestAmount = ParseDecimal(Balance),
            RequestShare = ParseDecimal(Shares),
            FeeDiscount = ParseDecimal(DiscountRate),
            Source = "api",
            RequestType = ParseType(AppBusiFlag),
            // LargeRedemptionFlag = TaFlag
        };
    }

    private TransferRequestType ParseType(string requestTypeStr)
    {
        // 注释不关心的项
        var t = requestTypeStr switch
        {
            "20" => TransferRequestType.Subscription,
            "22" => TransferRequestType.Purchase,
            "24" => TransferRequestType.Redemption,
            "25" => TransferRequestType.Redemption,
            //"26" => TransferRequestType.Transfer,
            "27" => TransferRequestType.TransferIn,
            "28" => TransferRequestType.TransferOut,
            "29" => TransferRequestType.BonusType,
            //"30" => TransferRequestType.Subscription, 认购结果，实际没有返回，反而20是对应record
            //"31" => TransferRequestType.Freeze,
            //"32" => TransferRequestType.Unfreeze,
            "33" => TransferRequestType.TransferOut,
            "34" => TransferRequestType.TransferIn,
            //"35" => TransferRequestType.NonTradeTransferOut,
            "36" => TransferRequestType.SwitchOut,
            "37" => TransferRequestType.SwitchIn,
            //"39" => TransferRequestType.SIP,
            "42" => TransferRequestType.ForceRedemption,
            //"43" => TransferRequestType.DividendPayout,
            //"44" => TransferRequestType.ForceIncrease,
            //"45" => TransferRequestType.ForceDecrease,
            "49" => TransferRequestType.Clear,
            "50" => TransferRequestType.Clear,
            //"59" => TransferRequestType.SIPStart,
            //"60" => TransferRequestType.SIPCancel,
            //"61" => TransferRequestType.SIPUpdate,
            //"63" => TransferRequestType.PeriodicRedemption,
            //"97" => TransferRequestType.QuickTransferIn,
            //"98" => TransferRequestType.QuickTransfer,
            _ => TransferRequestType.UNK
        };

        if (t == TransferRequestType.UNK)
            JsonBase.ReportSpecialType(new(0, CSC._Identifier, nameof(TransferRequest), OriginalNo, requestTypeStr));

        return t;
    }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。