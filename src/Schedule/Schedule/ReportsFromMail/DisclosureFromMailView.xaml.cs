using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FMO.Schedule;

/// <summary>
/// DisclosureFromMailView.xaml 的交互逻辑
/// </summary>
public partial class DisclosureFromMailView : UserControl
{
    public DisclosureFromMailView()
    {
        InitializeComponent();
    }
}

[MissionTitle("信披更新")]
public partial class DisclosureFromMailViewModel : MissionViewModel<DisclosureFromMailMission>
{
    [ObservableProperty] 
    public partial string? MailName { get; set; }


    [ObservableProperty]
    public partial int? Interval { get; set; }


    [ObservableProperty]
    public partial DateTime StartDate { get; set; }


    public DisclosureFromMailViewModel(DisclosureFromMailMission m) : base(m)
    {
        Title = "信披更新";

        MailName = m.MailName;
        Interval = m.Interval == 0 ? null : m.Interval;
    }



    [RelayCommand]
    public async Task RebuildData()
    {
        Mission.IgnoreHistory = true;
        await Task.Run(() => Mission.Work());
        Mission.IgnoreHistory = false;
    }
}