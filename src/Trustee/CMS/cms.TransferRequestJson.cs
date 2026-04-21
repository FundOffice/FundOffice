using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class TransferRequestJson : JsonBase
{
    public override string? JsonId => $"{CMS._Identifier}.{Remark1}";

    /// <summary>
    /// 客户名称（最大长度：200）
    /// </summary>
    [JsonPropertyName("custName")]
    public string InvestorName { get; set; }

    /// <summary>
    /// 客户类型（最大长度：30）
    /// </summary>
    [JsonPropertyName("custType")]
    public string CustomerType { get; set; }

    /// <summary>
    /// 证件类型（最大长度：50）
    /// </summary>
    [JsonPropertyName("certificateType")]
    public string CertificateType { get; set; }

    /// <summary>
    /// 证件号码（最大长度：30）
    /// </summary>
    [JsonPropertyName("certificateNo")]
    public string CertificateNumber { get; set; }

    /// <summary>
    /// 基金账号（最大长度：20）
    /// </summary>
    [JsonPropertyName("taAccountId")]
    public string TaAccountId { get; set; }

    /// <summary>
    /// 交易账号（最大长度：30）
    /// </summary>
    [JsonPropertyName("transactionAccountId")]
    public string TransactionAccountId { get; set; }

    /// <summary>
    /// 产品名称（最大长度：300）
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 产品代码（最大长度：6）
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 业务类型（最大长度：6）
    /// </summary>
    [JsonPropertyName("businessCode")]
    public string BusinessCode { get; set; }

    /// <summary>
    /// 申请金额，保留两位小数
    /// </summary>
    [JsonPropertyName("applicationAmount")]
    public string ApplicationAmount { get; set; }

    /// <summary>
    /// 申请份额，保留两位小数
    /// </summary>
    [JsonPropertyName("applicationVol")]
    public string ApplicationVol { get; set; }

    /// <summary>
    /// 申请日期，格式：yyyymmdd
    /// </summary>
    [JsonPropertyName("transactionDate")]
    public string TransactionDate { get; set; }

    /// <summary>
    /// 手续费折扣率，保留两位小数
    /// </summary>
    [JsonPropertyName("discountRateOfCommission")]
    public string DiscountRateOfCommission { get; set; }

    /// <summary>
    /// 销售渠道代码（最大长度：3）
    /// </summary>
    [JsonPropertyName("distributorCode")]
    public string DistributorCode { get; set; }

    /// <summary>
    /// 销售渠道名称（最大长度：300）
    /// </summary>
    [JsonPropertyName("distributorName")]
    public string DistributorName { get; set; }

    /// <summary>
    /// 申请流水号（最大长度：500）
    /// </summary>
    [JsonPropertyName("remark1")]
    public string Remark1 { get; set; }

    /// <summary>
    /// 预留字段2（最大长度：500）
    /// </summary>
    [JsonPropertyName("remark2")]
    public string Remark2 { get; set; }

    /// <summary>
    /// 预约申购日期，格式：yyyymmdd（可为空）
    /// </summary>
    [JsonPropertyName("futureBuyDate")]
    public string FutureBuyDate { get; set; }

    /// <summary>
    /// 预约赎回日期，格式：yyyymmdd（可为空）
    /// </summary>
    [JsonPropertyName("redemptionDateInAdvance")]
    public string RedemptionDateInAdvance { get; set; }


    public TransferRequest ToObject()
    {
        TransferRequestType transferRequestType = TranslateRequest(BusinessCode);
        //if (transferRequestType == TransferRequestType.UNK && BusinessCode switch { "120" => false, _ => true })
         //   ReportJsonUnexpected(CMS._Identifier, nameof(CMS.QueryTransferRequests), $"TA[{Remark1}] {TransactionDate} 份额：{ApplicationVol} 金额：{ApplicationAmount} 的业务类型[{BusinessCode}]无法识别");


        var r = new TransferRequest
        {
            InvestorIdentity = CertificateNumber,
            InvestorName = InvestorName,
            FundName = FundName,
            FundCode = FundCode,
            RequestDate = DateOnly.ParseExact(TransactionDate, "yyyyMMdd"),
            RequestType = transferRequestType,
            RequestAmount = ParseDecimal(ApplicationAmount),
            RequestShare = ParseDecimal(ApplicationVol),
            Agency = DistributorName,
            FeeDiscount = ParseDecimal(DiscountRateOfCommission),
            ExternalId = $"{CMS._Identifier}.{Remark1}",
            Source = "api"
        };


        if (r.RequestType == TransferRequestType.UNK)
            JsonBase.ReportSpecialType(new(0, CMS._Identifier, nameof(TransferRequest), Remark1, BusinessCode));

        return r;
    }

    public static TransferRequestType TranslateRequest(string c)
    {
        return c switch
        {
            "020" => TransferRequestType.InitialOffer,
            "022" => TransferRequestType.Purchase,
            "024" => TransferRequestType.Redemption,
            "029" => TransferRequestType.BonusType,//"分红方式",
            "030" => TransferRequestType.Subscription,
            "042" => TransferRequestType.ForceRedemption,
            "043" => TransferRequestType.Distribution,     //"分红确认",
            "044" => TransferRequestType.Increase,     //"强行调增", 份额类型调整
            "045" => TransferRequestType.Decrease,     //"强行调减",
            "052" => TransferRequestType.Abort,
            _ => TransferRequestType.UNK
        };
    }
}




#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。