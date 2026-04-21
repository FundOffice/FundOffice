using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum FundStatus
{
    Unk,

    [Description("项目发起")] Initiate,

    [Description("合同定稿")] ContractFinalized,

    [Description("基金成立")] Setup,

    [Description("中基协备案")] Registration,

    [Description("正在运作")] Normal = 11,

    [Description("开始清算")] StartLiquidation = 21,

    [Description("提前清算")] EarlyLiquidation,

    [Description("正常清算")] Liquidation,

    [Description("延期清算")] LateLiquidation,

    [Description("投顾协议已终止")] AdvisoryTerminated,
}