using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCSC;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

public class RaisingBankTransactionJson : JsonBase
{
    /// <summary>
    /// 交易日期，格式：YYYY-MM-DD
    /// </summary>
    [JsonPropertyName("tradeDate")]

    public string TradeDate { get; set; }

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
    /// 交易时间，格式：HHMMSS
    /// </summary>
    [JsonPropertyName("tradeTime")]

    public string TradeTime { get; set; }

    /// <summary>
    /// 交易金额，格式：小数点后两位
    /// </summary>
    [JsonPropertyName("amt")]

    public string Amount { get; set; }

    /// <summary>
    /// 借贷标识：1-借，2-贷
    /// </summary>
    [JsonPropertyName("transferInOut")]
    public string TransferType { get; set; }

    /// <summary>
    /// 账户余额，格式：小数点后两位
    /// </summary>
    [JsonPropertyName("balance")]

    public string Balance { get; set; }

    /// <summary>
    /// 对方账号
    /// </summary>
    [JsonPropertyName("optBankAcco")]

    public string CounterpartyAccount { get; set; }

    /// <summary>
    /// 对方户名
    /// </summary>
    [JsonPropertyName("optAccName")]

    public string CounterpartyName { get; set; }

    /// <summary>
    /// 对方开户行名称
    /// </summary>
    [JsonPropertyName("optOpenBankName")]

    public string CounterpartyBank { get; set; }

    /// <summary>
    /// 本方账号
    /// </summary>
    [JsonPropertyName("bankAcco")]
    public string OurAccount { get; set; }

    /// <summary>
    /// 交易摘要
    /// </summary>
    [JsonPropertyName("digest")]

    public string Summary { get; set; }

    /// <summary>
    /// 表记录ID
    /// </summary>
    [JsonPropertyName("id")]
    public string RecordId { get; set; }

    /// <summary>
    /// 交易流水号
    /// </summary>
    [JsonPropertyName("serialNo")]

    public string TransactionNo { get; set; }

    /// <summary>
    /// 流水详细状态
    /// 0-无关联订单，1-已下单有余款，2-已下单或已退款，3-退款中，5-已下单多余款项退款中，空-其他状态
    /// </summary>
    [JsonPropertyName("detailStatus")]

    public string Status { get; set; }


    public RaisingBankTransaction ToObject()
    {
        return new RaisingBankTransaction
        {
            Id = $"{OurAccount}|{TransactionNo}",
            FundCode = FundCode,
            Time = DateTime.ParseExact(TradeDate + TradeTime, "yyyyMMddHHmmss", null),
            Amount = ParseDecimal(Amount),
            AccountNo = OurAccount,
            // 此属性缺失
            AccountBank = "unset",
            AccountName = $"{FundName}募集户",//"unset",
            Direction = TransferType == "1" ? TransctionDirection.Pay : TransctionDirection.Receive,
            CounterBank = CounterpartyBank,
            CounterName = CounterpartyName,
            CounterNo = CounterpartyAccount,
            Balance = ParseDecimal(Balance),
            Serial = TransactionNo,
            Remark = Summary,
        };
    }
}



#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。