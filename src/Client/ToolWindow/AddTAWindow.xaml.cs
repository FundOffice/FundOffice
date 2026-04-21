using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace FMO;

/// <summary>
/// AddTAWindow.xaml 的交互逻辑
/// </summary>
public partial class AddTAWindow : Window
{
    public AddTAWindow()
    {
        InitializeComponent();


        Loaded += (s, e) => { if (DataContext is AddTAWindowViewModel d) d.SearchInvestorKey = null; };
    }

    private void Search_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is ComboBox obj) obj.IsDropDownOpen = true;
    }

    private void Search_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ComboBox obj) obj.IsDropDownOpen = true;
    }
}


public partial class AddTAWindowViewModel : ObservableObject
{
    public Fund[] Funds { get; set; }

    public CollectionViewSource FundSource { get; } = new();

    [ObservableProperty]
    public partial string? SearchFundKey { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial Fund? SelectedFund { get; set; }



    public Investor[] Investors { get; set; }

    public CollectionViewSource InvestorSource { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial string? SearchInvestorKey { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial Investor? SelectedInvestor { get; set; }


    public TransferRecordType[] Types { get; } = [TransferRecordType.Subscription, TransferRecordType.Purchase, TransferRecordType.Redemption, TransferRecordType.ForceRedemption, TransferRecordType.Distribution];



    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRequestNumber))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial TransferRecordType? SelectedType { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial DateTime? RequestDate { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial DateTime? ConfirmDate { get; set; }


    public bool ShowRequestNumber => SelectedType switch { TransferRecordType.Purchase or TransferRecordType.Subscription or TransferRecordType.Redemption or TransferRecordType.ForceRedemption => true, _ => false };


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? RequestNumber { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? ConfirmShare { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? ConfirmAmount { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? Fee { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? ConfirmNetAmount { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? PerformanceFee { get; set; }


    [ObservableProperty]
    public partial string? Tips { get; set; }

    public AddTAWindowViewModel()
    {
        using var db = DbHelper.Base();
        Funds = db.GetCollection<Fund>().FindAll().ToArray();

        FundSource.Source = Funds;
        FundSource.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(SearchFundKey) ? true : e.Item switch { Fund f => f.Name.Contains(SearchFundKey), _ => true };


        Investors = db.GetCollection<Investor>().FindAll().ToArray();
        InvestorSource.Source = Investors;
        InvestorSource.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(SearchInvestorKey) ? true : e.Item switch { Fund f => f.Name.Contains(SearchInvestorKey), _ => true };


    }






    partial void OnSearchFundKeyChanged(string? value) => FundSource.View.Refresh();


    partial void OnSearchInvestorKeyChanged(string? value) => InvestorSource.View.Refresh();



    public bool CanConfirm => SelectedFund is not null && SelectedType is not null && SelectedInvestor is not null && RequestDate is not null && ConfirmDate is not null && ConfirmShare is not null && ConfirmAmount is not null && ConfirmNetAmount is not null && SelectedType switch { TransferRecordType.Distribution or TransferRecordType.Redemption or TransferRecordType.ForceRedemption => PerformanceFee is not null, _ => true };






    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm()
    {
        using var db = DbHelper.Base();

        var tq = new TransferRequest
        {
            FundId = SelectedFund!.Id,
            FundCode = SelectedFund!.Code,
            FundName = SelectedFund!.Name,
            InvestorId = SelectedInvestor!.Id,
            InvestorIdentity = SelectedInvestor!.Identity!.Id,
            InvestorName = SelectedInvestor!.Name,
            RequestDate = DateOnly.FromDateTime(RequestDate!.Value),
            RequestAmount = SelectedType switch { TransferRecordType.Subscription or TransferRecordType.Purchase => RequestNumber ?? 0, _ => 0 },
            RequestShare = SelectedType switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption => RequestNumber ?? 0, _ => 0 },
            RequestType = SelectedType switch { TransferRecordType.Subscription => TransferRequestType.Subscription, TransferRecordType.Purchase => TransferRequestType.Purchase, TransferRecordType.Redemption => TransferRequestType.Redemption, TransferRecordType.ForceRedemption => TransferRequestType.ForceRedemption, _ => TransferRequestType.UNK },
            CreateDate = DateOnly.FromDateTime(DateTime.Today),
            Source = "manual"
        };
        db.GetCollection<TransferRequest>().DropIndex("SourceSourceExternalIdExternalId");
        db.GetCollection<TransferRequest>().Insert(tq);
        DataTracker.OnBatchTransferRequest([tq]);


        var ta = new TransferRecord
        {
            FundId = SelectedFund!.Id,
            FundCode = SelectedFund!.Code,
            FundName = SelectedFund!.Name,
            InvestorId = SelectedInvestor!.Id,
            InvestorIdentity = SelectedInvestor.Identity.Id,
            InvestorName = SelectedInvestor!.Name,
            RequestDate = DateOnly.FromDateTime(RequestDate!.Value),
            ConfirmedDate = DateOnly.FromDateTime(ConfirmDate!.Value),
            RequestAmount = SelectedType switch { TransferRecordType.Subscription or TransferRecordType.Purchase => RequestNumber ?? 0, _ => 0 },
            RequestShare = SelectedType switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption => RequestNumber ?? 0, _ => 0 },
            ConfirmedShare = ConfirmShare ?? 0,
            ConfirmedAmount = ConfirmAmount ?? 0,
            ConfirmedNetAmount = ConfirmNetAmount ?? 0,
            CreateDate = DateOnly.FromDateTime(DateTime.Today),
            Type = SelectedType!.Value,
            Source = "manual",
            Fee = Fee ?? 0,
            PerformanceFee = PerformanceFee ?? 0,
            RequestId = tq.Id
        };
        db.GetCollection<TransferRecord>().Insert(ta);

        DataTracker.OnBatchTransferRecord([ta]);

        // 发送消息
        WeakReferenceMessenger.Default.Send(new FundTipMessage(SelectedFund.Id));
        App.Current.Windows[^1].Close();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        //if (e.PropertyName == nameof(CanConfirm))
        if (CanConfirm)
        {
            // 检查是否有重复数据
            using var db = DbHelper.Base();
            var old = db.GetCollection<TransferRecord>().Find(x => x.FundId == SelectedFund!.Id && x.InvestorId == SelectedInvestor!.Id && x.Type == SelectedType && x.ConfirmedDate == DateOnly.FromDateTime(ConfirmDate!.Value)).ToArray();
            old = old.Where(x => x.ConfirmedShare == ConfirmShare && x.ConfirmedAmount == ConfirmAmount && x.ConfirmedNetAmount == ConfirmNetAmount && x.PerformanceFee == (PerformanceFee ?? 0)).ToArray();

            if (old.Any())
                Tips = $"可能存在重复数据";
            else Tips = null;
        }
    }
}