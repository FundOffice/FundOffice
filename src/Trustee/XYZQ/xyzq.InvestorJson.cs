using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;

internal class InvestorJson : JsonBase
{
    public override string? JsonId => FundAccount;

    /// <summary>
    /// 基金账号
    /// </summary>
    [JsonPropertyName("fundacco")]
    public string? FundAccount { get; set; }

    /// <summary>
    /// 客户名称
    /// </summary>
    [JsonPropertyName("custname")]
    public required string CustomerName { get; set; }

    /// <summary>
    /// 客户类型
    /// </summary>
    [JsonPropertyName("custtype")]
    public string? CustomerType { get; set; }

    /// <summary>
    /// 证件类型
    /// </summary>
    [JsonPropertyName("identitytype")]
    public string? IdentityType { get; set; }

    /// <summary>
    /// 证件号码
    /// </summary>
    [JsonPropertyName("identityno")]
    public required string IdentityNumber { get; set; }

    /// <summary>
    /// 开户日期
    /// </summary>
    [JsonPropertyName("opendate")]
    public string? OpenDate { get; set; }

    /// <summary>
    /// 产品编码
    /// </summary>
    [JsonPropertyName("fundcode")]
    public string? FundCode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("fundname")]
    public string? FundName { get; set; }

    /// <summary>
    /// 手机号码
    /// </summary>
    [JsonPropertyName("mobileno")]
    public string? MobileNumber { get; set; }

    /// <summary>
    /// 客户邮箱
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// 联系地址
    /// </summary>
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    /// <summary>
    /// 更新日期
    /// </summary>
    [JsonPropertyName("lastmodify")]
    public string? LastModifyDate { get; set; }

    /// <summary>
    /// 交易账号
    /// </summary>
    [JsonPropertyName("tradeacco")]
    public string? TradingAccount { get; set; }

    /// <summary>
    /// 持有份额
    /// </summary>
    [JsonPropertyName("realshares")]
    public decimal? RealShares { get; set; }

    /// <summary>
    /// 份额日期
    /// </summary>
    [JsonPropertyName("sharedate")]
    public string? ShareDate { get; set; }

    public Investor ToObject()
    {
        return new Investor
        {
            Name = CustomerName,
            Email = Email,
            Address = Address,
            Identity = new Identity
            {
                Id = IdentityNumber,
                Type = ParseIdtype(IdentityType)
            },
            //Type = Parse(CustomerType),
            Phone = MobileNumber,
        };
    }

    private IDType ParseIdtype(string? identityType)
    {
        switch (identityType)
        {
            // ===== 编码匹配 =====
            case "10":
            case "身份证":
                return IDType.IdentityCard;

            case "11":
            case "中国护照":
                return IDType.PassportChina;

            case "12":
            case "军官证":
                return IDType.OfficerID;

            case "13":
            case "士兵证":
                return IDType.SoldierID;

            case "14":
            case "港澳居民来往内地通行证":
                return IDType.HongKongMacauPass;

            case "15":
            case "户口本":
                return IDType.HouseholdRegister;

            case "16":
            case "外籍护照":
                return IDType.PassportForeign;

            case "17":
            case "其他":
                return IDType.Other;

            case "18":
            case "文职":
                return IDType.CivilianID;

            case "19":
            case "警官":
                return IDType.PoliceID;

            case "1A":
            case "台胞证":
                return IDType.TaiwanCompatriotsID;

            case "1B":
            case "外国人永久居留证":
                return IDType.ForeignPermanentResidentID;

            case "1C":
            case "港澳台居民居住证":
                return IDType.ResidencePermitForHongKongMacaoAndTaiwanResidents;

            // 组织机构相关
            case "00":
            case "0B":
            case "技术监督局代码":
            case "组织机构代码":
                return IDType.OrganizationCode;

            // 营业执照相关
            case "01":
            case "21":
            case "营业执照":
                return IDType.BusinessLicenseNumber;

            // 机构类型
            case "02":
            case "03":
            case "04":
            case "05":
            case "06":
            case "08":
            case "行政机关":
            case "社会团体":
            case "军队":
            case "武警":
            case "下属机构":
            case "其他机构":
                return IDType.Other;

            // 产品备案
            case "07":
            case "产品备案编码":
                return IDType.ProductFilingCode;

            // 登记证书
            case "09":
            case "29":
            case "登记证书":
                return IDType.RegistrationNumber;

            // 批文
            case "0A":
            case "2A":
            case "批文":
                return IDType.Approval;

            // 统一社会信用代码
            case "0I":
            case "统一社会信用代码":
                return IDType.UnifiedSocialCreditCode;

            // 其他
            case "28":
                return IDType.Other;

            default:
                return IDType.Unknown;
        }
    }

    //private AmacInvestorType Parse(string? type)
    //{

    //}

}
