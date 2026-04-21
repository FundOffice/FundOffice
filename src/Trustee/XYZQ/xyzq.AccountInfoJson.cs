using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
internal class AccountInfoJson : JsonBase
{
    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fundname")]
    public string FundName { get; set; }

    /// <summary>
    /// 账号类型
    /// </summary>
    [JsonPropertyName("accounttype")]
    public string AccountType { get; set; }

    /// <summary>
    /// 账号
    /// </summary>
    [JsonPropertyName("account")]
    public string Account { get; set; }

    /// <summary>
    /// 账户名称
    /// </summary>
    [JsonPropertyName("accountname")]
    public string AccountName { get; set; }

    /// <summary>
    /// 开户行/关联银行
    /// </summary>
    [JsonPropertyName("bankname")]
    public string BankName { get; set; }

    /// <summary>
    /// 券商名称（如有）
    /// </summary>
    [JsonPropertyName("brokername")]
    public string BrokerName { get; set; }

    /// <summary>
    /// 账户状态
    /// </summary>
    [JsonPropertyName("accountstatus")]
    public string AccountStatus { get; set; }


    public BankAccount ToBank()
    {
        return new BankAccount
        {
            Number = Account,
            Name = AccountName,
            BankOfDeposit = BankName
        };
    }

}
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。