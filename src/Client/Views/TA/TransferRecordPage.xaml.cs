using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DocumentFormat.OpenXml.Bibliography;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using Serilog;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    }
}


public partial class TransferRecordPageViewModel : ObservableObject, IRecipient<IList<TransferRequest>>, IRecipient<TransferRecord>,
    IRecipient<PageTAMessage>, IRecipient<TransferOrder>, IRecipient<TipChangeMessage>, IRecipient<List<ManualLinkOrder>>, IRecipient<IList<TransferOrder>>
{
    [ObservableProperty]
    public partial ObservableCollection<TransferRecordViewModel>? Records { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<TransferOrderViewModel>? Orders { get; set; }

    public CollectionViewSource RecordsSource { get; } = new();
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

            var tr = db.GetCollection<TransferRecord>().FindAll().ToList();
            var tr2 = db.GetCollection<TransferRequest>().FindAll().OrderByDescending(x => x.RequestDate).ToList();
            var t3 = db.GetCollection<TransferOrder>().FindAll().ToList();
            //var map = db.GetCollection<TransferMapping>().FindAll().ToList();

            FundNameFilter.Filters = funds.Select(x => new GridFilterItem
            {
                Title = x.Name,
                FilterFunc = y => y switch { ITransferViewModel v => v.FundName == x.Name, _ => true },
                IsSelected = false
            }).ToArray();

            InvestorNameFilter.Filters = tr.Select(x => x.InvestorName).Union(t3.Select(x => x.InvestorName)).Distinct().Where(x => x is not null).Select(x => new GridFilterItem
            {
                Title = x,
                FilterFunc = y => y switch { ITransferViewModel v => v.InvestorName == x, _ => true },
                IsSelected = false
            }).ToArray();


            OrderStatusFilter.Filters = [
                new GridFilterItem{ Title = "缺少认申购订单", FilterFunc = y=>y switch{ IHasOrderViewModel x=> x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.IsBuy(),_=>true } },
                new GridFilterItem{ Title = "缺少赎回订单", FilterFunc = y=>y switch{ IHasOrderViewModel x=> !x.IsLiquidating && x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.IsSell(),_=>true } },
                new GridFilterItem{ Title = "本管理人产品缺少订单", FilterFunc = y=>y switch{ IHasOrderViewModel x=>!x.IsLiquidating && x.IsOrderRequired && x.IsSameManager ,_=>true } },
                new GridFilterItem{ Title = "有订单", FilterFunc = y=> y switch{IHasOrderViewModel x=>x.OrderId != 0}}
                ];


            //var mapd = map.ToDictionary(x => x.OrderId, x => x);

            List<string?> list = [DataTracker.GetUniformTip(TipType.TANoOwner), DataTracker.GetUniformTip(TipType.TransferRequestMissing)];
            ErrorMessage = [.. list.Where(x => x is not null)];


            var records = tr.Select(x => new TransferRecordViewModel(x)).ToArray();
            var orders = t3.Select(x => new TransferOrderViewModel(x)).ToArray();
            var requests = tr2.Select(x => new TransferRequestViewModel(x)).ToArray();

            var transaction = db.GetCollection<RaisingBankTransaction>().FindAll().Select(x => new RaisingBankTranscationViewModel(x, funds!)).ToArray();

            //foreach (var item in records.IntersectBy<TransferRecordViewModel, int>(map.Where(x => x.RequestId == 0).Select(x => x.RecordId), x => x.Id))
            //    item.LackRequest = true;


            //foreach (var item in records.Join(map, x => x.Id, x => x.RecordId, (o, m) => new { o, m }))
            //{
            //    item.o.OrderId = item.m.OrderId; 
            //}

            foreach (var item in orders.Join(requests.Where(x => x.OrderId != 0).Select(x => x.OrderId), x => x.Id, x => x, (o, _) => o))
                item.IsApplyed = true;

            foreach (var item in orders.Join(records.Where(x => x.OrderId != 0).Select(x => x.OrderId), x => x.Id, x => x, (o, _) => o))
                item.IsConfirmed = true;


            //foreach (var item in orders.Join(map, x => x.Id, x => x.OrderId, (o, m) => new { o, m }))
            //{
            //    if (item.m.RequestId != 0)
            //        item.o.IsApplyed = true;
            //    if (item.m.RecordId != 0)
            //        item.o.IsConfirmed = true;
            //}



            //foreach (var item in requests.Join(map, x => x.Id, x => x.RequestId, (o, m) => new { o, m }))
            //{
            //    if (item.m.OrderId != 0)
            //        item.o.OrderId = item.m.OrderId;
            //}

            var codes = funds.Select(x => x.Code).Order().ToList();
            foreach (var item in requests.Where(x => codes.Contains(x.InvestorIdentity)))
            {
                item.IsSameManager = true;
            }
            foreach (var item in records.Where(x => codes.Contains(x.InvestorIdentity)))
            {
                item.IsSameManager = true;
            }


            LackOrderBuyCount = requests.Count(x => x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.RequestType!.Value.IsBuy());
            LackOrderSellCount = requests.Count(x => !x.IsLiquidating && x.IsOrderRequired && !x.IsSameManager && x.LackOrder && x.RequestType!.Value.IsSell());
            LackOrderCount2 = requests.Count(x => !x.IsLiquidating && x.IsOrderRequired && x.LackOrder && x.IsSameManager);
            //int lo1 = 0, lo2 = 0, lo3 = 0;
            //foreach (var item in requests.ExceptBy(map.Where(x => x.OrderId != 0 && x.RequestId != 0).Select(x => x.RequestId), x => x.Id))
            //{
            //    if (item.IsOrderRequired)
            //    {
            //        item.LackOrder = true;

            //        // 投资人是管理人自己的产品
            //        item.IsSameManager = codes.BinarySearch(item.InvestorIdentity) >= 0;

            //        if (item.IsSameManager) ++lo3;
            //        else if (item.RequestType!.Value.IsBuy()) ++lo1;
            //        else ++lo2;
            //    }
            //}
            //LackOrderBuyCount = lo1;
            //LackOrderSellCount = lo2;
            //LackOrderCount2 = lo3;



            //foreach (var o in orders)
            //{
            //    if (mapd.TryGetValue(o.Id!.Value, out var m))
            //        o.IsComfirmed = m.RequestId != 0;
            //    //if (tr.Any(x => x.OrderId == o.Id))
            //    //    o.IsComfirmed = true;
            //}


            //foreach (var ft in records.OrderBy(x => x.ConfirmedDate).GroupBy(x => x.FundId))
            //{
            //    var last = ft.Last().ConfirmedDate;
            //    var may = ft.Where(x => x.ConfirmedDate == last && (x.Type == TransferRecordType.Redemption || x.Type == TransferRecordType.ForceRedemption)).ToArray();
            //    if (may.Length == 0) continue;

            //    if (funds.FirstOrDefault(x => x.Id == ft.Key) is var ff && ff.ClearDate != default && last > ff.ClearDate)
            //        foreach (var item in may)
            //            item.IsLiquidating = true;
            //}



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
            Log.Error($"{e}");
        }
    }

    private void CheckDataError()
    {
        DataTracker.CheckTAMissOwner();

        List<string?> list = [DataTracker.GetUniformTip(TipType.TANoOwner), DataTracker.GetUniformTip(TipType.TransferRequestMissing)];

        ErrorMessage = [.. list.Where(x => x is not null)];
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
        if (!await signing.QueryOrderAsync(order))
        {
            HandyControl.Controls.Growl.Warning("未找到对应的订单");
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
            Log.Error($"void Receive(TransferRecordLinkOrderMessage message) {e}");
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
}
