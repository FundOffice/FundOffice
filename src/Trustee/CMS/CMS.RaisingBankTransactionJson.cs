using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class RaisingBankTransactionJson :JsonBase
{
    /// <summary>
    /// 本方账户号
    /// </summary>
    [JsonPropertyName("bfzhh")]
    public string OurAccountNumber { get; set; }

    /// <summary>
    /// 本方账户名
    /// </summary>
    [JsonPropertyName("bfzhmc")]
    public string OurAccountName { get; set; }

    /// <summary>
    /// 币种
    /// </summary>
    [JsonPropertyName("bz")]
    public string Currency { get; set; }

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
    /// 对手方开户行名称
    /// </summary>
    [JsonPropertyName("dsfkhhmc")]
    public string CounterpartyBankName { get; set; }

    /// <summary>
    /// 对方账户号
    /// </summary>
    [JsonPropertyName("dsfzhh")]
    public string CounterpartyAccountNumber { get; set; }

    /// <summary>
    /// 对方账户名
    /// </summary>
    [JsonPropertyName("dsfzhmc")]
    public string CounterpartyAccountName { get; set; }

    /// <summary>
    /// 交易金额
    /// </summary>
    [JsonPropertyName("jyje")]
    public string TransactionAmount { get; set; }

    /// <summary>
    /// 交易日期
    /// </summary>
    [JsonPropertyName("jyrq")]
    public string TransactionDate { get; set; }

    /// <summary>
    /// 交易时间
    /// </summary>
    [JsonPropertyName("jysj")]
    public string TransactionTime { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    [JsonPropertyName("lsh")]
    public string TransactionId { get; set; }

    /// <summary>
    /// 募集户开户行
    /// </summary>
    [JsonPropertyName("mjhyh")]
    public string CollectionBank { get; set; }

    /// <summary>
    /// 收付方向（收、付）
    /// </summary>
    [JsonPropertyName("sffx")]
    public string TransactionType { get; set; }

    /// <summary>
    /// 银行摘要
    /// </summary>
    [JsonPropertyName("yhbz")]
    public string BankMemo { get; set; }

    internal RaisingBankTransaction ToObject()
    {
        // 判断 OurAccountName 中是否有基金名 
        if (OurAccountName == "招商证券股份有限公司基金运营外包服务募集专户")
            OurAccountName = $"{ProductName}募集专户";

        return new RaisingBankTransaction
        {
            Id = TransactionId,
            FundCode = ProductCode,
            AccountNo = OurAccountNumber,
            AccountBank = CollectionBank,
            AccountName = OurAccountName,
            Serial = TransactionId,
            CounterBank = CounterpartyBankName,
            CounterName = CounterpartyAccountName,
            CounterNo = CounterpartyAccountNumber,
            Amount = ParseDecimal(TransactionAmount),
            Direction = TransactionType == "付" ? TransctionDirection.Pay : TransctionDirection.Receive,
            Remark = BankMemo,
            Time = DateTime.ParseExact(TransactionDate + TransactionTime, "yyyyMMddHH:mm:ss", null)
        };
    }
}



#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。