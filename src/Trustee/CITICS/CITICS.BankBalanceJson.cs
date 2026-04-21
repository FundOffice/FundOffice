using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class BankBalanceJson : JsonBase
{
    /// <summary>
    /// 账号
    /// </summary>
    [JsonPropertyName("YHZH")]
    public string AccountNumber { get; set; }

    /// <summary>
    /// 账户余额
    /// </summary>
    [JsonPropertyName("ZHYE")]
    public string AccountBalance { get; set; }

    /// <summary>
    /// 账户名称
    /// </summary>
    [JsonPropertyName("KHHM")]
    public string AccountHolderName { get; set; }

    /// <summary>
    /// 货币种类：
    /// HKD-港币；
    /// RMB-人民币；
    /// USD-美元
    /// </summary>
    [JsonPropertyName("JSBZ")]
    public string CurrencyType { get; set; }

    /// <summary>
    /// 余额查询时间，格式：YYYY-MM-DD HH:MM:SS
    /// </summary>
    [JsonPropertyName("CXSJ")]
    public string QueryTime { get; set; }

    /// <summary>
    /// 账户可用余额
    /// </summary>
    [JsonPropertyName("ZHKYYE")]
    public string AvailableBalance { get; set; }

    /// <summary>
    /// 处理结果：
    /// 0-成功；
    /// -2-处理中；
    /// 其他值代表失败
    /// </summary>
    [JsonPropertyName("CLJG")]
    public string ProcessingResult { get; set; }

    /// <summary>
    /// 处理说明
    /// </summary>
    [JsonPropertyName("CLSM")]
    public string ProcessingDescription { get; set; }

    /// <summary>
    /// 开户行
    /// </summary>
    [JsonPropertyName("KHYH")]
    public string OpeningBank { get; set; }

    public BankBalance ToObject()
    {
        return new BankBalance
        {
            AccountName = AccountHolderName,
            AccountNo = AccountNumber,
            Name = OpeningBank,
            Balance = ParseDecimal(AccountBalance),
            Currency = CurrencyType,
            Time = DateTime.ParseExact(QueryTime, "yyyy-MM-dd HH:mm:ss", null)
        };
    }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。