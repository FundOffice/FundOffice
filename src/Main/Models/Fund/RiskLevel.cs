using System.ComponentModel;

namespace FMO.Models;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum RiskLevel
{
    [Description("未选择")] Unk, R1, R2, R3, R4, R5
}

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum RiskEvaluation
{
    [Description("未选择")] Unk, C1, C2, C3, C4, C5
}
