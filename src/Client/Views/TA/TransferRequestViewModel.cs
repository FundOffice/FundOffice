using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Shared;
using FMO.Trustee;
using FMO.Utilities;
using System.Windows;
using System.Windows.Controls;

namespace FMO;


public interface ITransferViewModel
{
    string FundName { get; }

    string InvestorName { get; }
}

public interface IHasOrderViewModel
{
    int OrderId { get; }

    bool HasOrder { get; }

    bool LackOrder { get; }

    bool IsSameManager { get; }

    bool IsOrderRequired { get; }

    bool IsLiquidating { get; }

    public bool IsBuy();

    public bool IsSell();
}



[AutoChangeableViewModel(typeof(TransferRequest))]
partial class TransferRequestViewModel : ITransferViewModel, IHasOrderViewModel
{

    //public int OrderId
    //{
    //    get => field; set
    //    {
    //        field = value;
    //        OnPropertyChanged(nameof(OrderId));
    //        OnPropertyChanged(nameof(LackOrder));
    //        OnPropertyChanged(nameof(HasOrder));
    //    }
    //}

    int IHasOrderViewModel.OrderId => OrderId!.Value;

    public bool HasOrder => OrderId != 0;

    public bool LackOrder => IsOrderRequired && OrderId == 0;

    public bool IsSameManager { get; set; }

    [RelayCommand]
    public void OpenInvestorView()
    {
        var wnd = new HandyControl.Controls.Window
        {
            MaxHeight = App.Current.MainWindow.ActualHeight,
            Content = new CustomerView() { Margin = new Thickness(10) },
            DataContext = new CustomerViewModel(InvestorId!.Value),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = App.Current.MainWindow,
        };
        wnd.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) wnd.Close(); };
        wnd.ShowDialog();
    }

    [RelayCommand]
    public void ViewOrder()
    {
        var wnd = new ModifyOrderWindow();
        wnd.DataContext = new ModifyOrderWindowViewModel(OrderId!.Value);
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();
    }


    [RelayCommand]
    public void ViewJson()
    {
        if (ExternalId?.Split('.') is string[] arr && arr.Length > 0)
        {
            using var db = DbHelper.Platform();
            var json = db.GetCollection($"{arr[0]}_{nameof(ITrustee.QueryTransferRequests)}").Find($"JsonId='{ExternalId}'").LastOrDefault();

            if (json is null) return;

            Window wnd = new Window
            {
                Width = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = App.Current.MainWindow,
                Content = new DataGrid
                {
                    ItemsSource = json?.ToDictionary(x => x.Key, x => x.Value),
                    Style = App.Current.FindResource("DataGrid.Small") as Style
                }
            };

            wnd.ShowDialog();
        }
    }

    [RelayCommand]
    public void OpenFund() => WeakReferenceMessenger.Default.Send(new OpenFundMessage(FundId!.Value));

    public bool IsBuy() => RequestType!.Value.IsBuy();
    public bool IsSell() => RequestType!.Value.IsSell();

}
