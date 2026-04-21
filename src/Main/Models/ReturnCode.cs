
using FMO.Models;
using System.ComponentModel;

namespace FMO.Trustee;

[TypeConverter(nameof(EnumDescriptionTypeConverter))]
public enum ReturnCode
{
    Success,


    /// <summary>
    /// 成功，但还有数据
    /// </summary>
    //NotFinished,

    FailedBegin,

    Unknown,

    NoFund,

    /// <summary>
    /// 返回是空白的
    /// </summary>
    EmptyResponse,

    /// <summary>
    /// 数据异常
    /// </summary>
    DataIsNotWellFormed,

    /// <summary>
    /// ToObject有问题
    /// </summary>
    ObjectTransformError,


    /// <summary>
    /// 返回的json无法正常解析成对应的类
    /// </summary>
    JsonNotPairToEntity,


    /// <summary>
    /// 未实现的接口
    /// </summary>
    NotImplemented,

    /// <summary>
    /// 网络问题
    /// </summary>
    Network,

    /// </summary>
    [Description("无效的配置文件")]
    ConfigInvalid,

    /// <summary>
    /// 超时
    /// </summary>
    [Description("超时")]
    TimeOut,

    /// <summary>
    /// 接口不可用
    /// </summary>
    [Description("接口不可用")]
    InterfaceUnavailable,

    /// <summary>
    /// 无权访问
    /// </summary>
    [Description("无权访问")]
    AccessDenied,

    /// <summary>
    /// ip 不在白名单内
    /// </summary>
    [Description("此IP无法访问")]InvalidIP,

    /// <summary>
    /// 参数不合法
    /// </summary>
    [Description("参数不合法")]
    ParameterInvalid,


    /// <summary>
    /// 身份认证失败
    /// </summary>
    [Description("身份认证失败")] 
    IdentitifyFailed,


    /// <summary>
    /// 流量限制
    /// </summary>
    [Description("流量限制")]TrafficLimit,





    #region 中信建投特定
    CSCBegin = 1000,

    /// <summary>
    /// CSC 签名不合法
    /// </summary>
    [Description("签名不合法")]
    CSC_IlligalSign,

    /// <summary>
    /// CSC 签名过期
    /// </summary>
    [Description("签名过期")]
    CSC_SignExpired,

    /// <summary>
    /// 缺少参数apikey
    /// </summary>
    [Description("缺少参数apikey")]
    CSC_APIKEY,

    /// <summary>
    /// 参数body加密错误
    /// </summary>
    [Description("参数body加密错误")]
    CSC_BodyEncrypt,
    /// <summary>
    /// 业务处理失败
    /// </summary>
    [Description("业务处理失败")]
    CSC_BusinessFailed,

    /// <summary>
    /// 系统内部错误
    /// </summary>
    [Description("系统内部错误")]
    CSC_InternalServerError,

    /// <summary>
    /// 第三方流水号为空
    /// </summary>
    [Description("第三方流水号为空")]
    CSC_ThirdPartySerialNumberEmpty,

    /// <summary>
    /// 参数为空
    /// </summary>
    [Description("参数为空")]
    CSC_ParameterEmpty,


    /// <summary>
    /// 业务校验不通过
    /// </summary>
    [Description("业务校验不通过")]
    CSC_BusinessValidationFailed,
    #endregion


    #region 中信
    /// <summary>
    /// Auth/Token 为空
    /// </summary> 
    [Description("Token 为空")] CITICS_NoTokenOrAuth,



    /// <summary>
    /// 口令值不合法
    /// </summary> 
    [Description("口令值不合法")] CITICS_Credentials,



    /// <summary>
    /// Token 过期
    /// </summary> 
    [Description("Token 过期")] CITICS_Token,


    /// <summary>
    /// 流量受限
    /// </summary>
    [Description("流量受限")] CITICS_Limited,

    [Description("无法匹配投资人信息")] CITICS_Investor,

    #endregion


    /// <summary>
    /// 开始到结束不能超过一个月
    /// </summary>
    [Description("开始到结束不能超过一个月")]
    CMS_DateRangeLimitOneMonth,


    XYZQ_CanNotGetToken,
    XYZQ_NoFundIssued,
    XYZQ_FundCode,
}