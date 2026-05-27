using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using MoT;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO;

/// <summary>
/// TransferRecordPage.xaml 的交互逻辑
/// </summary>
public partial class TransferRecordPage : UserControl
{
    public TransferRecordPage()
    {
        InitializeComponent();

        Unloaded += (s, e) => { if (DataContext is IDisposable ob) ob.Dispose(); };
    }


}


public partial class TransferRecordPageViewModel : ObservableObject, IDisposable, IRecipient<IList<TransferRequest>>, IRecipient<TransferRecord>,
    IRecipient<PageTAMessage>, IRecipient<TransferOrder>, IRecipient<TipChangeMessage>, IRecipient<List<ManualLinkOrder>>, IRecipient<IList<TransferOrder>>
{
    [ObservableProperty]
    public partial ObservableCollection<TransferRecordViewModel>? Records { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<TransferOrderViewModel>? Orders { get; set; }

    public CollectionViewSource RecordsSource { get; } = new() ;
    public CollectionViewSource OrderSource { get; } = new();


    public CollectionViewSource RequestsSource { get; } = new();

    public CollectionViewSource TranscationSource { get; } = new();


    [ObservableProperty]
    public partial string? SearchKeyword { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecordTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsOrderTablSelected))]
    public partial int TabIndex { get; set; } = 4;

    public bool IsRecordTabSelected => TabIndex == 4;

    public bool IsOrderTablSelected => TabIndex == 2;


    [ObservableProperty]
    public partial ObservableCollection<TransferRequestViewModel>? Requests { get; set; }


    [ObservableProperty]
    public partial bool ShowOnlySignable { get; set; }

    /// <summary>
    /// 数据有问题
    /// </summary> 
    public bool DataHasError => (ErrorMessage?.Count ?? 0) > 0;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataHasError))]
    public partial List<string>? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial int LackOrderBuyCount { get; set; }
    [ObservableProperty]
    public partial int LackOrderSellCount { get; set; }

    [ObservableProperty]
    public partial int LackOrderCount2 { get; set; }


    public ObservableCollection<RaisingBankTranscationViewModel>? BankTransactions { get; set; }

    FileSystemWatcher? watcher;
    private bool disposedValue;

    public GridFilter FundNameFilter { get; }


    public GridFilter InvestorNameFilter { get; }


    public GridFilter OrderStatusFilter { get; }













    public TransferRecordPageViewModel()
    {
        ShowOnlySignable = true;
        WeakReferenceMessenger.Default.RegisterAll(this);


        FundNameFilter = new(RequestsSource, OrderSource, RecordsSource);
        InvestorNameFilter = new(RequestsSource, OrderSource, RecordsSource);
        OrderStatusFilter = new(RequestsSource, RecordsSource);

        Task.Run(() =>
        {
            using var db = DbHelper.Base();
            var funds = db.GetCollection<Fund>().FindAll().Select(x => (x.Id, x.Name, x.Code, x.ClearDate)).ToArray();

            //var map = db.GetCollection<TransferMapping>().FindAll().ToList();



            //var mapd = map.ToDictionary(x => x.OrderId, x => x);

            List<string?> list = [DataTracker.GetUniformTip(TipType.TANoOwner), DataTracker.GetUniformTip(TipType.TransferRequestMissing)];
            ErrorMessage = [.. list.OfType<string>()];


            var tr = db.GetCollection<TransferRecord>().Query().OrderByDescending(x => x.RequestDate).Limit(100).ToList();//FindAll().ToList();
            var tr2 = db.GetCollection<TransferRequest>().Query().OrderByDescending(x => x.RequestDate).Limit(50).ToList();//.FindAll().OrderByDescending(x => x.RequestDate).ToList();

            //var t3 = db.GetCollection<TransferOrder>().FindAll().ToList();
            var t3 = db.GetCollection<TransferOrder>().Query().OrderByDescending(x => x.Date).Limit(50).ToList();

            var records = tr.Select(x => new TransferRecordViewModel(x)).ToArray();
            var orders = t3.Select(x => new TransferOrderViewModel(x)).ToArray();
            var requests = tr2.Select(x => new TransferRequestViewModel(x)).ToArray();

            var dic = funds.ToDictionary(x => x.Id, x => x.Name);

            var transaction = db.GetCollection<RaisingBankTransaction>().Query().OrderByDescending(x => x.Time).Limit(50).ToArray().
                                Select(x => new RaisingBankTranscationViewModel(x, dic.TryGetValue(x.FundId, out var tt) ? tt : null)).ToArray();

            var orderEntry = db.GetCollection<TransferRequest>().Query().Where(x => x.OrderId != 0).Select(x => x.OrderId).ToArray();
            foreach (var item in orders.IntersectBy(orderEntry, x => x.Id))
                item.IsApplyed = true;

            var orderConfirm = db.GetCollection<TransferRecord>().Query().Where(x => x.OrderId != 0).Select(x => x.OrderId).ToArray();
            foreach (var item in orders.IntersectBy(orderConfirm, x => x.Id))
                item.IsConfirmed = true;

            ///////////////////////////
            var hasCusIds = tr.Select(x => x.InvestorId).Union(tr2.Select(x => x.InvestorId)).Union(t3.Select(x => x.InvestorId)).Distinct().OrderBy(x => x).ToArray();
            var investors = db.GetCollection<Investor>().Query().Where(Query.In("_id", hasCusIds.Select(x=>new BsonValue(x)))).Select(x => x.Name).ToArray();


            FundNameFilter.Filters = funds.Select(x => new GridFilterItem
            {
                Title = x.Name,
                FilterFunc = y => (y as ITransferViewModel)?.FundName == x.Name,//y switch { ITransferViewModel v => v.FundName == x.Name, _ => true },
                IsSelected = false
            }).ToArray();

            InvestorNameFilter.Filters = investors.OfType<string>().Select(x => new GridFilterItem
            {
                Title = x,
                FilterFunc = y => (y as ITransferViewModel)?.InvestorName == x,//y switch { ITransferViewModel v => v.InvestorName == x, _ => true },
                IsSelected = false
            }).ToArray();


            OrderStatusFilter.Filters = [
                new GridFilterItem{ Title = "缺少认申购订单", FilterFunc = y=>y switch{ IHasOrderViewModel x=> x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.IsBuy(),_=>true } },
                new GridFilterItem{ Title = "缺少赎回订单", FilterFunc = y=>y switch{ IHasOrderViewModel x=> !x.IsLiquidating && x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.IsSell(),_=>true } },
                new GridFilterItem{ Title = "本管理人产品缺少订单", FilterFunc = y=>y switch{ IHasOrderViewModel x=>!x.IsLiquidating && x.IsOrderRequired && x.IsSameManager ,_=>true } },
                new GridFilterItem{ Title = "有订单", FilterFunc = y => y switch{IHasOrderViewModel x=>x.OrderId != 0 ,_=>true }}
                ];


            // 本基金管理人互投 
            var cert = db.GetCollection<Fund>().Query().Select(x => x.Code).ToList();
            if (db.GetCollection<Manager>().Query().First().Identity?.Id is string s)
                cert.Add(s);

            foreach (var item in requests.Where(x => cert.BinarySearch(x.InvestorIdentity) >= 0))
            {
                item.IsSameManager = true;
            }
            foreach (var item in records.Where(x => cert.BinarySearch(x.InvestorIdentity) >= 0))
            {
                item.IsSameManager = true;
            }

            LackOrderBuyCount = db.GetCollection<TransferRequest>().Count(x => x.IsOrderRequired && x.OrderId == 0 && (x.RequestType == TransferRequestType.Purchase || x.RequestType == TransferRequestType.Subscription));
            LackOrderSellCount = db.GetCollection<TransferRequest>().Count(x => x.IsOrderRequired && x.OrderId == 0 && (x.RequestType == TransferRequestType.Redemption || x.RequestType == TransferRequestType.ForceRedemption));

            //LackOrderBuyCount = requests.Count(x => x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.RequestType!.Value.IsBuy());
            //LackOrderSellCount = requests.Count(x => !x.IsLiquidating && x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.RequestType!.Value.IsSell());
            LackOrderCount2 = LackOrderBuyCount + LackOrderSellCount;// requests.Count(x => x.IsOrderRequired && x.LackOrder && x.IsSameManager);


            App.Current.Dispatcher.BeginInvoke(() =>
            {
                Records = [.. records];
                RecordsSource.SortDescriptions.Add(new SortDescription(nameof(TransferRecordViewModel.ConfirmedDate), ListSortDirection.Descending));
                RecordsSource.Source = Records;


                RecordsSource.Filter += (s, e) => e.Accepted = e.Accepted && FilterRecord(e.Item);
            });

            App.Current.Dispatcher.BeginInvoke(() =>
            {
                Requests = [.. requests];
                Orders = [.. orders];
                BankTransactions = [.. transaction];

                RequestsSource.SortDescriptions.Add(new SortDescription(nameof(TransferRequest.RequestDate), ListSortDirection.Descending));
                OrderSource.SortDescriptions.Add(new SortDescription(nameof(TransferOrderViewModel.Date), ListSortDirection.Descending));

                RequestsSource.Source = Requests;
                OrderSource.Source = Orders;

                TranscationSource.Source = BankTransactions;
                TranscationSource.SortDescriptions.Add(new SortDescription(nameof(BankTransaction.Time), ListSortDirection.Descending));


                // RequestsSource.Filter += (s, e) => e.Accepted = e.Accepted && (string.IsNullOrWhiteSpace(SearchKeyword) ? true : SearchPair(e.Item, SearchKeyword));
            });
        });



        try
        {

            // 增加文件监控
            watcher = new FileSystemWatcher("files\\tac");
            watcher.EnableRaisingEvents = true;
            watcher.Created += (s, e) =>
            {
                if (e?.Name is null || Records is null) return;

                var m = Regex.Match(e.Name, @"\d+");
                if (m.Success && int.Parse(m.Value) is int id)
                {
                    Records.FirstOrDefault(x => x.Id == id)?.OnPropertyChanged(nameof(TransferRecordViewModel.FileExists));
                }
            };
            watcher.Renamed += (s, e) =>
            {
                if (e?.Name is null || Records is null) return;

                var m = Regex.Match(e.Name, @"\d+");
                if (m.Success && int.Parse(m.Value) is int id)
                {
                    var v = Records.FirstOrDefault(x => x.Id == id);
                    v?.OnPropertyChanged(nameof(TransferRecordViewModel.FileExists));
                }

                m = Regex.Match(e.OldName!, @"\d+");
                if (m.Success && int.Parse(m.Value) is int id2)
                {
                    var v = Records.FirstOrDefault(x => x.Id == id2);
                    v?.OnPropertyChanged(nameof(TransferRecordViewModel.FileExists));
                }
            };
        }
        catch (Exception e)
        {
            Logg.Error(e);
        }
    }

    private void CheckDataError()
    {
        DataTracker.CheckTAMissOwner();

        List<string?> list = [DataTracker.GetUniformTip(TipType.TANoOwner), DataTracker.GetUniformTip(TipType.TransferRequestMissing)];

        ErrorMessage = [.. list.OfType<string>()];
    }


    private bool FilterRecord(object obj)
    {
        if (obj is not TransferRecordViewModel r || r.Type is null) return false;

        bool show = !ShowOnlySignable || TAHelper.RequiredOrder(r.Type.Value);
        return show && (string.IsNullOrWhiteSpace(SearchKeyword) ? true : SearchPair(obj, SearchKeyword));
    }


    private bool SearchPair(object obj, string key)
    {
        if (obj is TransferRecordViewModel r)
            return (r.InvestorName?.Contains(key) ?? false) || (r.FundName?.Contains(key) ?? false) || (key?.Length > 3 && (r.InvestorIdentity?.Contains(key) ?? false));

        if (obj is TransferRequest rr)
            return (rr.InvestorName?.Contains(key) ?? false) || (rr.FundName?.Contains(key) ?? false) || (key?.Length > 3 && (rr.InvestorIdentity?.Contains(key) ?? false));

        if (obj is TransferOrderViewModel o)
            return (o.InvestorName?.Contains(key) ?? false) || (o.FundName?.Contains(key) ?? false) || (key?.Length > 3 && (o.InvestorIdentity?.Contains(key) ?? false));

        return false;
    }


    partial void OnSearchKeywordChanged(string? value)
    {
        if (RequestsSource.View is null)
            Task.Run(() => App.Current.Dispatcher.BeginInvoke(() => Refresh()));
        else Refresh();
    }


    partial void OnShowOnlySignableChanged(bool value)
    {
        RecordsSource?.View?.Refresh();
    }

    private void Refresh()
    {
        RequestsSource.View?.Refresh();
        RecordsSource.View?.Refresh();
    }

    [RelayCommand]
    public void CalcFee()
    {
        try
        {
            var di = new DirectoryInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName).Parent!;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Path.Combine(di.FullName, "FMO.FeeCalc.exe"), WorkingDirectory = Directory.GetCurrentDirectory() });
        }
        catch (Exception e)
        {

            HandyControl.Controls.Growl.Warning($"无法启动计算器，{e.Message}");
        }
    }


    //[RelayCommand]
    //public void OpenFile(FileStorageInfo file)
    //{
    //    if (file?.Path is null) return;
    //    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file.Path) { UseShellExecute = true }); } catch { }
    //}



    [RelayCommand]
    public void OpenConfirmFile(FileInfo fi)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fi.FullName) { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    public void DeleteRecord(TransferRecordViewModel r)
    {
        using var db = DbHelper.Base();
        db.GetCollection<TransferRecord>().Delete(r.Id);

        Records?.Remove(r);

        DataTracker.OnDeleteTransferRecord(r.Id);
        //if (r?.FundId is not null)
        //    DataTracker.CheckShareIsPair(r.FundId.Value);
    }





    [RelayCommand]
    public void DeleteRequest(TransferRequestViewModel r)
    {
        using var db = DbHelper.Base();
        db.GetCollection<TransferRequest>().Delete(r.Id);

        Requests?.Remove(r);
    }

    [RelayCommand]
    public void AddTARecord()
    {
        switch (TabIndex)
        {
            case 2:
                AddOrder(); break;

            //case 1:
            //    AddRequest(); break;

            case 4:
                AddRecord(); break;
            default:
                break;
        }
    }

    private void AddRecord()
    {
        var wnd = new AddTAWindow();
        wnd.Owner = App.Current.MainWindow;
        if (wnd.ShowDialog() switch { true => false, _ => true })
            return;


        RecordsSource.View.Refresh();

        //if (wnd.DataContext is AddTAWindowViewModel vm && vm.SelectedFund is not null)
        //    DataTracker.CheckShareIsPair(vm.SelectedFund.Id);
    }

    private void AddRequest()
    {
        throw new NotImplementedException();
    }

    private void AddOrder()
    {
        var wnd = new AddOrderWindow();
        wnd.Owner = App.Current.MainWindow;
        var context = new AddOrderWindowViewModel();
        wnd.DataContext = context;
        if (wnd.ShowDialog() switch { true => false, _ => true })
            return;
    }



    [RelayCommand]
    public void DeleteOrder(TransferOrderViewModel order)
    {
        if (HandyControl.Controls.MessageBox.Show("是否确认删除订单？", button: System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
        {
            using var db = DbHelper.Base();
            db.GetCollection<TransferOrder>().Delete(order.Id);

            var rr = db.GetCollection<TransferRecord>().Find(x => x.OrderId == order.Id).ToArray();
            foreach (var item in rr)
                item.OrderId = 0;
            db.GetCollection<TransferRecord>().Update(rr);

            //DataTracker.LinkOrder(rr);
            Orders!.Remove(order);
        }
    }

    [RelayCommand]
    public void AbortOrder(TransferOrderViewModel r)
    {
        using var db = DbHelper.Base();
        var obj = db.GetCollection<TransferOrder>().FindById(r.Id);
        if (obj is not null)
        {
            obj.IsAborted = !obj.IsAborted;
            r.IsAborted = obj.IsAborted;
            db.GetCollection<TransferOrder>().Update(obj);
        }
    }


    [RelayCommand]
    public async Task SyncOrder(TransferOrderViewModel r)
    {
        var signing = ESigning.SigningGalley.FindByIdentifier(r.Source);
        if (signing is null)
        {
            HandyControl.Controls.Growl.Warning("未找到对应的电子签平台");
            return;
        }

        var order = r.Build();
        if (await signing.QueryOrderAsync(order) is ErrorReturn { Successed: false } er)
        {
            HandyControl.Controls.Growl.Warning($"更新订单失败 {er.Error}");
            return;
        }

        using var db = DbHelper.Base();
        db.GetCollection<TransferOrder>().Upsert(order);

        var idx = Orders!.IndexOf(r);
        Orders.RemoveAt(idx);
        Orders.Insert(idx, new TransferOrderViewModel(order));
    }


    [RelayCommand]
    public void TryHandleDataError()
    {
        using var db = DbHelper.Base();
        var err = db.GetCollection<TransferRequest>().Find(x => x.FundId == 0).ToList();
        foreach (var item in err)
        {
            if (db.FindFund(item.FundCode) is Fund fund)
            {
                item.FundId = fund.Id;
                if (Requests?.FirstOrDefault(x => x.Id == item.Id) is TransferRequestViewModel v)
                    v.FundId = item.Id;
            }
        }

        db.GetCollection<TransferRequest>().Update(err);


        var customers = db.GetCollection<Investor>().FindAll().ToList();
        err = db.GetCollection<TransferRequest>().Find(x => x.InvestorId == 0).ToList();
        foreach (var r in err)
        {
            // 此项可能存在重复Id的bug，不用name是因为名字中有（）-等，在不同情景下，全角半角不一样
            var c = customers.FirstOrDefault(x => /*x.Name == r.InvestorName &&*/ x.Identity?.Id == r.InvestorIdentity);
            if (c is null)
            {
                c = new Investor { Name = r.InvestorName, Identity = new Identity { Id = r.InvestorIdentity } };
                db.GetCollection<Investor>().Insert(c);
            }

            // 添加数据 
            r.InvestorId = c.Id;


            if (Records?.FirstOrDefault(x => x.Id == r.Id) is TransferRecordViewModel v)
                v.InvestorId = r.Id;
        }
        db.GetCollection<TransferRequest>().Update(err);

        //////////////////////////////////

        var err2 = db.GetCollection<TransferRecord>().Find(x => x.FundId == 0).ToList();
        foreach (var item in err2)
        {
            if (db.FindFund(item.FundCode) is Fund fund)
            {
                item.FundId = fund.Id;
                if (Records?.FirstOrDefault(x => x.Id == item.Id) is TransferRecordViewModel v)
                    v.FundId = item.Id;
            }
        }

        db.GetCollection<TransferRecord>().Update(err2);


        err2 = db.GetCollection<TransferRecord>().Find(x => x.InvestorId == 0).ToList();
        foreach (var r in err)
        {
            // 此项可能存在重复Id的bug，不用name是因为名字中有（）-等，在不同情景下，全角半角不一样
            var c = customers.FirstOrDefault(x => /*x.Name == r.InvestorName &&*/ x.Identity?.Id == r.InvestorIdentity);
            if (c is null)
            {
                c = new Investor { Name = r.InvestorName, Identity = new Identity { Id = r.InvestorIdentity } };
                db.GetCollection<Investor>().Insert(c);
            }


            // 添加数据 
            r.InvestorId = c.Id;


            if (Records?.FirstOrDefault(x => x.Id == r.Id) is TransferRecordViewModel v)
                v.InvestorId = r.Id;
        }
        db.GetCollection<TransferRecord>().Update(err2);


        CheckDataError();
    }


    [RelayCommand]
    public async Task RebuildTransferRelation()
    {
        await Task.Run(DataTracker.RebuildTARelation);


        if (Orders is null || Requests is null || Records is null) return;

        using var db = DbHelper.Base();


        foreach (var item in Orders.Join(Requests.Where(x => x.OrderId != 0).Select(x => x.OrderId), x => x.Id, x => x, (o, _) => o))
            item.IsApplyed = true;

        foreach (var item in Orders.Join(Records.Where(x => x.OrderId != 0).Select(x => x.OrderId), x => x.Id, x => x, (o, _) => o))
            item.IsConfirmed = true;

        //var map = db.GetCollection<TransferMapping>().FindAll().ToList();


        //if (Orders is not null)
        //    foreach (var item in Orders.Join(map, x => x.Id, x => x.OrderId, (o, m) => new { o, m }))
        //    {
        //        if (item.m.RequestId != 0)
        //            item.o.IsApplyed = true;
        //        if (item.m.RecordId != 0)
        //            item.o.IsConfirmed = true;
        //    }

    }



    [RelayCommand]
    public async Task LoadRaising()
    {
        using var db = DbHelper.Base();
        var data = db.GetCollection<RaisingBankTransaction>().Query().OrderByDescending(x => x.Time).Skip(BankTransactions?.Count ?? 0).Limit(50).ToArray();
        if (data?.Length is null or 0) return;

        var dic = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Name }).ToArray().ToDictionary(x => x.Id, x => x.Name);


        await App.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var item in data)
                BankTransactions!.Add(new(item, dic.TryGetValue(item.FundId, out var fn) ? fn : null));
        });

    }


    [RelayCommand]
    public async Task LoadOrder()
    {
        using var db = DbHelper.Base();


        var data = db.GetCollection<TransferOrder>().Query().OrderByDescending(x => x.Date).Skip(Orders?.Count ?? 0).Limit(50).ToList();
        if (data?.Count is null or 0) return;

        await App.Current.Dispatcher.InvokeAsync(() =>
        {
            var vms = data.Select(x => new TransferOrderViewModel(x)).ToArray();

            var orderEntry = db.GetCollection<TransferRequest>().Query().Where(x => x.OrderId != 0).Select(x => x.OrderId).ToArray();
            foreach (var item in vms.IntersectBy(orderEntry, x => x.Id))
                item.IsApplyed = true;

            var orderConfirm = db.GetCollection<TransferRecord>().Query().Where(x => x.OrderId != 0).Select(x => x.OrderId).ToArray();
            foreach (var item in vms.IntersectBy(orderConfirm, x => x.Id))
                item.IsConfirmed = true;

            foreach (var item in vms)
                Orders?.Add(item);

        });
    }


    [RelayCommand]
    public async Task LoadRequest()
    {
        using var db = DbHelper.Base();

        var tr2 = db.GetCollection<TransferRequest>().Query().OrderByDescending(x => x.RequestDate).Skip(Requests?.Count ?? 0).Limit(50).ToList();

        var requests = tr2.Select(x => new TransferRequestViewModel(x)).ToArray();
        var cert = db.GetCollection<Fund>().Query().Select(x => x.Code).ToList();
        if (db.GetCollection<Manager>().Query().First().Identity?.Id is string s)
            cert.Add(s);

        foreach (var item in requests.Where(x => cert.BinarySearch(x.InvestorIdentity) >= 0))
        {
            item.IsSameManager = true;
        }

        await App.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var item in requests)
                Requests?.Add(item);
        });
    }


    [RelayCommand]
    public async Task LoadRecord()
    {
        using var db = DbHelper.Base();

        var tr = db.GetCollection<TransferRecord>().Query().OrderByDescending(x => x.RequestDate).Skip(Records?.Count ?? 0).Limit(50).ToList();

        var records = tr.Select(x => new TransferRecordViewModel(x)).ToArray();
        var cert = db.GetCollection<Fund>().Query().Select(x => x.Code).ToList();
        if (db.GetCollection<Manager>().Query().First().Identity?.Id is string s)
            cert.Add(s);

        foreach (var item in records.Where(x => cert.BinarySearch(x.InvestorIdentity) >= 0))
        {
            item.IsSameManager = true;
        }

        await App.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var item in records)
                Records?.Add(item);
        });
    }



    public void Receive(TransferRecord message)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            var old = Records!.FirstOrDefault(x => x.Id == message.Id);
            if (old is not null)
                old.UpdateFrom(message);
            else Records!.Add(new TransferRecordViewModel(message));
        });
    }

    public void Receive(TransferRequest message)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            var old = Requests!.FirstOrDefault(x => x.Id == message.Id);
            if (old is not null)
                Requests!.Remove(old);
            Requests!.Add(new(message));
        });
    }

    public void Receive(PageTAMessage message)
    {
        TabIndex = message.TabIndex;
        SearchKeyword = message.Search;
    }

    public void Receive(TransferOrder message)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            var old = Orders!.FirstOrDefault(x => x.Id == message.Id);
            if (old is not null)
                old.UpdateFrom(message);
            else Orders!.Add(new TransferOrderViewModel(message));
        });
    }

    public void Receive(TipChangeMessage message)
    {
        CheckDataError();
    }

    public void Receive(List<ManualLinkOrder> message)
    {
        try
        {
            if (Records is not null)
                foreach (var (confirm, link) in Records.Join(message, x => x.Id, x => x.Id, (confirm, link) => (confirm, link)))
                {
                    confirm.OrderId = link.OrderId;
                    confirm.OnPropertyChanged(nameof(confirm.HasOrder));
                    confirm.OnPropertyChanged(nameof(confirm.LackOrder));
                }

            if (Requests is not null)
                foreach (var (request, link) in Requests.Join(message, x => x.ExternalId, x => x.ExternalRequestId, (request, link) => (request, link)))
                {
                    request.OrderId = link.OrderId;
                    request.OnPropertyChanged(nameof(request.HasOrder));
                    request.OnPropertyChanged(nameof(request.LackOrder));
                }
        }
        catch (Exception e)
        {
            Logg.Error($"void Receive(TransferRecordLinkOrderMessage message) {e}");
        }
    }

    public void Receive(IList<TransferRequest> message)
    {
        // 更新的, 跳过，实际运行，更新没有意义，值应该是一样的
        //App.Current.Dispatcher.InvokeAsync(() =>
        //{
        //    if (Requests is not null)
        //        Requests.IntersectBy(message.Select(x => x.Id), x => x.Id);
        //});

        // 新的
        App.Current.Dispatcher.InvokeAsync(() =>
        {
            // 需要添加的
            var add = Requests is null ? message.Select(x => new TransferRequestViewModel(x)).ToList() : message.ExceptBy(Requests.Select(x => x.Id), x => x.Id).Select(x => new TransferRequestViewModel(x)).ToList();
            if (add.Count == 0) return;

            // 影响order
            if (Orders is not null)
                foreach (var (o, q) in Orders.Join(add, x => x.Id, x => x.OrderId, (order, request) => (order, request)))
                    o.IsApplyed = true;

            // IsSameManager
            using var db = DbHelper.Base();
            var codes = db.GetCollection<Fund>().Query().Select(x => x.Code).ToList(); codes.Sort();
            foreach (var item in add)
            {
                if (codes.BinarySearch(item.InvestorIdentity) >= 0)
                    item.IsSameManager = true;
            }

            // 入列
            if (Requests is null || Requests.Count == 0)
                Requests = [.. add];
            else
                Requests = [.. Requests, .. add];

            RequestsSource.Source = Requests;
        });

    }

    public void Receive(IList<TransferOrder> message)
    {
        // 新的
        App.Current.Dispatcher.InvokeAsync(() =>
        {
            // 需要添加的
            var add = Orders is null ? message.Select(x => new TransferOrderViewModel(x)).ToList() : message.ExceptBy(Orders.Select(x => x.Id), x => x.Id).Select(x => new TransferOrderViewModel(x)).ToList();
            if (add.Count == 0) return;


            // 入列
            if (Orders is null || Orders.Count == 0)
                Orders = [.. add];
            else
                Orders = [.. Orders, .. add];

            OrderSource.Source = Orders;
        });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: 释放托管状态(托管对象)
                watcher?.Dispose();
            }

            // TODO: 释放未托管的资源(未托管的对象)并重写终结器
            // TODO: 将大型字段设置为 null
            disposedValue = true;
        }
    }

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~TransferRecordPageViewModel()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
