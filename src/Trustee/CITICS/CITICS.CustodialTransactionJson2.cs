using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class CustodialTransactionJson2 : JsonBase
{
    /// <summary>
    /// 付方账号
    /// </summary>
    [JsonPropertyName("fpayerAcctCode")]
    public string PayerAccountCode { get; set; }

    /// <summary>
    /// 付方户名
    /// </summary>
    [JsonPropertyName("fpayerName")]
    public string PayerName { get; set; }

    /// <summary>
    /// 付方开户行
    /// </summary>
    [JsonPropertyName("fpayerBank")]
    public string PayerBank { get; set; }

    /// <summary>
    /// 收款方账号
    /// </summary>
    [JsonPropertyName("fpayeeAcctCode")]
    public string PayeeAccountCode { get; set; }

    /// <summary>
    /// 收款方户名
    /// </summary>
    [JsonPropertyName("fpayeeName")]
    public string PayeeName { get; set; }

    /// <summary>
    /// 收款方开户行
    /// </summary>
    [JsonPropertyName("fpayeeBank")]
    public string PayeeBank { get; set; }

    /// <summary>
    /// 发生日期（格式：yyyy-MM-dd）
    /// </summary>
    [JsonPropertyName("date")]
    public string OccurDate { get; set; }

    /// <summary>
    /// 收款方向代码：
    /// SFKFX001: 出款
    /// SFKFX002: 入款
    /// SFKFX003: 调拨
    /// </summary>
    [JsonPropertyName("fway")]
    public string DirectionCode { get; set; }

    /// <summary>
    /// 收款方向名称（SFKFX001：出款；SFKFX002：入款；SFKFX003：调拨）
    /// </summary>
    [JsonPropertyName("fwayName")]
    public string DirectionName { get; set; }

    /// <summary>
    /// 发生金额（字符串类型，保留两位小数）
    /// </summary>
    [JsonPropertyName("famount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [JsonPropertyName("fsummary")]
    public string Summary { get; set; }

    public BankTransaction ToObject()
    {
        var direction = DirectionCode switch { "SFKFX001" or "SFKFX003" => TransctionDirection.Pay,/* "SFKFX002"*/ _ => TransctionDirection.Receive };

        return new BankTransaction
        {
            AccountBank = direction == TransctionDirection.Pay ? PayerBank : PayeeBank,
            AccountName = direction == TransctionDirection.Pay ? PayerName : PayeeName,
            AccountNo = direction == TransctionDirection.Pay ? PayerAccountCode : PayeeAccountCode,
            CounterBank = direction != TransctionDirection.Pay ? PayerBank : PayeeBank,
            CounterName = direction != TransctionDirection.Pay ? PayerName : PayeeName,
            CounterNo = direction != TransctionDirection.Pay ? PayerAccountCode : PayeeAccountCode,
            Id = $"{PayeeAccountCode.GetHashCode()}|{PayerAccountCode.GetHashCode()}|{OccurDate}{Amount}",
            Serial = $"{PayeeAccountCode.GetHashCode()}|{PayerAccountCode.GetHashCode()}|{OccurDate}{Amount}",
            Amount = Amount,
            Direction = direction,
            Remark = Summary,
            Time = DateTime.ParseExact(OccurDate, "yyyy-MM-dd HH:mm:ss", null)
        };
    }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。