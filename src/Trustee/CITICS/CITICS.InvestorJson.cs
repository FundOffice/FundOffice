using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class InvestorJson : JsonBase
{
    [JsonPropertyName("custName")]
    public string CustName { get; set; } // 投资者名称

    [JsonPropertyName("fundAcco")]
    public string FundAcco { get; set; } // 基金账号

    [JsonPropertyName("tradeAcco")]
    public string TradeAcco { get; set; } // 交易账号

    [JsonPropertyName("custType")]
    public string CustType { get; set; } // 客户类型（参见附录4）

    [JsonPropertyName("certiType")]
    public string CertiType { get; set; } // 证件类型（参见附录4）

    [JsonPropertyName("certiNo")]
    public string CertiNo { get; set; } // 证件号

    [JsonPropertyName("bankNo")]
    public string? BankNo { get; set; } // 银行编号

    [JsonPropertyName("bankAccount")]
    public string? BankAccount { get; set; } // 银行账号

    [JsonPropertyName("bankOpenName")]
    public string? BankOpenName { get; set; } // 开户行名称

    [JsonPropertyName("bankAccountName")]
    public string? BankAccountName { get; set; } // 银行户名

    [JsonPropertyName("address")]
    public string? Address { get; set; } // 通讯地址

    [JsonPropertyName("tel")]
    public string? Tel { get; set; } // 联系电话

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; } // 邮编

    [JsonPropertyName("agencyNo")]
    public string? AgencyNo { get; set; } // 销售商代码，ZX6表示直销

    [JsonPropertyName("email")]
    public string? Email { get; set; } // 邮箱

    public Investor ToObject()
    {
        return new Investor
        {
            Name = CustName,
            Identity = new Identity { Id = CertiNo, Type = ParseIdType(CustType, CertiType) },
            Email = Email,
            Phone = Tel,
        };
    }



    public static IDType ParseIdType(string custType, string certiType)
    {
        if (string.IsNullOrEmpty(custType) || string.IsNullOrEmpty(certiType))
            return IDType.Unknown;

        // 处理客户类型：0=机构，1=个人，2=产品
        switch (custType)
        {
            case "0": // 机构
                return certiType.ToUpper() switch
                {
                    "0" => IDType.OrganizationCode, // 组织机构代码证
                    "1" => IDType.BusinessLicenseNumber,      // 营业执照
                    "2" => IDType.RegistrationNumber,         // 行政机关
                    "3" => IDType.OrganizationCode,           // 社会团体
                    "4" => IDType.Other,                     // 军队
                    "5" => IDType.Other,                     // 武警
                    "6" => IDType.Other,                     // 下属机构
                    "7" => IDType.Other,                     // 基金会
                    "8" => IDType.Other,                     // 其他机构
                    "9" => IDType.ProductFilingCode,         // 登记证书
                    "A" => IDType.ManagerRegistrationCode,   // 批文
                    _ => IDType.Unknown
                };

            case "1": // 个人
                return certiType.ToUpper() switch
                {
                    "0" => IDType.IdentityCard,                    // 身份证
                    "1" => IDType.PassportChina,                   // 中国护照
                    "2" => IDType.OfficerID,                       // 军官证
                    "3" => IDType.SoldierID,                       // 士兵证
                    "4" => IDType.HongKongMacauPass,               // 港澳居民来往内地通行证
                    "5" => IDType.HouseholdRegister,               // 户口本
                    "6" => IDType.PassportForeign,                 // 外籍护照
                    "7" => IDType.Other,                           // 其他
                    "8" => IDType.CivilianID,                      // 文职证
                    "9" => IDType.PoliceID,                        // 警官证
                    "A" => IDType.TaiwanCompatriotsID,             // 台胞证
                    "B" => IDType.ForeignPermanentResidentID,      // 外国人永久居留身份证
                    "C" => IDType.ResidencePermitForHongKongMacaoAndTaiwanResidents, // 港澳台居民居住证
                    _ => IDType.Unknown
                };

            case "2": // 产品
                return certiType.ToUpper() switch
                {
                    "1" => IDType.BusinessLicenseNumber, // 营业执照（直销接口不允许使用）
                    "8" => IDType.Other,                 // 其他
                    "9" => IDType.ProductFilingCode,     // 登记证书（直销接口不允许使用）
                    "A" => IDType.ManagerRegistrationCode, // 批文
                    _ => IDType.Unknown
                };

            default:
                return IDType.Unknown;
        }
    }


}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。