using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 交易申请业务类型
/// </summary>
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum TransferRequestType
{
    [Description("未知")] UNK,
    
    [Description("认购")]InitialOffer,
    
    /// <summary>
    /// 认购
    /// </summary>
    [Description("认购")] Subscription,

    /// <summary>
    /// 申购
    /// </summary>
    [Description("申购")] Purchase,

    /// <summary>
    /// 份额调增
    /// </summary>
    [Description("份额调增")] Increase,


    /// <summary>
    /// 受让
    /// </summary>
    [Description("受让")] TransferIn,

    /// <summary>
    /// 份额转换
    /// </summary>
    [Description("份额转换转入")] SwitchIn,

    /// <summary>
    /// 分红
    /// </summary>
    [Description("分红")] Distribution = 9,


    /// <summary>
    /// 赎回
    /// </summary>
    [Description("赎回")] Redemption = 11,

    /// <summary>
    /// 强制赎回
    /// </summary>
    [Description("强制赎回")] ForceRedemption,

    /// <summary>
    /// 份额调减
    /// </summary>
    [Description("份额调减")] Decrease,

    /// <summary>
    /// 转让
    /// </summary>
    [Description("转让")] TransferOut,


    /// <summary>
    /// 份额转换
    /// </summary>
    [Description("份额转换转出")] SwitchOut,



    /// <summary>
    /// 设置分红方式
    /// </summary>
    [Description("设置分红方式")] BonusType = 21,

    /// <summary>
    /// 二次或多次清算产生的金额
    /// </summary>
    [Description("清算")] Clear,


    [Description("取消")] Abort,

}

