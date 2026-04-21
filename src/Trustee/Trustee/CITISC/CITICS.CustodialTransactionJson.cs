using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCITICS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class CustodialTransactionJson : JsonBase
{
    /// <summary>
    /// 银行代码
    /// </summary>
    [JsonPropertyName("YHDM")]
    public string BankCode { get; set; }

    /// <summary>
    /// 银行名称
    /// </summary>
    [JsonPropertyName("YHMC")]
    public string BankName { get; set; }

    /// <summary>
    /// 付方账号（CLJG为-1时填查询帐号）
    /// </summary>
    [JsonPropertyName("FKFZH")]
    public string PayerAccountNo { get; set; }

    /// <summary>
    /// 付方户名
    /// </summary>
    [JsonPropertyName("FKFHM")]
    public string PayerAccountName { get; set; }

    /// <summary>
    /// 收方账号（CLJG为-1时填查询帐号）
    /// </summary>
    [JsonPropertyName("SKFZH")]
    public string PayeeAccountNo { get; set; }

    /// <summary>
    /// 收方户名
    /// </summary>
    [JsonPropertyName("SKFHM")]
    public string PayeeAccountName { get; set; }

    /// <summary>
    /// 发生金额
    /// </summary>
    [JsonPropertyName("FSJE")]
    public decimal Amount { get; set; }

    /// <summary>
    /// 结算币种
    /// HKD: 港币, RMB: 人民币, USD: 美元
    /// </summary>
    [JsonPropertyName("JSBZ")]
    public string Currency { get; set; }

    /// <summary>
    /// 借贷标志：
    /// 借、贷、付款=借、扣款=借、收款=贷、扣款撤销、收款撤销
    /// </summary>
    [JsonPropertyName("JDBZ")]
    public string DebitCreditFlag { get; set; }

    /// <summary>
    /// 摘要名称
    /// </summary>
    [JsonPropertyName("ZYMC")]
    public string Summary { get; set; }

    /// <summary>
    /// 发生时间，格式 yyyy-MM-dd HH:mm:ss
    /// </summary>
    [JsonPropertyName("FSSJ")]
    public string OccurTime { get; set; }

    /// <summary>
    /// 返回信息
    /// </summary>
    [JsonPropertyName("FHXX")]
    public string ReturnInfo { get; set; }

    /// <summary>
    /// 备用字段
    /// </summary>
    [JsonPropertyName("BYZD")]
    public string ReservedField { get; set; }

    /// <summary>
    /// 账户余额
    /// </summary>
    [JsonPropertyName("ZHYE")]
    public decimal AccountBalance { get; set; }

    /// <summary>
    /// 可用余额
    /// </summary>
    [JsonPropertyName("KYYE")]
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    [JsonPropertyName("LSH")]
    public string SerialNumber { get; set; }

    /// <summary>
    /// 处理结果
    /// 0 - 成功
    /// -2 - 处理中
    /// 其他值代表失败
    /// </summary>
    [JsonPropertyName("CLJG")]
    public string ProcessResult { get; set; }

    /// <summary>
    /// 处理说明
    /// </summary>
    [JsonPropertyName("CLSM")]
    public string ProcessDescription { get; set; }


    public BankTransaction ToObject()
    {
        return new BankTransaction
        {
            AccountBank = BankName,
            AccountName = PayerAccountName,
            AccountNo = PayerAccountNo,
            CounterBank = "",
            CounterName = PayeeAccountName,
            CounterNo = PayeeAccountNo,
            Id = SerialNumber,
            Serial = SerialNumber,
            Amount = Amount,
            Balance = AccountBalance,
            Currency = Currency,
            Direction = DebitCreditFlag switch { string s when s.Contains("撤销") => TransctionDirection.Cancel, "借" or "借款" or "扣款" => TransctionDirection.Pay, _ => TransctionDirection.Receive },
            Remark = Summary,
            Time = DateTime.ParseExact(OccurTime, "yyyy-MM-dd HH:mm:ss", null)
        };
    }

}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。