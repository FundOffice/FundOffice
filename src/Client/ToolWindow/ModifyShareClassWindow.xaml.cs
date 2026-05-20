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


    [ObservableProperty]
    public partial ObservableCollection<ShareClassViewModel> Shares { get; set; }

    [SetsRequiredMembers]
#pragma warning disable CS9264 // 退出构造函数时，不可为 null 的属性必须包含非 null 值。请考虑添加 ‘required’ 修饰符，或将属性声明为可为 null，或添加 ‘[field: MaybeNull, AllowNull]’ 特性。
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public ModifyShareClassWindowViewModel(int fundId, int flowId, ShareClassViewModel[] shareClassViewModels)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning restore CS9264 // 退出构造函数时，不可为 null 的属性必须包含非 null 值。请考虑添加 ‘required’ 修饰符，或将属性声明为可为 null，或添加 ‘[field: MaybeNull, AllowNull]’ 特性。
    {
        FundId = fundId;
        FlowId = flowId;

        old = [.. shareClassViewModels.Select(x => x.Build())];
        Shares = [.. shareClassViewModels];
    }


    [RelayCommand]
    public void DivideShares()
    {
        ///最大5类
        if (Shares.Count > 5) return;

        using var db = DbHelper.ShareClass();
        if (Shares.Count == 1)
        {
            Shares[0].Name = "A";
            var tmp = new ShareClass { Name = "A", Requirement = Shares[0].Requirement };
            db.GetCollection<ShareClass>().Insert(tmp);
            Shares[0].Id = tmp.Id;
        }
        var sc = new ShareClass { Name = ((char)('A' + Shares.Count)).ToString() };
        db.GetCollection<ShareClass>().Insert(sc);

        Shares.Add(new(sc));
    }

    [RelayCommand]
    public void DeleteShare(ShareClassViewModel s)
    {
        Shares.Remove(s);

        if (Shares.Count == 1)
            Shares[0] = new(ShareClass.DefaultShare);
    }

    [RelayCommand]
    public void ConfirmShares(Window wnd)
    {
        using var db = DbHelper.Base();
        var elements = db.GetCollection<FundElements>().FindById(FundId);

        //// 同步份额相关的要素 
        var rem = Shares.Select(x => x.Build()).ToList();

        // 同名对齐
        var dic = old.ToDictionary(x => x.Name, x => x.Id);
        rem.ForEach(x => x.Id = dic.TryGetValue(x.Name, out var d) ? d : 0);

        var remove = old.ExceptBy(rem.Select(x => x.Id), x => x.Id).Where(x => x.Id != -1).ToArray();
        var add = rem.ExceptBy(old.Select(x => x.Id), x => x.Id).ToArray();
        var change = rem.Where(x => old.Any(y => y.Id == x.Id && (y.Name != x.Name || x.Requirement != y.Requirement))).ToArray();
 

        if ((remove.Length != 0 && MessageBoxResult.Cancel == HandyControl.Controls.MessageBox.Show($"此操作将会删除份额[{string.Join(',', remove.Select(x => x.Name))}]相关的要素", "危险操作提示", MessageBoxButton.OKCancel)))
        {
            Shares = [.. old.Select(x => new ShareClassViewModel(x))];
            return;
        }

        if(add.Length >0 || remove.Length > 0 || change.Length > 0)
        {
            ShareClassChange(remove);
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


    public void InitShare(Mutable<ShareClass[]>? shareClass = null)
    {
        if (shareClass is null)
        {
            using var db = DbHelper.Base();
            shareClass = db.GetCollection<FundElements>().FindById(FundId)?.ShareClasses;
        }

        old = shareClass!.GetValue(FlowId).Value ?? Array.Empty<ShareClass>();

        if (shareClass is not null && shareClass.GetValue(FlowId).Value is ShareClass[] shares)
            Shares = new ObservableCollection<ShareClassViewModel>(shares.Select(x => new ShareClassViewModel { Id = x.Id, Name = x.Name, Requirement = x.Requirement }));
        else
            Shares = new([new ShareClassViewModel { Id = -1, Name = FundElements.SingleShareKey }]);// throw new Exception(); //Shares = new ObservableCollection<ShareClassViewModel>([new ShareClassViewModel { Id = IdGenerator.GetNextId(nameof(ShareClass)), Name = FundElements.SingleShareKey }]);

    }


    private void ShareClassChange(ShareClass[] remove)
    {
        using var db = DbHelper.Base();

        db.GetCollection<IFundFactor>().Upsert(new FundFactor<ShareClass[]>(FactorFields.ShareClasses, FundId, FlowId, Shares.Select(x => x.Build()).ToArray()));

        // 删除相关要素
        db.GetCollection<IFundFactor>().DeleteMany(Query.And(Query.EQ(nameof(FundId), FundId), Query.In(nameof(IFundFactor.ShareId), remove.Where(x => x.Id != -1).Select(x => new BsonValue(x.Id)))));


    }

}