using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCITICS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class CustodialAccountJson : JsonBase
{
    [JsonPropertyName("pdCode")]
    public string ProductCode { get; set; }

    [JsonPropertyName("pdName")]
    public string ProductName { get; set; }

    [JsonPropertyName("accName")]
    public string AccountName { get; set; }

    [JsonPropertyName("account")]
    public string Account { get; set; }

    [JsonPropertyName("bankName")]
    public string BankName { get; set; }

    [JsonPropertyName("lrgAmtNo")]
    public string LargePaymentNumber { get; set; }

    [JsonPropertyName("recentBalance")]
    public string RecentBalance { get; set; }

    [JsonPropertyName("recentBalanceQueryTime")]
    public string BalanceQueryTime { get; set; }

    /// <summary>
    /// 1：正常 2：销户
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }


    public FundBankAccount ToObject()
    {
        return new FundBankAccount
        {
            Name = AccountName,
            BankOfDeposit = BankName,
            FundCode = ProductCode,
            LargePayNo = LargePaymentNumber,
            Number = Account,
            IsCanceled = Status == 2,
        };
    }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。