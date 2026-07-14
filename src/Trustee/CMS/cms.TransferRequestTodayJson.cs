using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class TransferRequestTodayJson : JsonBase
{
    public override string? JsonId => $"{CMS._Identifier}.{AppSerialNo}";

    /// <summary>
    /// 客户名称
    /// </summary>
    [JsonPropertyName("custName")]
    public string CustName { get; set; }

    /// <summary>
    /// 客户类型
    /// </summary>
    [JsonPropertyName("custType")]
    public string CustType { get; set; }

    /// <summary>
    /// 证件类型
    /// </summary>
    [JsonPropertyName("certificateType")]
    public string CertificateType { get; set; }

    /// <summary>
    /// 证件号码
    /// </summary>
    [JsonPropertyName("certificateNo")]
    public string CertificateNo { get; set; }

    /// <summary>
    /// 基金账号
    /// </summary>
    [JsonPropertyName("taAccountId")]
    public string TaAccountId { get; set; }

    /// <summary>
    /// 交易账号
    /// </summary>
    [JsonPropertyName("transactionAccountId")]
    public string TransactionAccountId { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 业务类型
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
    /// 销售渠道代码
    /// </summary>
    [JsonPropertyName("distributorCode")]
    public string DistributorCode { get; set; }

    /// <summary>
    /// 销售渠道名称
    /// </summary>
    [JsonPropertyName("distributorName")]
    public string DistributorName { get; set; }

    /// <summary>
    /// 申请单号
    /// </summary>
    [JsonPropertyName("appSerialNo")]
    public string AppSerialNo { get; set; }

    /// <summary>
    /// 预留字段
    /// </summary>
    [JsonPropertyName("reserveField1")]
    public string ReserveField1 { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [JsonPropertyName("remark1")]
    public string Remark1 { get; set; }

    /// <summary>
    /// 预约申购日期，格式：yyyymmdd
    /// </summary>
    [JsonPropertyName("futureBuyDate")]
    public string FutureBuyDate { get; set; }

    /// <summary>
    /// 预约赎回日期，格式：yyyymmdd
    /// </summary>
    [JsonPropertyName("redemptionDateInAdvance")]
    public string RedemptionDateInAdvance { get; set; }

    /// <summary>
    /// 销售机构类型，1:直销 0:代销
    /// </summary>
    [JsonPropertyName("distributorType")]
    public string DistributorType { get; set; }

    /// <summary>
    /// 银行卡号
    /// </summary>
    [JsonPropertyName("bankAccount")]
    public string BankAccount { get; set; }

    public TransferRequest ToObject()
    {
        var transferRequestType = TransferRequestJson.TranslateRequest(BusinessCode);

        var r = new TransferRequest
        {
            InvestorIdentity = CertificateNo,
            InvestorName = CustName,
            FundName = FundName,
            FundCode = FundCode,
            ShareCode = FundCode,
            RequestDate = DateOnly.ParseExact(TransactionDate, "yyyyMMdd"),
            RequestType = transferRequestType,
            RequestAmount = ParseDecimal(ApplicationAmount),
            RequestShare = ParseDecimal(ApplicationVol),
            Agency = DistributorName,
            FeeDiscount = ParseDecimal(DiscountRateOfCommission),
            ExternalId = $"{CMS._Identifier}.{AppSerialNo}",
            Source = "api"
        };

        if (r.RequestType == TransferRequestType.UNK)
            JsonBase.ReportSpecialType(new(0, CMS._Identifier, nameof(TransferRequest), AppSerialNo, BusinessCode));

        return r;
    }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
