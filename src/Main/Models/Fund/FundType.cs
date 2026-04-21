using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum FundType
{

    Unk,

    [Description("私募证券投资基金")]
    PrivateSecuritiesInvestmentFund,

    [Description("私募证券投资基金FOF基金")]
    PrivateSecuritiesInvestmentFundFOF,

    [Description("私募股权投资基金")]
    PrivateEquityFund,

    [Description("信托计划")]
    TrustPlan,

    [Description("期货公司及其子公司的资产管理计划")]
    ManagementPlansOfFuture,


    [Description("证券公司及其子公司的资产管理计划")]
    ManagementPlansOfSecurity,

    [Description("创业投资基金")]
    VentureCapitalFund,

    [Description("股权投资基金")]
    EquityFund
}
