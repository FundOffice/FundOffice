using FMO.Trigger;
using System.Collections.Immutable;

namespace FMO.Settings;

public class SettingUnit
{
    public string Id => $"{Section}.{Name}";

    public required string Section { get; set; }

    public required string Name { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }




}

public static class SettingSections
{
    public const string VerifyRule = "VerifyRule";
}

  


public class VerifyRuleUnit : SettingUnit
{
    public bool IsEnabled { get; set; }

}

