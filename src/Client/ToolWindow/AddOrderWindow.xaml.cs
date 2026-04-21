using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using Serilog;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// AddOrderWindow.xaml 的交互逻辑
/// </summary>
public partial class AddOrderWindow : Window
{
    public AddOrderWindow()
    {
        InitializeComponent();
    }


}



public abstract partial class AddOrderWindowViewModelBase : ObservableObject
{
    public AddOrderWindowViewModelBase()
    {
        Contract = new();
        Contract.FileChanged += f => UpdateFile(new { Contract = f });


        RiskDisclosure = new();
        RiskDisclosure.FileChanged += f => UpdateFile(new { RiskDisclosure = f });


        OrderFile = new();
        OrderFile.FileChanged += f => UpdateFile(new { OrderFile = f });
        OrderFile.FileChanged += f => SetDate(f);


        Video = new();
        Video.FileChanged += f => UpdateFile(new { Video = f });


        RiskPair = new();
        RiskPair.FileChanged += f => UpdateFile(new { RiskPair = f });


        Review = new();
        Review.FileChanged += f => UpdateFile(new { Review = f });

        //Contract = new()
        //{
        //    Label = "基金合同",
        //    OnSetFile = (x, y) => SetFile(x, y, (a, b) => a.Contract = b),
        //    OnDeleteFile = x => DeleteFile(x),
        //    FileChanged = () => Check()
        //};
        //RiskDisclosure = new()
        //{
        //    Label = "风险揭示书",
        //    OnSetFile = (x, y) => SetFile(x, y, (a, b) => a.RiskDiscloure = b),
        //    OnDeleteFile = x => DeleteFile(x),
        //    FileChanged = () => Check()
        //};
        //OrderFile = new()
        //{
        //    Label = "认申赎单",
        //    OnSetFile = (x, y) => SetFile2(x, y, (a, b) => a.OrderSheet = b),
        //    OnDeleteFile = x => DeleteFile(x),
        //    FileChanged = () => Check()
        //};
        //Video = new()
        //{
        //    Label = "双录",
        //    OnSetFile = (x, y) => SetFile(x, y, (a, b) => a.Videotape = b),
        //    OnDeleteFile = x => DeleteFile(x),
        //    FileChanged = () => Check()
        //};


        //RiskPair = new()
        //{
        //    Label = "风险匹配告知书",
        //    OnSetFile = (x, y) => SetFile(x, y, (a, b) => a.RiskPair = b),
        //    OnDeleteFile = x => DeleteFile(x),
        //    FileChanged = () => Check()
        //};

        //Review = new()
        //{
        //    Label = "回访",
        //    OnSetFile = (x, y) => SetFile(x, y, (a, b) => a.Review = b),
        //    OnDeleteFile = x => DeleteFile(x),
        //    FileChanged = () => Check()
        //};
    }

    private void SetDate(SimpleFile f)
    {
        if (Date is null && DateTimeHelper.TryFindDate(f.File?.Name) is DateOnly date)
            Date = new DateTime(date, default);
    }

    private void UpdateFile<T>(T v)
    {
        if (Id == 0) return;
        using var db = DbHelper.Base();
        db.GetCollection<TransferOrder>().UpdateMany(BsonMapper.Global.ToDocument(v).ToString(), $"_id={Id}");
    }

    public int Id { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial decimal? Number { get; set; }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSell))]
    public partial TransferOrderType? SelectedType { get; set; }

    [ObservableProperty]
    public partial TransferOrderType[] Types { get; set; } = [TransferOrderType.FirstTrade, TransferOrderType.Buy, TransferOrderType.Share, TransferOrderType.Amount, TransferOrderType.RemainAmout];


    [ObservableProperty]
    public partial bool IsVideoNesscessary { get; set; } = false;



    public OrderFileViewModel Contract { get; }

    public OrderFileViewModel RiskDisclosure { get; }


    public OrderFileViewModel OrderFile { get; }


    public OrderFileViewModel Video { get; }


    public OrderFileViewModel RiskPair { get; }


    public OrderFileViewModel Review { get; }



    //partial void OnDateChanged(DateTime? value)
    //{
    //    if (!SupplementaryMode) return;

    //    var tip = "签约日期晚于申请日期";
    //    var max = SelectedRecords?.Any() ?? false ? SelectedRecords.Max(x => x.RequestDate) : RecordSource.View.Cast<TransferRecord>().Max(x => x.RequestDate);

    //    bool need = value is not null && DateOnly.FromDateTime(value.Value) > max;
    //    if (!need)
    //        Tips.Remove(tip);
    //    else if (!Tips.Contains(tip))
    //        Tips.Add(tip);
    //}



    //partial void OnSelectedRecordsChanged(IEnumerable<TransferRecord>? value)
    //{
    //    if (!SupplementaryMode || Date is null) return;

    //    var tip = "签约日期晚于申请日期";
    //    var max = SelectedRecords?.Any() ?? false ? SelectedRecords.Max(x => x.RequestDate) : RecordSource.View.Cast<TransferRecord>().Max(x => x.RequestDate);

    //    bool need = value is not null && DateOnly.FromDateTime(Date.Value) > max;
    //    if (!need)
    //        Tips.Remove(tip);
    //    else if (!Tips.Contains(tip))
    //        Tips.Add(tip);
    //}







    public virtual bool CanConfirm => true;


    [ObservableProperty]
    public partial string? Tips { get; set; }


    public bool IsSell => SelectedType > TransferOrderType.Buy;


    protected abstract void Check();



    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm(Window window)
    {
        ConfirmOverride();

        window.DialogResult = true;
        window.Close();
    }


    protected abstract void ConfirmOverride();


    //private void DeleteFile(FileStorageInfo x)
    //{
    //    if (Id == 0) //未保存
    //        return;

    //    using var db = DbHelper.Base();
    //    if (db.GetCollection<TransferOrder>().FindById(Id) is TransferOrder o)
    //    {
    //        if (Contract.File == x)
    //            o.Contract = null;
    //        else if (RiskDisclosure.File == x)
    //            o.RiskDiscloure = null;
    //        else if (RiskPair.File == x)
    //            o.RiskPair = null;
    //        else if (Review.File == x)
    //            o.Review = null;
    //        else if (Video.File == x)
    //            o.Videotape = null;
    //        else if (OrderFile.File == x)
    //            o.OrderSheet = null;

    //        db.GetCollection<TransferOrder>().Update(o);
    //    }
    //}





    private TransferOrder? Parse(string text)
    {
        TransferOrder order = new();
        var m = Regex.Match(text, @"姓名/名称：([-\(\)（）\w]+)");
        if (!m.Success)
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "交易申请单中，未找到投资人名称"));
        order.InvestorName = m.Groups[1].Value;

        m = Regex.Match(text, @"证件号码：([\da-zA-Z]+)");
        if (!m.Success)
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "交易申请单中，未找到证件号码"));
        order.InvestorIdentity = m.Groups[1].Value;

        m = Regex.Match(text, @"产品名称：([\da-zA-Z]+)");
        if (!m.Success)
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "交易申请单中，未找到产品名称"));
        order.FundName = m.Groups[1].Value;

        m = Regex.Match(text, @"赎回方式：([\da-zA-Z]+)");
        if (!m.Success)
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "交易申请单中，未找到产品名称"));
        order.FundName = m.Groups[1].Value;




        return order;
    }

}


public partial class AddOrderWindowViewModel : AddOrderWindowViewModelBase
{
    public TransferOrderViewModel? Order { get; internal set; }


    public Fund[] Funds { get; set; }

    public CollectionViewSource FundSource { get; } = new();


    [ObservableProperty]
    public partial string? ShareClass { get; set; }

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


    public InvestorQualification[]? Qualifications { get; set; }

    [ObservableProperty]
    public partial bool IsFirstTrade { get; set; }


    public AddOrderWindowViewModel()
    {
        using var db = DbHelper.Base();
        Funds = db.GetCollection<Fund>().FindAll().ToArray();

        FundSource.Source = Funds;
        FundSource.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(SearchFundKey) || SearchFundKey == SelectedFund?.Name ? true : e.Item switch { Fund f => f.Name.Contains(SearchFundKey), _ => true };


        Investors = db.GetCollection<Investor>().FindAll().ToArray();
        InvestorSource.Source = Investors;
        InvestorSource.Filter += (s, e) =>
        {
            e.Accepted = (string.IsNullOrWhiteSpace(SearchInvestorKey) || SearchInvestorKey == SelectedInvestor?.Name ? true : e.Item switch { Investor f => f.Name.Contains(SearchInvestorKey), _ => true });
        };

    }

    partial void OnSearchFundKeyChanged(string? value) => FundSource.View.Refresh();
    partial void OnSearchInvestorKeyChanged(string? value) => InvestorSource.View.Refresh();

    partial void OnSelectedFundChanged(Fund? value) => CheckFirstTrade();

    partial void OnSelectedInvestorChanged(Investor? value)
    {
        if (value is null)
            Qualifications = null;
        else using (var db = DbHelper.Base())
                Qualifications = db.GetCollection<InvestorQualification>().Find(x => x.InvestorId == value.Id).ToArray();

        CheckFirstTrade();
    }

    public override bool CanConfirm => SelectedFund is not null && SelectedInvestor is not null && Date is not null && SelectedType is not null && Number is not null;

    protected override void ConfirmOverride()
    {

        using var db = DbHelper.Base();
        // db.BeginTrans();
        try
        {
            // 如果是新增加的
            //if (Id == 0)
            //{
            //    var obj = new TransferOrder();
            //    db.GetCollection<TransferOrder>().Insert(obj);
            //    Id = obj.Id;

            //// 移动文件
            //Contract.File = Move(Contract.File);
            //RiskDisclosure.File = Move(RiskDisclosure.File);
            //OrderFile.File = Move(OrderFile.File);
            //Video.File = Move(Video.File);
            //Contract.File = Move(Contract.File);
            //}


            TransferOrder order = new TransferOrder
            {
                Id = Id,
                Date = DateOnly.FromDateTime(Date ?? default),
                FundId = SelectedFund!.Id,
                FundName = SelectedFund!.Name,
                ShareClass = ShareClass,
                InvestorId = SelectedInvestor!.Id,
                InvestorName = SelectedInvestor.Name,
                InvestorIdentity = SelectedInvestor.Identity?.Id,
                Type = SelectedType!.Value,
                Number = Number ?? 0,
                Contract = new SimpleFile { File = Contract.Meta },
                RiskDiscloure = new SimpleFile { File = RiskDisclosure.Meta },
                OrderSheet = new SimpleFile { File = OrderFile.Meta },
                Videotape = new SimpleFile { File = Video.Meta },
                RiskPair = new SimpleFile { File = RiskPair.Meta },
                Review = new SimpleFile { File = Review.Meta },
            };
            db.GetCollection<TransferOrder>().Upsert(order);
            //db.Commit();

            WeakReferenceMessenger.Default.Send(order);
        }
        catch (Exception e)
        {
            db.Rollback();
            Log.Error($"添加交易订单失败，{e}");
        }
    }

    //public TransferOrder? Build()
    //{
    //    if (Date is null || SelectedFund is null || SelectedInvestor is null || SelectedType is null || Number is null) return null;

    //    var order = new TransferOrder
    //    {
    //        Date = DateOnly.FromDateTime(Date.Value),
    //        CreateDate = DateOnly.FromDateTime(DateTime.Now),
    //        FundId = SelectedFund.Id,
    //        FundName = SelectedFund.Name,
    //        InvestorId = SelectedInvestor.Id,
    //        InvestorName = SelectedInvestor.Name,
    //        InvestorIdentity = SelectedInvestor.Identity!.Id,
    //        Number = Number.Value,
    //        ShareClass = ShareClass,
    //        Type = SelectedType.Value,
    //        OrderSheet = OrderFile.File,

    //    };

    //    using var db = DbHelper.Base();
    //    db.GetCollection<TransferOrder>().Insert(order);

    //    // 移动文件
    //    order.Contract = Move(Contract.File);
    //    order.RiskDiscloure = Move(RiskDisclosure.File);
    //    order.OrderSheet = Move(OrderFile.File);
    //    order.Videotape = Move(Video.File);
    //    order.Review = Move(Review.File);
    //    order.RiskPair = Move(RiskPair.File);
    //    db.GetCollection<TransferOrder>().Update(order);
    //    return order;
    //}

    protected override void Check()
    {
        var tip = "";

        // 判断是否是首次
        bool need = false;
        bool needvideo = false;
        DateOnly date = Date is null ? default : DateOnly.FromDateTime(Date.Value);
        if (SelectedType == TransferOrderType.Buy)
        {
            using var db = DbHelper.Base();
            int fundid = SelectedFund?.Id ?? 0;
            var cid = SelectedInvestor?.Id ?? 0;
            var early = db.GetCollection<TransferRecord>().Find(x => x.FundId == fundid && x.InvestorId == cid).Min(x => x.RequestDate);

            if (Date is not null && early >= date) need = true;

            var q = db.GetCollection<InvestorQualification>().Find(x => x.InvestorId == cid).Where(x => x.Date <= date).OrderBy(x => x.Date).LastOrDefault();
            if (q is null || q.Result == QualifiedInvestorType.Normal) needvideo = true;
        }

        IsVideoNesscessary = needvideo;

        if (Date is not null && date < DateOnly.FromDateTime(Date.Value))
            tip += "签约日期晚于申请日期";

        if (need && !Contract.Exists)
            tip += " 缺少合同";

        if (need && !RiskDisclosure.Exists)
            tip += " 缺少风险揭示书";

        if (needvideo && !Video.Exists)
            tip += " 缺少双录";


        Tips = tip;
    }


    private void CheckFirstTrade()
    {
        var first = true;
        var professional = false;
        if (SelectedFund is not null && SelectedInvestor is not null)
        {
            using var db = DbHelper.Base();
            first = db.GetCollection<TransferRecord>().Find(x => x.FundId == SelectedFund.Id && x.InvestorId == SelectedInvestor.Id).Sum(x => x.ShareChange()) == 0;
            professional = Qualifications?.Any(x => x.Result == QualifiedInvestorType.Professional) ?? false;
        }

        if (first)
        {
            Contract.IsRequired = true;
            RiskDisclosure.IsRequired = true;
            Video.IsRequired = true;
            OrderFile.IsRequired = !professional;
            Types = [TransferOrderType.FirstTrade];
            SelectedType = TransferOrderType.FirstTrade;
        }
        else
        {
            Contract.IsRequired = false;
            RiskDisclosure.IsRequired = false;
            Video.IsRequired = false;
            OrderFile.IsRequired = true;
            Types = [TransferOrderType.Buy, TransferOrderType.Share, TransferOrderType.Amount, TransferOrderType.RemainAmout];

            SelectedType = null;
        }
    }
}

public partial class SupplementaryOrderWindowViewModel : AddOrderWindowViewModelBase
{
    public SupplementaryOrderWindowViewModel(TransferRecordViewModel record, bool firstTrade)
    {
        Record = record;

        SelectedType = record.Type switch { TransferRecordType.Redemption or TransferRecordType.ForceRedemption => GetSellType(record), _ => firstTrade ? TransferOrderType.FirstTrade : TransferOrderType.Buy };


        Number = record.RequestAmount > 0 ? record.RequestAmount : record.RequestShare;


        IsSellTypeForzen = SelectedType == TransferOrderType.Share;


        // 检查是否存在已存在
        using var db = DbHelper.Base();
        var order = db.GetCollection<TransferOrder>().FindById(record.OrderId);

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


        if (firstTrade)
        {
            Contract.IsRequired = true;
            RiskDisclosure.IsRequired = true;
        }
        else
        {
            Video.IsRequired = false;
            OrderFile.IsRequired = true;
        }

        if (SelectedType == TransferOrderType.Amount || SelectedType == TransferOrderType.RemainAmout)
            Types = [TransferOrderType.Amount, TransferOrderType.RemainAmout];
        else Types = [TransferOrderType.Share, TransferOrderType.Amount, TransferOrderType.RemainAmout];

        Check();
    }

    public TransferRecordViewModel Record { get; }


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
            //// 如果是新增加的
            //if (Id == 0)
            //{
            //    var obj = new TransferOrder();
            //    db.GetCollection<TransferOrder>().Insert(obj);
            //    Id = obj.Id;

            //    // 移动文件
            //    Contract.Meta = Move(Contract.File);
            //    RiskDisclosure.File = Move(RiskDisclosure.File);
            //    OrderFile.File = Move(OrderFile.File);
            //    Video.File = Move(Video.File);
            //    Review.File = Move(Review.File);
            //    RiskPair.File = Move(RiskPair.File);
            //}


            TransferOrder order = new TransferOrder
            {
                Id = Id,
                Date = DateOnly.FromDateTime(Date ?? default),
                FundId = Record.FundId ?? 0,
                FundName = Record.FundName,
                ShareClass = Record.ShareClass,
                InvestorId = Record.InvestorId ?? 0,
                InvestorIdentity = Record.InvestorIdentity,
                InvestorName = Record.InvestorName,
                Type = SelectedType!.Value,
                Number = Number ?? 0,
                Contract = new SimpleFile { File = Contract.Meta },
                RiskDiscloure = new SimpleFile { File = RiskDisclosure.Meta },
                OrderSheet = new SimpleFile { File = OrderFile.Meta },
                Videotape = new SimpleFile { File = Video.Meta },
                RiskPair = new SimpleFile { File = RiskPair.Meta },
                Review = new SimpleFile { File = Review.Meta },
            };


            db.GetCollection<TransferOrder>().Upsert(order);
            Id = order.Id;

            List<ManualLinkOrder> link = [];

            // 同日订单
            if (MergeOrderBySameDay)
            {
                var same = db.GetCollection<TransferRecord>().Find(x => x.ConfirmedDate == Record.ConfirmedDate && x.FundId == Record.FundId && x.InvestorId == Record.InvestorId).ToArray();
                foreach (var item in same)
                {
                    item.OrderId = Id;
                    if(db.GetCollection<TransferRequest>().FindById(item.RequestId) is TransferRequest request)
                    {
                        request.OrderId = Id;
                        db.GetCollection<TransferRequest>().Upsert(request);
                    }
                }
                db.GetCollection<TransferRecord>().Update(same);
                foreach (var item in same)
                    link.Add(new ManualLinkOrder(item.Id, Id, item.ExternalId!, item.ExternalRequestId!));
                //DataTracker.LinkOrder(same);
            }
            else
            {
                var rec = db.GetCollection<TransferRecord>().FindById(Record.Id);
                rec.OrderId = Id;
                db.GetCollection<TransferRecord>().Update(rec);

                if (db.GetCollection<TransferRequest>().FindById(rec.RequestId) is TransferRequest request)
                {
                    request.OrderId = Id;
                    db.GetCollection<TransferRequest>().Upsert(request);
                }

                link.Add(new ManualLinkOrder(Record.Id, Id, Record.ExternalId!, Record.ExternalRequestId!));
                //DataTracker.LinkOrder(rec);
            }


            db.Commit();

            WeakReferenceMessenger.Default.Send(order);
            WeakReferenceMessenger.Default.Send(link);
        }
        catch (Exception e)
        {
            db.Rollback();
            Log.Error($"添加交易订单失败，{e}");
        }
    }


    partial void OnMergeOrderBySameDayChanged(bool value)
    {
        if (value && SameDay is null)
        {
            using var db = DbHelper.Base();
            SameDay = db.GetCollection<TransferRecord>().Find(x => x.ConfirmedDate == Record.ConfirmedDate && x.FundId == Record.FundId && x.InvestorId == Record.InvestorId).ToArray();
        }

        if (!value)
            Number = Record.RequestAmount > 0 ? Record.RequestAmount : Record.RequestShare;
        else if (SameDay is not null)
            Number = SameDay.Sum(x => x.RequestAmount) switch { 0 => SameDay.Sum(x => x.RequestShare), var n => n };


    }

    private TransferOrderType GetSellType(TransferRecordViewModel record)
    {
        if (record.RequestAmount is null || record.RequestAmount == 0)
            return TransferOrderType.Share;
        else if (record.RequestAmount is not null && record.ConfirmedNetAmount is not null && Math.Abs(record.RequestAmount.Value - record.ConfirmedNetAmount.Value) < 10)
            return TransferOrderType.Amount;
        else return TransferOrderType.RemainAmout;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(Date):
                Check();
                break;
        }

        if (e.PropertyName == nameof(Date) && Date is DateTime dt)
        {
            var d = DateOnly.FromDateTime(dt);

            DateMayNotGood = (d > Record.RequestDate || Record.RequestDate?.DayNumber - d.DayNumber > 5);
        }

    }

    protected override void Check()
    {
        var tip = "";

        // 判断是否是首次
        bool need = false;
        bool needvideo = false;
        if (SelectedType == TransferOrderType.Buy || SelectedType == TransferOrderType.FirstTrade)
        {
            using var db = DbHelper.Base();
            var early = db.GetCollection<TransferRecord>().Find(x => x.FundId == Record.FundId && x.InvestorId == Record.InvestorId).Min(x => x.RequestDate);
            if (early == Record.RequestDate) need = true;

            var q = db.GetCollection<InvestorQualification>().Find(x => x.InvestorId == Record.InvestorId).Where(x => x.Date <= Record.RequestDate).OrderBy(x => x.Date).LastOrDefault();
            if (q is null || q.Result == QualifiedInvestorType.Normal) needvideo = true;
        }

        IsVideoNesscessary = needvideo;
        Video.IsRequired = needvideo;

        if (Date is not null && Record.RequestDate < DateOnly.FromDateTime(Date.Value))
            tip += "签约日期晚于申请日期";
        else if (DateMayNotGood)
            tip += "签约日期可能不合适";

        if (need && !Contract.Exists)
            tip += " 缺少合同";

        if (need && !RiskDisclosure.Exists)
            tip += " 缺少风险揭示书";

        if (need && needvideo && !Video.Exists)
            tip += " 缺少双录";


        Tips = tip;
    }

    internal void OnBatchFile(string[]? v)
    {
        if (v is null) return;

        if (v.Length == 1 && Path.GetExtension(v[0]) switch { "zip" or "gzip" or "rar" or "7z" => true, _ => false })
        {
            using var fs = new FileStream(v[0], FileMode.Open);
            using ZipArchive archive = new ZipArchive(fs);

            var file = archive.Entries.Where(x => x.Name.Contains("基金合同"));


        }
    }

}


public partial class OrderFileViewModel : SimpleFileViewModel
{
    [ObservableProperty]
    public partial bool IsRequired { get; set; }
}