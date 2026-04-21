using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using Serilog;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace FMO;

/// <summary>
/// ModifyOrderWindow.xaml 的交互逻辑
/// </summary>
public partial class ModifyOrderWindow : Window
{
    public ModifyOrderWindow()
    {
        InitializeComponent();
    }
}


public partial class ModifyOrderWindowViewModel : AddOrderWindowViewModelBase
{
    public ModifyOrderWindowViewModel(int orderId, bool isReadOnly = true)
    {
        IsReadOnly = isReadOnly;
        using var db = DbHelper.Base();
        var order = db.GetCollection<TransferOrder>().FindById(orderId);
        Order = order;
        SelectedType = order.Type;

        Types = order.Type switch
        {
            TransferOrderType.FirstTrade or TransferOrderType.Buy => [TransferOrderType.FirstTrade, TransferOrderType.Buy],
            _ => [TransferOrderType.Share, TransferOrderType.Amount, TransferOrderType.RemainAmout ]
        };

        Number = order.Number;

        IsSellTypeForzen = SelectedType == TransferOrderType.Share;


        if (order is not null)
        {
            Id = order.Id;
            Date = new DateTime(order.Date, default);
            Contract.Meta = order.Contract?.File;
            OrderFile.Meta = order.OrderSheet?.File;
            RiskDisclosure.Meta = order.RiskDiscloure?.File;
            RiskPair.Meta = order.RiskPair?.File;
            Video.Meta = order.Videotape?.File;
            Review.Meta = order.Review?.File;
        }


        if (order.Type == TransferOrderType.FirstTrade)
        {
            Contract.IsRequired = true;
            RiskDisclosure.IsRequired = true;
        }
        else
        {
            Video.IsRequired = false;
            OrderFile.IsRequired = true;
        }

    
        Check();
    }


    public TransferOrder Order { get; set; }


    public TransferRecord[]? SameDay { get; set; }



    [ObservableProperty]
    public partial bool IsSellTypeForzen { get; set; }

    /// <summary>
    /// 同日的确认合并一个订单
    /// </summary>
    [ObservableProperty]
    public partial bool MergeOrderBySameDay { get; set; }

    [ObservableProperty]
    public partial bool DateMayNotGood { get; set; }

    public override bool CanConfirm => Date is not null && Number > 0 && SelectedType is not null;

    protected override void ConfirmOverride()
    {
        using var db = DbHelper.Base();
        db.BeginTrans();
        try
        {

            Order.Date = DateOnly.FromDateTime(Date ?? default);
            Order.Type = SelectedType!.Value;
            Order.Number = Number ?? 0;
            Order.Contract = new SimpleFile { File = Contract.Meta };
            Order.RiskDiscloure =new SimpleFile { File = RiskDisclosure.Meta };
            Order.OrderSheet =new SimpleFile { File = OrderFile.Meta };
            Order.Videotape =new SimpleFile { File = Video.Meta };
            Order.RiskPair =new SimpleFile { File = RiskPair.Meta };
            Order.Review =new SimpleFile { File = Review.Meta };

            db.GetCollection<TransferOrder>().Upsert(Order);
            db.Commit();

            WeakReferenceMessenger.Default.Send(Order);
        }
        catch (Exception e)
        {
            db.Rollback();
            Log.Error($"更新交易订单失败，{e}");
        }
    }


    private TransferOrderType GetSellType(TransferRecordViewModel record)
    {
        if (record.RequestAmount is null || record.RequestAmount == 0)
            return TransferOrderType.Share;
        else if (record.RequestAmount is not null && record.ConfirmedNetAmount is not null && Math.Abs(record.RequestAmount.Value - record.ConfirmedNetAmount.Value) < 10)
            return TransferOrderType.Amount;
        else return TransferOrderType.RemainAmout;
    }



    protected override void Check()
    {
        var tip = "";

        // 判断是否是首次

    }

    internal void OnBatchFile(string[]? v)
    {
        if (v is null) return;

        if (v.Length == 1 && System.IO.Path.GetExtension(v[0]) switch { "zip" or "gzip" or "rar" or "7z" => true, _ => false })
        {
            using var fs = new FileStream(v[0], FileMode.Open);
            using ZipArchive archive = new ZipArchive(fs);

            var file = archive.Entries.Where(x => x.Name.Contains("基金合同"));


        }
    }

}