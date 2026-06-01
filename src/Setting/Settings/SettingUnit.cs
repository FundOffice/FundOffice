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
    public const string VerifyRule = "Monitor.VerifyRule";

    /// <summary>
    /// TA
    /// </summary>
    public const string TransferMonitor = "Monitor.Transfer";


    /// <summary>
    /// 基金流程
    /// </summary>
    public const string FundOperationMonitor = "Monitor.FundOperation";


    /// <summary>
    /// 基金流程
    /// </summary>
    public const string FundMonitor = "Monitor.Fund";

}




public class AbilityUnit : SettingUnit
{
    public bool IsEnabled { get; set; }

}



public class SwitchUnit : SettingUnit
{
    public bool IsEnabled { get; set; }
}


