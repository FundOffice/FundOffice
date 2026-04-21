using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCMS;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class BankBalanceJson : JsonBase
{
    /// <summary>
    /// 币种
    /// </summary>
    [JsonPropertyName("bz")]
    public string Currency { get; set; }

    /// <summary>
    /// 说明
    /// </summary>
    [JsonPropertyName("clsm")]
    public string Description { get; set; }

    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("cpdm")]
    public string ProductCode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("cpmc")]
    public string ProductName { get; set; }

    /// <summary>
    /// 更新时间（格式建议：yyyyMMddHHmmss）
    /// </summary>
    [JsonPropertyName("gxsj")]
    public string UpdateTime { get; set; }

    /// <summary>
    /// 开户行
    /// </summary>
    [JsonPropertyName("khzh")]
    public string BankName { get; set; }

    /// <summary>
    /// 可用余额
    /// </summary>
    [JsonPropertyName("kyye")]
    public string AvailableBalance { get; set; }

    /// <summary>
    /// 账号
    /// </summary>
    [JsonPropertyName("zhh")]
    public string AccountNo { get; set; }

    /// <summary>
    /// 账号名称
    /// </summary>
    [JsonPropertyName("zhmc")]
    public string AccountName { get; set; }

    /// <summary>
    /// 账号余额
    /// </summary>
    [JsonPropertyName("zhye")]
    public string AccountBalance { get; set; }

    public FundBankBalance ToObject()
    {
        return new FundBankBalance
        {
            FundCode = ProductCode,
            FundName = ProductName,
            Currency = Currency,
            AccountName = AccountName,
            AccountNo = AccountNo,
            Name = BankName,
            Balance = ParseDecimal(AccountBalance),
            Time = DateTime.ParseExact(UpdateTime, "yyyy-MM-dd HH:mm:ss", null)
        };
    }
}



#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。