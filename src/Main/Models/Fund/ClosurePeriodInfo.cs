using System.ComponentModel;

namespace FMO.Models;






public enum ClosureType
{
    [Description("无封闭期")]None,

    [Description("封闭月数")] ByMonth,

    [Description("始终封闭")] Always,

    [Description("其它")] Other
}

