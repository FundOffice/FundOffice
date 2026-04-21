using System.ComponentModel;

namespace FMO.Models;




[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum AmacInvestorType
{
    [Description("请选择")]None,

    [Description("自然人（非员工跟投）")] NonEmployee,

    [Description("自然人（员工跟投）")] Employee, 

    [Description("本产品管理人跟投")] Manager,

    [Description("境内法人机构（公司等）")] LegalEntity,

    [Description("境内非法人机构（个人独资企业）")] IndividualProprietorship,

    [Description("境内非法人机构（一般合伙企业等）")] NonLegalEntity,

    [Description("境外资金（QFII、RQFII等）")] QFII,

    [Description("境外机构")] Foreign,

    [Description("财政直接出资")]
    DirectFinancialInvestment,

    //[Description("其它机构")] Other,


    [Description("产品")] Product,

    [Description("私募基金产品")]
    PrivateFundProduct,

    [Description("证券公司及其子公司资产管理计划")]
    SecuritiesCompanyAssetManagementPlan,

    [Description("基金公司及其子公司资产管理计划")]
    FundCompanyAssetManagementPlan,

    [Description("期货公司及其子公司资产管理计划")]
    FuturesCompanyAssetManagementPlan,

    [Description("信托计划")]
    TrustPlan,

    [Description("商业银行理财产品")]
    CommercialBankFinancialProduct,

    [Description("保险资产管理计划")]
    InsuranceAssetManagementPlan,

    [Description("慈善基金、捐赠基金等社会公益基金")]
    SocialWelfareFund,

    [Description("养老基金")]
    PensionFund,

    [Description("社会保障基金")]
    SocialSecurityFund,

    [Description("企业年金")]
    EnterpriseAnnuity,

    [Description("政府类引导基金")]
    GovernmentGuidanceFund,

}


[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum QualifiedInvestorType
{
    [Description("普通")] Normal,

    [Description("专业")] Professional,
}


/// <summary>
/// 硬编码 ToString 扩展方法（不反射、不读特性）
/// </summary>
public static class AmacInvestorTypeExtensions
{
    public static string ToAmacString(this AmacInvestorType type)
    {
        return type switch
        {
            AmacInvestorType.None => "未知",
            AmacInvestorType.NonEmployee => "自然人（非员工跟投）",
            AmacInvestorType.Employee => "自然人（员工跟投）",
            AmacInvestorType.Manager => "本产品管理人跟投",
            AmacInvestorType.LegalEntity => "境内法人机构（公司等）",
            AmacInvestorType.IndividualProprietorship => "境内非法人机构（个人独资企业）",
            AmacInvestorType.NonLegalEntity => "境内非法人机构（一般合伙企业等）",
            AmacInvestorType.QFII => "境外资金（QFII、RQFII等）",
            AmacInvestorType.Foreign => "境外机构",
            AmacInvestorType.DirectFinancialInvestment => "财政直接出资",
            AmacInvestorType.Product => "产品",
            AmacInvestorType.PrivateFundProduct => "私募基金产品",
            AmacInvestorType.SecuritiesCompanyAssetManagementPlan => "证券公司及其子公司资产管理计划",
            AmacInvestorType.FundCompanyAssetManagementPlan => "基金公司及其子公司资产管理计划",
            AmacInvestorType.FuturesCompanyAssetManagementPlan => "期货公司及其子公司资产管理计划",
            AmacInvestorType.TrustPlan => "信托计划",
            AmacInvestorType.CommercialBankFinancialProduct => "商业银行理财产品",
            AmacInvestorType.InsuranceAssetManagementPlan => "保险资产管理计划",
            AmacInvestorType.SocialWelfareFund => "慈善基金、捐赠基金等社会公益基金",
            AmacInvestorType.PensionFund => "养老基金",
            AmacInvestorType.SocialSecurityFund => "社会保障基金",
            AmacInvestorType.EnterpriseAnnuity => "企业年金",
            AmacInvestorType.GovernmentGuidanceFund => "政府类引导基金",
            _ => type.ToString() // 兜底
        };
    }
}