using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCITICS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class BankTransactionJson : JsonBase
{

    /// <summary>
    /// 交易发生时间，格式为 YYYY-MM-DD HH:MM:SS
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
    /// 账户类型，例如 "02-募集户"
    /// </summary>
    [JsonPropertyName("accoType")]
    public string AccoType { get; set; }

    /// <summary>
    /// 本方账号
    /// </summary>
    [JsonPropertyName("bankAcco")]
    public string BankAcco { get; set; }

    /// <summary>
    /// 本方账户名称
    /// </summary>
    [JsonPropertyName("accName")]
    public string AccName { get; set; }

    /// <summary>
    /// 本方开户行名称
    /// </summary>
    [JsonPropertyName("openBankName")]
    public string OpenBankName { get; set; }

    /// <summary>
    /// 本方银行编号，以人行大额付款码前三位为标准（见附录2）
    /// </summary>
    [JsonPropertyName("bankNo")]
    public string BankNo { get; set; }

    /// <summary>
    /// 对方账号
    /// </summary>
    [JsonPropertyName("othBankAcco")]
    public string OthBankAcco { get; set; }

    /// <summary>
    /// 对方账户名称
    /// </summary>
    [JsonPropertyName("othAccName")]
    public string OthAccName { get; set; }

    /// <summary>
    /// 对方开户行名称
    /// </summary>
    [JsonPropertyName("othOpenBankName")]
    public string OthOpenBankName { get; set; }

    /// <summary>
    /// 对方银行编号，以人行大额付款码前三位为标准（见附录2）
    /// </summary>
    [JsonPropertyName("othBankNo")]
    public string OthBankNo { get; set; }

    /// <summary>
    /// 币种
    /// </summary>
    [JsonPropertyName("curType")]
    public string CurType { get; set; }

    /// <summary>
    /// 收付方向，1表示收款，2表示付款
    /// </summary>
    [JsonPropertyName("directFlag")]
    public string DirectFlag { get; set; }

    /// <summary>
    /// 发生金额
    /// </summary>
    [JsonPropertyName("occurAmt")]
    public string OccurAmt { get; set; }

    /// <summary>
    /// 账户余额
    /// </summary>
    [JsonPropertyName("acctBal")]
    public string AcctBal { get; set; }

    /// <summary>
    /// 银行流水号
    /// </summary>
    [JsonPropertyName("bankJour")]
    public string BankJour { get; set; }

    /// <summary>
    /// 银行返回代码
    /// </summary>
    [JsonPropertyName("bankRetCode")]
    public string BankRetCode { get; set; }

    /// <summary>
    /// 银行摘要
    /// </summary>
    [JsonPropertyName("bankNote")]
    public string BankNote { get; set; }



    public RaisingBankTransaction ToObject()
    {
        return new RaisingBankTransaction
        {
            Id = $"{BankAcco}|{BankJour}",
            Time = DateTime.ParseExact(OccurTime, "yyyy-MM-dd HH:mm:ss", null),
            FundCode = FundCode,
            AccountBank = OpenBankName,
            AccountName = AccName,
            AccountNo = BankAcco,
            CounterBank = OthOpenBankName,
            CounterName = OthAccName,
            CounterNo = OthBankAcco,
            Amount = ParseDecimal(OccurAmt),
            Balance = ParseDecimal(AcctBal),
            Direction = DirectFlag == "付款" ? TransctionDirection.Pay : TransctionDirection.Receive,
            Remark = BankNote,
            Serial = BankJour,
        };
    }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。