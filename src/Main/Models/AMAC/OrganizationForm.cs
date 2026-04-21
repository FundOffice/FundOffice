using System.ComponentModel;

namespace FMO.Models;

/// <summary>
/// 基金组织形式
/// </summary>
public enum OrganizationForm
{
    /// <summary>
    /// 契约型
    /// </summary>
    [Description("契约型")]
    Contractual = 0,

    /// <summary>
    /// 公司型
    /// </summary>
    [Description("公司型")]
    Corporate = 1,

    /// <summary>
    /// 合伙型
    /// </summary>
    [Description("合伙型")]
    Partnership = 2
}
