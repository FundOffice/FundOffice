using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Disclosure;
using FMO.Models;
using System.Windows;

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

    public ScaleWarningType[] WarningTypes { get; set; } = [ScaleWarningType.None, ScaleWarningType.AnnualAverageNetAssetBelow1000W, ScaleWarningType.Continuous60TradeDaysAssetBelow500W, ScaleWarningType.DailyAverageAssetBelow500W];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial ScaleWarningType WarningType { get; set; }


    public override bool CanConfirm => WarningType != ScaleWarningType.None && TouchDate is not null;

    /// <summary>
    /// 触发日期
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial DateTime? TouchDate { get; set; }

    partial void OnTouchDateChanged(DateTime? value)
    {
        if (value is not DateTime t) return;

        if (WarningType == ScaleWarningType.AnnualAverageNetAssetBelow1000W || WarningType == ScaleWarningType.DailyAverageAssetBelow500W)
        {
            var d = new DateTime(t.Year, 12, 31, 23, 59, 59);
            if (d > DateTime.Now) d = d.AddYears(-1);
            TouchDate = d;
        }
    }

    partial void OnWarningTypeChanged(ScaleWarningType value)
    {
        if (WarningType == ScaleWarningType.AnnualAverageNetAssetBelow1000W || WarningType == ScaleWarningType.DailyAverageAssetBelow500W)
        {
            TouchDate = new DateTime(DateTime.Now.Year - 1, 12, 31, 23, 59, 59);
        }
        else if (TouchDate?.Month is 12 && TouchDate?.Day is 31)
            TouchDate = null;
    }
}