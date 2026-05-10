using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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






public partial class SettingsWindowViewModel:ObservableObject
{

    public VerifyRuleUnitViewModel[] VerifyRuleUnits { get; set; }

    public SettingsWindowViewModel()
    {
        VerifyRuleUnits = SettingService.VerifyRuleSection.Values.Select(x=> new VerifyRuleUnitViewModel(x)).ToArray();
    }














}




