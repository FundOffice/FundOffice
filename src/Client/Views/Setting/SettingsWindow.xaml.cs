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


    public IUnitViewModel?[] BasicGroups { get; set; }


    public IUnitViewModel?[] OrderSection { get; set; }


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
                units.Add(new() { Name = title, Units = item.Select(x => SettingViewModels.CreateViewModel(x)) });

            }

        }

        MonitorGroups = units.ToArray();



        var basics = SettingService.GetAbilityUnits("Basic");//.Select(x => new AbilityUnitViewModel(x)).ToArray();
        BasicGroups = basics.Select(x => SettingViewModels.CreateViewModel(x)).ToArray();


        var u = SettingService.GetUnits("Order");
        OrderSection = [.. u.Select(x => SettingViewModels.CreateViewModel(x))];
    }














}


public class SettingMonitorGroup
{
    public required string Name { get; set; }


    public required IEnumerable<IUnitViewModel?> Units { get; set; }
}

