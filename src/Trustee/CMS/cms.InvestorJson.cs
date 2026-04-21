using FMO.Models;
using System.Text.Json.Serialization;


namespace FMO.Trustee;



#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class InvestorJson : JsonBase
{

    /// <summary>
    /// 客户名称，最大长度200
    /// </summary>
    [JsonPropertyName("custName")]
    public string CustName { get; set; }

    /// <summary>
    /// 客户类型，最大长度30
    /// </summary>
    [JsonPropertyName("custType")]
    public string CustType { get; set; }

    /// <summary>
    /// 证件类型，最大长度50
    /// </summary>
    [JsonPropertyName("certificateType")]
    public string CertificateType { get; set; }

    /// <summary>
    /// 证件号码，最大长度30
    /// </summary>
    [JsonPropertyName("certificateNo")]
    public string CertificateNo { get; set; }

    /// <summary>
    /// 基金账号，最大长度20
    /// </summary>
    [JsonPropertyName("taAccountId")]
    public string TaAccountId { get; set; }

    /// <summary>
    /// 交易账号，最大长度30
    /// </summary>
    [JsonPropertyName("transactionAccountId")]
    public string TransactionAccountId { get; set; }

    /// <summary>
    /// 银行账户名称，最大长度300
    /// </summary>
    [JsonPropertyName("acctName")]
    public string AcctName { get; set; }

    /// <summary>
    /// 银行账号，最大长度20
    /// </summary>
    [JsonPropertyName("acctNo")]
    public string AcctNo { get; set; }

    /// <summary>
    /// 开户行名称，最大长度50
    /// </summary>
    [JsonPropertyName("clearingAgency")]
    public string ClearingAgency { get; set; }

    /// <summary>
    /// 开户行省份，最大长度50
    /// </summary>
    [JsonPropertyName("provinces")]
    public string Provinces { get; set; }

    /// <summary>
    /// 开户行城市，最大长度50
    /// </summary>
    [JsonPropertyName("city")]
    public string City { get; set; }

    /// <summary>
    /// 基金账号开户日期，格式：yyyymmdd
    /// </summary>
    [JsonPropertyName("openDate")]
    public string OpenDate { get; set; }

    /// <summary>
    /// 客户所属销售渠道，最大长度50
    /// </summary>
    [JsonPropertyName("distributorName")]
    public string DistributorName { get; set; }

    /// <summary>
    /// 销售渠道代码，最大长度3
    /// </summary>
    [JsonPropertyName("distributorCode")]
    public string DistributorCode { get; set; }

    /// <summary>
    /// 是否专业投资机构，最大长度10
    /// </summary>
    [JsonPropertyName("individualOrInstitution")]
    public string IndividualOrInstitution { get; set; }


    public Investor ToObject()
    {
        IDType iDType = ParseIdType(CertificateType);
        EntityType entityType = ParseCustomerType(CustType);

        if (iDType == IDType.Unknown)
            ReportJsonUnexpected(CMS._Identifier, nameof(CMS.QueryInvestors), $"{CustName}的证件类型[{CertificateType}]无法识别");
        if (entityType == EntityType.Unk)
            ReportJsonUnexpected(CMS._Identifier, nameof(CMS.QueryInvestors), $"{CustName}的实体类型[{entityType}]无法识别");

        return new Investor
        {
            Name = CustName,
            Identity = new Identity { Id = CertificateNo, Type = iDType },
            EntityType = entityType,

        };
    }

    private EntityType ParseCustomerType(string custType)
    {
        return custType switch
        {
            "个人" => EntityType.Natural,
            "机构" => EntityType.Institution,
            "产品" => EntityType.Product,
            _ => EntityType.Unk
        };
    }

    private IDType ParseIdType(string certificateType)
    {
        return certificateType switch
        {
            "0" or "未知证件类型" or "未知" => IDType.Unknown,
            "1" or "身份证" or "居民身份证" => IDType.IdentityCard,
            "2" or "社保卡" => IDType.Unknown, // 可根据实际情况映射
            "3" or "中国护照" => IDType.PassportChina,
            "4" or "军官证" => IDType.OfficerID,
            "5" or "士兵证" => IDType.SoldierID,
            "6" or "港澳居民来往内地通行证" => IDType.HongKongMacauPass,
            "7" or "户口本" => IDType.HouseholdRegister,
            "8" or "外国护照" => IDType.PassportForeign,
            "9" or "其他" or "其他证件" => IDType.Other,
            "10" or "文职证" => IDType.CivilianID,
            "11" or "警官证" => IDType.PoliceID,
            "12" or "台胞证" => IDType.TaiwanCompatriotsID,
            "13" or "外国人永久居留身份证" => IDType.ForeignPermanentResidentID,
            "20" => IDType.Unknown,
            "21" or "备案证明" => IDType.ProductFilingCode, // 可根据实际情况映射
            "22" or "组织机构代码" or "组织机构代码证" => IDType.OrganizationCode,
            "23" or "社会统一信用代码" or "统一社会信用代码" => IDType.UnifiedSocialCreditCode,
            "24" or "工商注册号" or "注册号" => IDType.RegistrationNumber,
            "25" or "营业执照" or "营业执照号" => IDType.BusinessLicenseNumber,
            "26" or "行政机关" => IDType.Other, // 可根据实际情况映射
            "27" or "社会团体" => IDType.Other, // 可根据实际情况映射
            "28" or "军队" => IDType.Other, // 可根据实际情况映射
            "29" or "武警" => IDType.Other, // 可根据实际情况映射
            "30" or "下属机构（具有主管单位批文号）" => IDType.Other, // 可根据实际情况映射
            "31" or "基金会" => IDType.Other, // 可根据实际情况映射
            "32" or "登记证书" => IDType.Other, // 可根据实际情况映射
            "33" or "批文" => IDType.Other, // 可根据实际情况映射
            "34" or "其它" => IDType.Other,
            "40" => IDType.Other, // 可根据实际情况映射
            "41" => IDType.BusinessLicenseNumber,
            "42" => IDType.Other, // 可根据实际情况映射
            "43" => IDType.Other,
            "管理人登记编码" => IDType.ManagerRegistrationCode,
            "产品备案编码" => IDType.ProductFilingCode,
            "证券业务许可证" => IDType.SecuritiesBusinessLicense,
            "产品登记编码" => IDType.ProductRegistrationCode,
            "港澳台居民居住证" => IDType.ResidencePermitForHongKongMacaoAndTaiwanResidents,
            "信托登记系统产品编码" => IDType.TrustRegistrationSystemProductCode,
            _ => IDType.Unknown,
        };
    }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。