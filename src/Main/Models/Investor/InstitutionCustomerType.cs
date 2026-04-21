using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 机构客户类型
/// </summary>
public enum InstitutionCustomerType
{
    /// <summary>
    /// 私募基金管理人
    /// </summary>
    [Description("私募基金管理人")]
    PrivateFundManager,

    /// <summary>
    /// 期货公司
    /// </summary>
    [Description("期货公司")]
    FuturesCompany,

    /// <summary>
    /// 基金管理公司子公司
    /// </summary>
    [Description("基金管理公司子公司")]
    FundManagementSubsidiary,

    /// <summary>
    /// 证券公司子公司
    /// </summary>
    [Description("证券公司子公司")]
    SecuritiesSubsidiary,

    /// <summary>
    /// 期货公司子公司
    /// </summary>
    [Description("期货公司子公司")]
    FuturesSubsidiary,

    /// <summary>
    /// 财务公司
    /// </summary>
    [Description("财务公司")]
    FinanceCompany,

    /// <summary>
    /// 其他境内金融机构
    /// </summary>
    [Description("其他境内金融机构")]
    OtherDomesticFinancialInstitution,

    /// <summary>
    /// 机关法人
    /// </summary>
    [Description("机关法人")]
    GovernmentAgency,

    /// <summary>
    /// 事业单位法人
    /// </summary>
    [Description("事业单位法人")]
    PublicInstitution,

    /// <summary>
    /// 社会团体法人
    /// </summary>
    [Description("社会团体法人")]
    SocialOrganization,

    /// <summary>
    /// 非金融机构企业法人
    /// </summary>
    [Description("非金融机构企业法人")]
    NonFinancialCorporateEntity,

    /// <summary>
    /// 非金融类非法人机构
    /// </summary>
    [Description("非金融类非法人机构")]
    NonFinancialNonCorporateEntity,

    /// <summary>
    /// 境外代理人
    /// </summary>
    [Description("境外代理人")]
    OverseasAgent,

    /// <summary>
    /// 境外金融机构
    /// </summary>
    [Description("境外金融机构")]
    OverseasFinancialInstitution,

    /// <summary>
    /// 外国战略投资者
    /// </summary>
    [Description("外国战略投资者")]
    ForeignStrategicInvestor,

    /// <summary>
    /// 保险公司
    /// </summary>
    [Description("保险公司")]
    InsuranceCompany,

    /// <summary>
    /// 境外非金融机构
    /// </summary>
    [Description("境外非金融机构")]
    OverseasNonFinancialInstitution,

    /// <summary>
    /// 基金管理公司
    /// </summary>
    [Description("基金管理公司")]
    FundManagementCompany,

    /// <summary>
    /// 银行
    /// </summary>
    [Description("银行")]
    Bank,

    /// <summary>
    /// 银行子公司
    /// </summary>
    [Description("银行子公司")]
    BankSubsidiary,

    /// <summary>
    /// 保险子公司
    /// </summary>
    [Description("保险子公司")]
    InsuranceSubsidiary,

    /// <summary>
    /// 信托公司
    /// </summary>
    [Description("信托公司")]
    TrustCompany,

    /// <summary>
    /// 证券公司
    /// </summary>
    [Description("证券公司")]
    SecuritiesCompany,

    /// <summary>
    /// 其它
    /// </summary>
    [Description("其它")]
    Other,


}
