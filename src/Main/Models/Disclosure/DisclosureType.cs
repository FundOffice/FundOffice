using FMO.Models;
using System.ComponentModel;

namespace FMO.Disclosure;

/// <summary>
/// 信批类型最大支持1023个
/// </summary>
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum DisclosureType
{
    /// <summary>
    /// 月报
    /// </summary>
    [Description("月报")] Monthly = 1,

    /// <summary>
    /// 季报
    /// </summary>
    [Description("季报")] Quarterly,

    /// <summary>
    /// 半年报
    /// </summary>
    [Description("半年报")] SemiAnnually,

    /// <summary>
    /// 年报
    /// </summary>
    [Description("年报")] Annually,

    [Description("季度更新")]QuarterlyUpdate,
    /// <summary>
    /// 临时报告起始
    /// </summary>
    Temporary = 99,

    /// <summary>
    /// 临时开放
    /// </summary>
    [Description("临时开放")] TemporaryOpen,

    /// <summary>
    /// 巨额赎回
    /// </summary>
    [Description("巨额赎回")] HugeRedemption,

    /// <summary>
    /// 基金成立
    /// </summary>
    [Description("基金成立")] FundSetup,


    [Description("其他基金公告")] OtherFundNotice,

    //以下是管理人层面的公告类型，ID从100开始，区分于基金层面的公告

    ManagerLevel = 500,

    /// <summary>
    /// 管理人变更公告
    /// </summary>
    [Description("管理人变更")] MangerChange,

    /// <summary>
    /// 管理人办公地址变更公告
    /// </summary>
    [Description("办公地址变更")] OfficeAddressChange,


    [Description("其他管理人公告")] OtherManagerNotice,
    
}
