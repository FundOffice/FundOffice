using System.ComponentModel;

namespace FMO.Models;

public enum ConductType
{
    /// <summary>
    /// 既募集又投资
    /// </summary>
    [Description("既募集又投资")]
    BothRaiseAndInvest = 0,

    /// <summary>
    /// 只投资
    /// </summary>
    [Description("只投资")]
    InvestOnly = 1,

    /// <summary>
    /// 只募集
    /// </summary>
    [Description("只募集")]
    RaiseOnly = 2
}
