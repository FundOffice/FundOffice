using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCSC;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

public class BankBalanceJson : JsonBase
{  /// <summary>
   /// 币种
   /// </summary>
    [JsonPropertyName("curType")]
    public string Currency { get; set; }

    /// <summary>
    /// 基金代码（最大长度32）
    /// </summary>
    [JsonPropertyName("fundCode")]
    public string FundCode { get; set; }

    /// <summary>
    /// 基金名称（最大长度250）
    /// </summary>
    [JsonPropertyName("fundName")]
    public string FundName { get; set; }

    /// <summary>
    /// 账户类型（最大长度2）
    /// </summary>
    [JsonPropertyName("accType")]
    public string AccountType { get; set; }

    /// <summary>
    /// 银行账号（最大长度32）
    /// </summary>
    [JsonPropertyName("bankAcco")]
    public string BankAccountNo { get; set; }

    /// <summary>
    /// 账户名称（最大长度64）
    /// </summary>
    [JsonPropertyName("accName")]
    public string AccountName { get; set; }

    /// <summary>
    /// 银行编号（最大长度6，通常为人行大额支付码前三位）
    /// </summary>
    [JsonPropertyName("bankNo")]
    public string BankNumber { get; set; }

    /// <summary>
    /// 开户行名称（最大长度250）
    /// </summary>
    [JsonPropertyName("openBankName")]
    public string OpenBankName { get; set; }

    /// <summary>
    /// 银行余额（最大长度20，保留两位小数）
    /// </summary>
    [JsonPropertyName("acctBal")]
    public string AccountBalance { get; set; }

    /// <summary>
    /// 托管户账户状态（仅托管户有效，募集户无意义）
    /// 参见 3.16 托管账户状态
    /// </summary>
    [JsonPropertyName("acctStatus")]
    public string CustodialStatus { get; set; }

    /// <summary>
    /// 募集户账户状态（仅募集户有效，托管户无意义）
    /// 参见 3.23 募集户账户状态
    /// </summary>
    [JsonPropertyName("raiseStatus")]
    public string RaiseStatus { get; set; }

    /// <summary>
    /// 最后更新时间（格式：YYYY-MM-dd HH:mm:ss）
    /// </summary>
    [JsonPropertyName("retTime")]
    public string LastUpdateTime { get; set; }



    public FundBankBalance ToObject()
    {
        return new FundBankBalance
        {
            AccountName = AccountName,
            AccountNo = BankAccountNo,
            Name = OpenBankName,
            FundName = FundName,
            FundCode = FundCode,
            Balance = string.IsNullOrWhiteSpace(AccountBalance) ? 0 : ParseDecimal(AccountBalance),
            Currency = ParseCurrency(Currency),
            Time = string.IsNullOrWhiteSpace(LastUpdateTime) ? default : DateTime.ParseExact(LastUpdateTime, "yyyy-MM-dd HH:mm:ss", null)
        };
    }

}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。