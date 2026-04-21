using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 产品客户类型
/// </summary>
public enum ProductCustomerType
{
    /// <summary>
    /// 封闭式公募基金产品
    /// </summary>
    [Description("封闭式公募基金产品")]
    ClosedEndMutualFund,

    /// <summary>
    /// 银行理财产品
    /// </summary>
    [Description("银行理财产品")]
    BankWealthManagementProducts,

    /// <summary>
    /// 信托计划
    /// </summary>
    [Description("信托计划")]
    TrustPlans,

    /// <summary>
    /// 基金公司专户
    /// </summary>
    [Description("基金公司专户")]
    FundCompanySpecialAccounts,

    /// <summary>
    /// 基金子公司产品
    /// </summary>
    [Description("基金子公司产品")]
    FundSubsidiaryProducts,

    /// <summary>
    /// 保险产品
    /// </summary>
    [Description("保险产品")]
    InsuranceProducts,

    /// <summary>
    /// 保险公司及其子公司的资产管理计划
    /// </summary>
    [Description("保险公司及其子公司的资产管理计划")]
    InsuranceCompanyAssetManagementPlans,

    /// <summary>
    /// 证券公司集合理财产品（含证券公司大集合）
    /// </summary>
    [Description("证券公司集合理财产品（含证券公司大集合）")]
    SecuritiesCompanyCollectiveWealthManagementProducts,

    /// <summary>
    /// 证券公司及其子公司专项资管计划
    /// </summary>
    [Description("证券公司及其子公司专项资管计划")]
    SecuritiesCompanySpecialAssetManagementPlans,

    /// <summary>
    /// 证券公司及其子公司单一资管计划
    /// </summary>
    [Description("证券公司及其子公司单一资管计划")]
    SecuritiesCompanySingleAssetManagementPlans,

    /// <summary>
    /// 期货公司及其子公司的资产管理计划
    /// </summary>
    [Description("期货公司及其子公司的资产管理计划")]
    FuturesCompanyAssetManagementPlans,

    /// <summary>
    /// 私募投资基金
    /// </summary>
    [Description("私募投资基金")]
    PrivateEquityFunds,

    /// <summary>
    /// 政府引导基金
    /// </summary>
    [Description("政府引导基金")]
    GovernmentGuidanceFunds,

    /// <summary>
    /// 全国社保基金
    /// </summary>
    [Description("全国社保基金")]
    NationalSocialSecurityFund,

    /// <summary>
    /// 地方社保基金
    /// </summary>
    [Description("地方社保基金")]
    LocalSocialSecurityFund,

    /// <summary>
    /// 基本养老保险
    /// </summary>
    [Description("基本养老保险")]
    BasicOldAgeInsurance,

    /// <summary>
    /// 养老金产品
    /// </summary>
    [Description("养老金产品")]
    PensionProducts,

    /// <summary>
    /// 境外资金（QFII）
    /// </summary>
    [Description("境外资金（QFII）")]
    QFII,

    /// <summary>
    /// 境外资金（RQFII）
    /// </summary>
    [Description("境外资金（RQFII）")]
    RQFII,

    /// <summary>
    /// 其它境外资金
    /// </summary>
    [Description("其它境外资金")]
    OtherOverseasFunds,

    /// <summary>
    /// 社会公益基金（慈善基金、捐赠基金等）
    /// </summary>
    [Description("社会公益基金（慈善基金、捐赠基金等）")]
    PublicWelfareFunds,

    /// <summary>
    /// 企业年金及职业年金
    /// </summary>
    [Description("企业年金及职业年金")]
    CorporateAnnuityAndOccupationalAnnuity,

    /// <summary>
    /// 其他产品
    /// </summary>
    [Description("其他产品")]
    OtherProducts,

    /// <summary>
    /// 开放式公募基金产品
    /// </summary>
    [Description("开放式公募基金产品")]
    OpenEndMutualFund
}