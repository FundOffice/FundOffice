using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Settings;
using System.Windows;

namespace FMO;

/// <summary>
/// SettingsWindow.xaml 的交互逻辑
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        Loaded += (s, e) => DataContext = new SettingsWindowViewModel();
    }

}






public partial class SettingsWindowViewModel : ObservableObject
{



    /// <summary>
    /// TA监控，包括订单 申请和确认
    /// </summary>
    public SettingMonitorGroup[] MonitorGroups { get; set; }


    public AbilityUnitViewModel[] BasicGroups { get; set; }


    public UnitViewModel[] OrderSection { get; set; }


    public SettingsWindowViewModel()
    {
        var dic = new Dictionary<string, string>
        {
            [SettingSections.TransferMonitor] = "订单&交易",
            [SettingSections.FundOperationMonitor] = "基金运营",
            [SettingSections.FundMonitor] = "基金信息"
        };


        //OrderMonitorUnits = SettingService.GetAbilityUnits(SettingSections.OrderMonitor).Select(x => new AbilityUnitViewModel(x)).ToArray();

        var monitors = SettingService.GetAbilityUnits("Monitor");//.Select(x => new AbilityUnitViewModel(x)).ToArray();

        List<SettingMonitorGroup> units = [];
        foreach (var item in monitors.GroupBy(x => x.Section))
        {
            if (dic.TryGetValue(item.Key, out var title))
            {
                units.Add(new() { Name = title, Units = item.Select(x => new AbilityUnitViewModel(x)) });

            }

        }

        MonitorGroups = units.ToArray();



        var basics = SettingService.GetAbilityUnits("Basic");//.Select(x => new AbilityUnitViewModel(x)).ToArray();
        BasicGroups = basics.Select(x => new AbilityUnitViewModel(x)).ToArray();


        var u = SettingService.GetUnits("Order");
        if (!u.Any(x => x.Name == "AllowCreateTemporaryInESigning"))
            SettingService.RegisterSwitch("Order", "AllowCreateTemporaryInESigning", "允许在电签平台设置开放日", "允许在电签平台设置开放日，即使它未不是托管平台中的开放日", true);

        OrderSection = [.. u.Select(x => SettingService.CreateViewModel(x) as UnitViewModel)];
    }














}


public class SettingMonitorGroup
{
    public required string Name { get; set; }


    public required IEnumerable<AbilityUnitViewModel> Units { get; set; }
}

