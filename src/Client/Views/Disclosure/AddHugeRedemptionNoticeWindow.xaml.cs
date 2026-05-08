using CommunityToolkit.Mvvm.ComponentModel;
using FMO.Models;
using FMO.Utilities;
using System.ComponentModel;
using System.Windows;

namespace FMO;

/// <summary>
/// Interaction logic for AddHugeRedemptionNoticeWindow.xaml
/// </summary>
public partial class AddHugeRedemptionNoticeWindow : Window
{
    public AddHugeRedemptionNoticeWindow()
    {
        InitializeComponent();
    }
}

public partial class AddHugeRedemptionNoticeWindowViewModel : AddTemporaryWindowViewModel
{



    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? RealRatio { get; set; }



    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? DefinedRatio { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial DateTime OpenDate { get; set; }



    [ObservableProperty]
    public partial bool IsFullyPaid { get; set; } = true;


    public override bool CanConfirm => SelectedFund is not null && OpenDate.Year > 2000 && PublishTime.Year > 2000 && DefinedRatio > 0 && RealRatio > DefinedRatio;

    public AddHugeRedemptionNoticeWindowViewModel(Fund[] names) : base(names)
    {

    }


    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(SelectedFund):
                UpdateDefined();
                break;

            case nameof(OpenDate):
                UpdateReal();
                break;

            default:
                break;
        }
    }

    private void UpdateReal()
    {
        if (SelectedFund is null) return;

        using var db = DbHelper.Base();
      
        var days = OpenDate.Ticks / 864000000000;
        var sbd = db.GetCollection<FundShareRecordByDaily>().Query().Where(x => x.FundId == SelectedFund.Id && x.Date.DayNumber <= days).OrderByDescending(x => x.Date).Limit(2).ToArray();
        if (sbd.Length == 2)
        {
            RealRatio = (sbd[1].Share - sbd[0].Share) / sbd[1].Share;
        }
    }

    private void UpdateDefined()
    {
        if (SelectedFund is null) return;

        using var db = DbHelper.Base();
        var elements = db.GetCollection<FundElements>().FindById(SelectedFund.Id);
        if (elements is null) return;

        DefinedRatio = elements.HugeRedemptionRatio.Value * 100;

        var days = OpenDate.Ticks / 864000000000;
        var sbd = db.GetCollection<FundShareRecordByDaily>().Query().Where(x=>x.FundId == SelectedFund.Id && x.Date.DayNumber <= days).OrderByDescending(x => x.Date).Limit(2).ToArray();
        if (sbd.Length == 2)
        {
            RealRatio = (sbd[1].Share - sbd[0].Share) / sbd[1].Share;
        }


    }




}