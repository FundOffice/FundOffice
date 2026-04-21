using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
[Description("TA业务类型")]
public enum TransferRecordType
{
    [Description("未知")] UNK,


    [Description("认购")] InitialOffer,

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
    [Description("转入")] MoveIn,
    /// <summary>
    /// 受让
    /// </summary>
    [Description("受让")] TransferIn = MoveIn,

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

 
    [Description("转出")] MoveOut,
    /// <summary>
    /// 转让
    /// </summary>
    [Description("转让")] TransferOut = MoveOut,



    /// <summary>
    /// 份额转换
    /// </summary>
    [Description("份额转换转出")] SwitchOut,


    /// <summary>
    /// 二次或多次清算产生的金额
    /// </summary>
    [Description("清算")] Clear = 21,



    /// <summary>
    /// 设置分红方式
    /// </summary>
    [Description("设置分红方式")] BonusType,


    /// <summary>
    /// 冻结
    /// </summary>
    [Description("冻结")]Frozen,

    /// <summary>
    /// 解冻
    /// </summary>
    [Description("解冻")] Thawed,

    /// <summary>
    /// 募集失败
    /// </summary>
    [Description("募集失败")] RaisingFailed,

    [Description("撤单")] Abort
}

