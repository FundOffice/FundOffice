using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Disclosure;
using FMO.Models;
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
/// AddFundSacleWarningNoticeWindow.xaml 的交互逻辑
/// </summary>
public partial class AddFundSacleWarningNoticeWindow : Window
{
    public AddFundSacleWarningNoticeWindow()
    {
        InitializeComponent();
    }
}


public partial class AddFundSacleWarningNoticeWindowViewModel : AddTemporaryWindowViewModel
{
    public AddFundSacleWarningNoticeWindowViewModel(Fund[] names) : base(names)
    {
    }

    public ScaleWarningType[] WarningTypes { get; set; } = [ScaleWarningType.None,ScaleWarningType.AnnualAverageNetAssetBelow1000W, ScaleWarningType.Continuous60TradeDaysAssetBelow500W, ScaleWarningType.DailyAverageAssetBelow500W];

    [ObservableProperty]
    public partial ScaleWarningType WarningType { get; set; }


    public override bool CanConfirm => WarningType != ScaleWarningType.None;

    /// <summary>
    /// 触发日期
    /// </summary>
    [ObservableProperty]
    public partial DateTime? TouchDate { get;  set; }
}