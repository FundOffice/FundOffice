using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using MoT;
using System.IO;
using System.Windows;

namespace FMO;

[AutoChangeableViewModel(typeof(TransferOrder))]
partial class TransferOrderViewModel : ITransferViewModel
{
    public bool IsConfirmed { get => field; set { field = value; OnPropertyChanged(nameof(IsConfirmed)); } }

    /// <summary>
    /// 是否已申请
    /// </summary>
    public bool IsApplyed { get => field; set { field = value; OnPropertyChanged(nameof(IsApplyed)); } }


    public bool IsEditable => string.IsNullOrWhiteSpace(Source) || Source == "manual";



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
    public void ModifyOrder()
    {
        using var db = DbHelper.Base();
        var order = db.GetCollection<TransferOrder>().FindById(Id);
        if (order is null)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, $"订单【{Id}】不存在"));
            return;
        }

        var wnd = new ModifyOrderWindow();
        wnd.DataContext = new ModifyOrderWindowViewModel(order, false);
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();
    }

    [RelayCommand]
    public void OpenFund() => WeakReferenceMessenger.Default.Send(new OpenFundMessage(FundId!.Value));


    [RelayCommand]
    public void OpenFile(SimpleFile? file)
    {
        if (file?.File is null) return;
        try
        {
            var f = file.File;
            Directory.CreateDirectory(@$"temp\{f.Id}");
            string tmp = @$"temp\{f.Id}\{f.Name}";

            if (!File.Exists(tmp))
                FileMeta.CreateHardLink(@$"files\hardlink\{f.Id}", tmp);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tmp) { UseShellExecute = true });
        }
        catch (Exception e) { Logg.Error(e); WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "无法打开文件")); }
    }



}
