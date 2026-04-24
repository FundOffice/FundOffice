using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Disclosure;
using FMO.Utilities;
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

namespace FMO.Disclosure;

/// <summary>
/// ConfigureChannelWindow.xaml 的交互逻辑
/// </summary>
public partial class ConfigureChannelWindow : Window
{
    public ConfigureChannelWindow()
    {
        InitializeComponent();
    }
}


//public partial class ConfigureChannelWindowViewModel:ObservableObject
//{
//    [ObservableProperty]
//    public partial IDisclosureChannel[] Channels { get; set; }

//    [ObservableProperty]
//    public partial IDisclosureChannel? SelectedChannel { get; set; }





//    [ObservableProperty]
//    public partial ChannelConfigViewModel? Config { get; set; }


//    public ConfigureChannelWindowViewModel()
//    {
//        Channels = DisclosureChannelGalley.GetRegisteredChannels().ToArray();
//    }



//    partial void OnSelectedChannelChanged(IDisclosureChannel? value)
//    {
//        if (value is null)
//        {
//            Config = null;
//            return;
//        }

//        // 检查是否有对应的配置界面
//        using var db = DbHelper.Base();
//        var config = db.GetCollection<IDisclosureChannelConfig>().FindOne(x => x.ChannelCode == value.Code);
//        if (config is null)
//            Config = ChannelConfigFactory.CreateConfig(value.Code);
//        else Config = ChannelConfigFactory.CreateConfig(config);
//    }








//}