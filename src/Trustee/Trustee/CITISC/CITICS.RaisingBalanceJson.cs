using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCITICS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class RaisingBalanceJson : JsonBase
{
    /// <summary>
    /// 发生时间（格式：YYYY-MM-DD HH:mm:ss）
    /// </summary>
    [JsonPropertyName("occurTime")]
    public string OccurTime { get; set; }

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
    /// 账户类型
    /// 02 - 募集户
    /// </summary>
    [JsonPropertyName("accoType")]
    public string AccountType { get; set; }

    /// <summary>
    /// 银行账号
    /// </summary>
    [JsonPropertyName("bankAcco")]
    public string BankAccountNo { get; set; }

    /// <summary>
    /// 银行账户名称
    /// </summary>
    [JsonPropertyName("accName")]
    public string BankAccountName { get; set; }

    /// <summary>
    /// 银行开户行名称
    /// </summary>
    [JsonPropertyName("openBankName")]
    public string OpenBankName { get; set; }

    /// <summary>
    /// 银行编号（以人行大额付款码前三位为标准）
    /// </summary>
    [JsonPropertyName("bankNo")]
    public string BankNumber { get; set; }

    /// <summary>
    /// 币种
    /// </summary>
    [JsonPropertyName("curType")]
    public string Currency { get; set; }

    /// <summary>
    /// 账户余额
    /// </summary>
    [JsonPropertyName("acctBal")]
    public string AccountBalance { get; set; }

    /// <summary>
    /// 银行返回代码
    /// </summary>
    [JsonPropertyName("bankRetCode")]
    public string BankReturnCode { get; set; }

    /// <summary>
    /// 银行摘要
    /// </summary>
    [JsonPropertyName("bankNote")]
    public string BankSummary { get; set; }

    /// <summary>
    /// CNAPS 大额支付号
    /// </summary>
    [JsonPropertyName("cnapsCode")]
    public string CNAPSCode { get; set; }


    public FundBankBalance ToObject()
    {
        return new FundBankBalance
        {
            FundCode = FundCode,
            FundName = FundName,
            AccountName = BankAccountName,
            AccountNo = BankNumber,
            Name = OpenBankName,
            Balance = ParseDecimal(AccountBalance),
            Currency = Currency,
            Time = DateTime.ParseExact(OccurTime, "yyyy-MM-dd HH:mm:ss", null),
        };
    }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。