using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace FMO;

/// <summary>
/// ModifyShareClassWindow.xaml 的交互逻辑
/// </summary>
public partial class ModifyShareClassWindow : Window
{
    public ModifyShareClassWindow()
    {
        InitializeComponent();

    }
}


public partial class ModifyShareClassWindowViewModel : ObservableObject
{
    private ShareClass[] old;

    public int FundId { get; }
    public int FlowId { get; }

    /// <summary>
    /// 初始是单一份额，
    /// </summary>
    public bool IsOriginalSingleton { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ShareClassViewModel> Shares { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmSharesCommand))]
    public partial bool AnythingChanged { get; set; }

    private int newId = 0;

    private bool isInherit;

    private List<ShareClassViewModel> removed = [];

    [SetsRequiredMembers]
    public ModifyShareClassWindowViewModel(int fundId, int flowId, ShareClassViewModel[] shareClassViewModels)
    {
        FundId = fundId;
        FlowId = flowId;

        IsOriginalSingleton = shareClassViewModels.Length <= 1;
        old = [.. shareClassViewModels.Select(x => x.Build())];
        Shares = [.. shareClassViewModels];

        isInherit = Shares.Any(x => x.Id / 1000 < flowId);
        newId = isInherit ? flowId * 1000 + 1 : Shares.Max(x => x.Id) + 1;

        foreach (var item in Shares)
            item.PropertyChanged += (s, e) => AnythingChanged = true;
        Shares.CollectionChanged += (s, e) => AnythingChanged = true;
    }


    [RelayCommand]
    public void DivideShares(ShareClassViewModel? vm)
    {
        ///最大5类
        if (Shares.Count > 5) return;

        using var db = DbHelper.ShareClass();
        if (Shares.Count == 1)
        {
            Shares[0].Name = "A";
            if (isInherit)
            {
                Shares[0].Inherit = Shares[0].Id;
                Shares[0].RealInherit = Shares[0].Id;
                Shares[0].Id = ++newId;
            }
        }

        if (vm is null) vm = Shares[0];


        Shares.Add(new() { FlowId = FlowId, Id = ++newId, Name = GetNextClass(), Inherit = vm.Id, RealInherit = vm.Inherit });
    }

    private string GetNextClass()
    {
        var cnt = Shares.Count;
        var tmp = ((char)('A' + cnt++)).ToString();
        while (Shares.Any(x => x.Name == tmp))
        {
            tmp = ((char)('A' + cnt++)).ToString();
        }
        return tmp;
    }

    [RelayCommand]
    public void DeleteShare(ShareClassViewModel s)
    {
        Shares.Remove(s);
        if (ShareClass.GetFlow(s.Id) == FlowId && old.Any(x => x.Id == s.Id))
            removed.Add(s);

        // 只改名，确认的时候还要改要素
        if (Shares.Count == 1)
            Shares[0].Name = ShareClass.SingletonName;// = new(ShareClass.DefaultShare);
    }

    [RelayCommand(CanExecute = nameof(AnythingChanged))]
    public void ConfirmShares(Window wnd)
    {
        using var db = DbHelper.Base();
        //// 同步份额相关的要素

        // 同名对齐
        //var dic = old.ToDictionary(x => x.Name, x => x.Id);
        //Shares.ForEach(x => x.Id = dic.TryGetValue(x.Name, out var d) ? d : x.Id);

        if ((removed.Count != 0 && MessageBoxResult.Cancel == HandyControl.Controls.MessageBox.Show($"此操作将会删除份额[{string.Join(',', removed.Select(x => x.Name))}]相关的要素", "危险操作提示", MessageBoxButton.OKCancel)))
        {
            Shares = [.. old.Select(x => new ShareClassViewModel(FlowId, x))];
            return;
        }

        // 有变化 
        if (AnythingChanged)
        {
            ShareClassChange();
            WeakReferenceMessenger.Default.Send(new FundShareChangedMessage { FundId = FundId, FlowId = FlowId });

            wnd.DialogResult = true;
        }

        wnd.Close();
    }


    [RelayCommand]
    public void Cancel(Window wnd)
    {
        wnd.DialogResult = false;
        wnd.Close();
    }


    //public void InitShare(Mutable<ShareClass[]>? shareClass = null)
    //{
    //    if (shareClass is null)
    //    {
    //        using var db = DbHelper.Base();
    //        shareClass = db.GetCollection<FundElements>().FindById(FundId)?.ShareClasses;
    //    }

    //    old = shareClass!.GetValue(FlowId).Value ?? [];

    //    if (shareClass is not null && shareClass.GetValue(FlowId).Value is ShareClass[] shares)
    //        Shares = new ObservableCollection<ShareClassViewModel>(shares.Select(x => new ShareClassViewModel { Id = x.Id, Name = x.Name, Requirement = x.Requirement }));
    //    else
    //        Shares = new([new ShareClassViewModel { Id = -1, Name = FundElements.SingleShareKey }]);// throw new Exception(); //Shares = new ObservableCollection<ShareClassViewModel>([new ShareClassViewModel { Id = IdGenerator.GetNextId(nameof(ShareClass)), Name = FundElements.SingleShareKey }]);

    //}


    private void ShareClassChange()
    {
        using var db = DbHelper.Base();

        // 删除相关要素
        if (removed.Count > 0)
            db.GetCollection<IFundFactor>().DeleteMany(Query.And(Query.EQ(nameof(FundId), FundId), /*Query.EQ(nameof(FlowId), FlowId), */Query.In(nameof(IFundFactor.ShareId), removed.Where(x => x.Id != -1).Select(x => new BsonValue(x.Id)))));

        // 同级继承，复制要素 
        foreach (var item in Shares.Where(x => x.IsInheritBrother()))
        {
            var copy = db.GetCollection<IFundFactor>().Find(x => x.FundId == FundId && x.ShareId == item.Inherit).ToArray();
            if (copy.Length == 0) continue;

            foreach (var x in copy)
                x.ShareId = item.Id;
            db.GetCollection<IFundFactor>().Insert(copy);
        }

        var rem = Shares.Select(x => x.Build());
        db.GetCollection<IFundFactor>().Upsert(new FundFactor<ShareClass[]>(FactorFields.ShareClasses, FundId, FlowId, rem.ToArray()));

        //if (rem.Count == 1) // 统一了
        //    db.GetCollection<IFundFactor>().UpdateMany(Query.And(Query.EQ(nameof(FundId), FundId), Query.In(nameof(IFundFactor.ShareId))), $"{{ {nameof(IFundFactor.ShareId)} : -1 }}");

    }

}