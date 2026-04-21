using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
internal class FundBankBalanceJson : JsonBase
{     /// <summary>
      /// 产品代码
      /// </summary>
    [JsonPropertyName("fundcode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fundname")]
    public string FundName { get; set; }

    /// <summary>
    /// 募集账号
    /// </summary>
    [JsonPropertyName("recipientacco")]
    public string RecipientAccount { get; set; }

    /// <summary>
    /// 开户行
    /// </summary>
    [JsonPropertyName("recipientbankname")]
    public string RecipientBankName { get; set; }

    /// <summary>
    /// 总金额
    /// </summary>
    [JsonPropertyName("amount")]
    public string TotalAmount { get; set; }

    /// <summary>
    /// 余额日期
    /// </summary>
    [JsonPropertyName("date")]
    public string BalanceDate { get; set; }

    /// <summary>
    /// 币种
    /// </summary>
    [JsonPropertyName("moneytype")]
    public string CurrencyType { get; set; }

    /// <summary>
    /// 募集户名称
    /// </summary>
    [JsonPropertyName("recipientacconame")]
    public string RecipientAccountName { get; set; }


    public FundBankBalance ToObject()
    {
        return new FundBankBalance
        {
            AccountName = RecipientAccountName,
            AccountNo = RecipientAccount,
            FundName = FundName,
            FundCode = FundCode,
            Name = RecipientAccountName,
            Currency = CurrencyType,
            Time = DateTime.ParseExact(BalanceDate, "yyyy-MM-dd", null),
            Balance = ParseDecimal(TotalAmount)
        };
    }

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
}