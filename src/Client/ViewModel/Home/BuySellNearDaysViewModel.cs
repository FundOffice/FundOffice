using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace FMO;

/// <summary>
///    <DataGridTextColumn Binding="{Binding Id}" Header="Id" />
///   <DataGridTextColumn Binding = "{Binding RequestDate, StringFormat=yyyy-MM-dd}" Header="日期" />
///   <DataGridTextColumn Binding = "{Binding FundName}" Header="基金" />
///   <DataGridTextColumn Binding = "{Binding InvestorName}" Header="投资人" />
///   <DataGridTextColumn Binding = "{Binding RequestType}" Header="类型" />
///   <DataGridTextColumn Binding = "{Binding RequestAmount}" Header="金额" />
///   <DataGridTextColumn Binding = "{Binding RequestShare}" Header="份额" />
/// </summary>
public partial class BuySellNearDaysViewModel
{
    public partial class TransferRequestViewModel : ObservableObject
    {
        public TransferRequestViewModel(TransferRequest r)
        {
            this.UpdateFrom(r);
        }

        public int Id { get; set; }

        public DateOnly RequestDate { get; set; }

        public int FundId { get; set; }

        public string? FundName { get; set; }

        public string? ShareClass { get; set; }

        public int InvestorId { get; set; }

        public string? InvestorName { get; set; }

        public TransferRequestType RequestType { get; set; }

        public decimal RequestShare { get; set; }

        public decimal RequestAmount { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSettled))]
        public partial RaisingBankTransaction? Transaction { get; set; }

        /// <summary>
        /// 认申购，是否收到款
        /// 赎回，是否交收
        /// </summary> 
        public bool IsSettled => Transaction is not null;


    }
}

/// <summary>
/// 近日内的申赎
/// </summary>
public partial class BuySellNearDaysViewModel : ObservableObject, IRecipient<IList<TransferRequest>>, IRecipient<NewDay>, IRecipient<IList<RaisingBankTransaction>>
{
    public BuySellNearDaysViewModel(IEnumerable<TransferRequest> requests)
    {

        WeakReferenceMessenger.Default.RegisterAll(this);

        Recent = [.. requests.Where(x=>x.RequiredOrder()).Select(x => new TransferRequestViewModel(x))];

        if (requests.Any())
        {
            using var db = DbHelper.Base();
            var min = new DateTime(requests.Min(x => x.RequestDate), default);
            var trans = db.GetCollection<RaisingBankTransaction>().Find(x => x.Time > min).ToList();

            Task.Run(() => CheckIsSettled(trans));
        }

        BuySource.Filter += (s, e) => e.Accepted = e.Item switch { TransferRequestViewModel r => r.RequestType.IsBuy(), _ => false };
        SellSource.Filter += (s, e) => e.Accepted = e.Item switch { TransferRequestViewModel r => r.RequestType.IsSell(), _ => false };
        UnsettledSource.Filter += (s, e) => e.Accepted = e.Item switch { TransferRequestViewModel r => !r.IsSettled, _ => false };
        UnsettledSource.LiveFilteringProperties.Add(nameof(TransferRequestViewModel.IsSettled));
        UnsettledSource.IsLiveFilteringRequested = true;
    }

    private async Task CheckIsSettled(IList<RaisingBankTransaction> trans)
    {
        var list = await App.Current.Dispatcher.InvokeAsync(() => Recent.ToList());

        // 获取已知账户
        using var db = DbHelper.Base();
        var ids = list.Select(x => x.InvestorId).Distinct().ToList();
        var dic = db.GetCollection<InvestorBankAccount>().Find(x => ids.Contains(x.OwnerId)).GroupBy(x => x.OwnerId).ToDictionary(x => x.Key);

        // 检查有没有对应的流水
        foreach (var req in list.Where(x => x.RequestType.IsBuy()))
        {
            if (trans.FirstOrDefault(x => x.FundId == req.FundId && x.Direction == TransctionDirection.Receive && DateOnly.FromDateTime(x.Time.Date) <= req.RequestDate &&
                    (dic[req.InvestorId].Any(y => y.Number == x.CounterNo) || x.CounterName == req.InvestorName) && x.Amount == req.RequestAmount) is RaisingBankTransaction transaction)
                req.Transaction = transaction;
        }

        foreach (var req in list.Where(x => x.RequestType.IsSell()))
        {
            if (trans.FirstOrDefault(x => x.FundId == req.FundId && x.Direction == TransctionDirection.Pay && DateOnly.FromDateTime(x.Time.Date) > req.RequestDate &&
                    (dic[req.InvestorId].Any(y => y.Number == x.CounterNo) || x.CounterName == req.InvestorName)) is RaisingBankTransaction transaction)
                req.Transaction = transaction;
        }

        //.View.Refresh();
    }



    //public int CancelCount => BuyRequests.Count(x => x.IsCanceled && x.RequestType != TransferRequestType.Abort) + SellRequests.Count(x => x.IsCanceled && x.RequestType != TransferRequestType.Abort);

    // public int TotalCount => BuyRequests.Count + SellRequests.Count;



    [ObservableProperty]
    public partial ObservableCollection<TransferRequestViewModel> Recent { get; set; }

    public CollectionViewSource BuySource { get; } = new();


    public CollectionViewSource SellSource { get; } = new();


    public CollectionViewSource UnsettledSource { get; } = new();


    [ObservableProperty]
    public partial DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);


    partial void OnRecentChanged(ObservableCollection<TransferRequestViewModel> value)
    {
        BuySource.Source = Recent;
        SellSource.Source = Recent;
        UnsettledSource.Source = Recent;
    }


    public void Receive(IList<TransferRequest> message)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var limit = today.AddDays(-7);

        // 删除过期的
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            for (int i = Recent.Count - 1; i >= 0; i--)
            {
                if (Recent[i].RequestDate <= limit)
                    Recent.RemoveAt(i);
            }

            foreach (var item in message.Where(x => x.IsBuy() && x.RequestDate > limit).ExceptBy(Recent.Select(x => x.Id), x => x.Id))
            {
                Recent.Add(new TransferRequestViewModel(item));
            }
        });



    }

    public void Receive(NewDay message)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var limit = today.AddDays(-7);

        App.Current.Dispatcher.BeginInvoke(() =>
        {
            for (int i = Recent.Count - 1; i >= 0; i--)
            {
                if (Recent[i].RequestDate <= limit)
                    Recent.RemoveAt(i);
            }
        });
    }

    /// <summary>
    /// 检验是否交收
    /// </summary>
    /// <param name="message"></param>
    /// <exception cref="NotImplementedException"></exception>
    public void Receive(IList<RaisingBankTransaction> message)
    {
        Task.Run(() => CheckIsSettled(message));
    }
}



public partial class HomePageViewModel
{
    [ObservableProperty]
    public partial BuySellNearDaysViewModel? BuySellNearDaysViewModel { get; set; }

    /// <summary>
    /// 检查交易申请与流水是否匹配
    /// </summary>
    private void DailyCheckRequestIsWell()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var limit = today.AddDays(-7);
        var limitt = new DateTime(limit, default);
        using var db = DbHelper.Base();
        // 7日内的request
        var tq = db.GetCollection<TransferRequest>().Find(x => x.RequestDate > limit).ToArray();

        App.Current.Dispatcher.InvokeAsync(() => BuySellNearDaysViewModel = new(tq));


        //BuySellNearDaysViewModel.BuyCount = BuySellNearDaysViewModel.BuyRequests.Count;
        //BuySellNearDaysViewModel.SellCount = BuySellNearDaysViewModel.SellRequests.Count;

        // 更新数量
        //BuySellNearDaysViewModel.BuyRequests.CollectionChanged += (s, e) => { BuySellNearDaysViewModel.BuyCount = BuySellNearDaysViewModel.BuyRequests.Count; };
        //BuySellNearDaysViewModel.SellRequests.CollectionChanged += (s, e) => { BuySellNearDaysViewModel.SellCount = BuySellNearDaysViewModel.SellRequests.Count; };


        var btrans = db.GetCollection<BankTransaction>().Find(x => x.Time > limitt).ToArray();





        foreach (var item in tq)
        {
        }
    }

}