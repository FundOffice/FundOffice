using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace FMO;

/// <summary>
/// 股票户
/// </summary>
public partial class StockAccountViewModel : ObservableObject
{
    private readonly StockAccount _account;

    public StockAccountViewModel(StockAccount v)
    {
        Company = v.Company;
        Id = v.Id;
        Group = v.Group;
        IsClosed = v.IsClosed;

        using var db = DbHelper.Base();
        var cars = db.GetCollection<SecurityCardLink>().Find(x => x.Account == Id).ToArray();

        var sh = cars.LastOrDefault(x => x.Type == SecurityCardType.ShangHai);
        if (!sh?.Detatch ?? true)
            SHCard = sh?.Card;
        SHCardConnected = !sh?.Detatch ?? false;

        var sz = cars.LastOrDefault(x => x.Type == SecurityCardType.ShenZhen);
        if (!sz?.Detatch ?? true)
            SZCard = sz?.Card;
        SZCardConnected = !sz?.Detatch ?? false;

        //Common = new(v.Id, v.Common);
        //if (v.Credit is not null)
        //    Credit = new(v.Id, v.Credit);

        var events = db.GetCollection<AccountEvent>().Find(x => x.AccountId == v.Id).ToList();


        Events = [.. events.Select(x => Transfer(v.Id, x))];
        this._account = v;
    }

    public int Id { get; set; }

    public string? Company { get; set; }

      
    [ObservableProperty]
    public partial bool IsClosed { get; set; }

    [ObservableProperty] public partial bool ShowGroupPop { get; set; }

    [ObservableProperty] public partial int Group { get; set; }

    [ObservableProperty]
    public partial SolidColorBrush? GroupBrush { get; set; }

    [ObservableProperty]
    public partial string? SHCard { get; set; }

    [ObservableProperty]
    public partial bool SHCardConnected { get; set; }

    [ObservableProperty]
    public partial string? SZCard { get; set; }

    [ObservableProperty]
    public partial bool SZCardConnected { get; set; }


    public SecurityCardViewModel? NewSHCard { get; set; }
    public SecurityCardViewModel? NewSZCard { get; set; }


    public ObservableCollection<AccountEventViewModel> Events { get; }


    partial void OnGroupChanged(int value)
    {
        using var db = DbHelper.Base();
        var obj = db.GetCollection<StockAccount>().FindById(Id);
        obj.Group = value;
        db.GetCollection<StockAccount>().Update(obj);
        ShowGroupPop = false;
    }

    private AccountEventViewModel Transfer(int id, AccountEvent e)
    {
        return e switch
        {
            AccountCredentialEvent v => new AccountCredentialEventViewModel(v),
            OpenAccountEvent v => new BasicAccountViewModel(v),
            _ => throw new NotImplementedException(),
        };
    }


    [RelayCommand]
    public void SetGroup()
    {
        ShowGroupPop = true;
    }

    [RelayCommand]
    public void ConfirmSHCard()
    {
        if (NewSHCard is not null)
        {
            SHCard = NewSHCard.CardNo;
            SHCardConnected = true;

            using var db = DbHelper.Base();
            db.GetCollection<SecurityCardLink>().Insert(new SecurityCardLink(0, SecurityCardType.ShangHai, SHCard!, Id));
        }
    }

    [RelayCommand]
    public void DisconnectSH()
    {
        using var db = DbHelper.Base();
        db.GetCollection<SecurityCardLink>().Insert(new SecurityCardLink(0, SecurityCardType.ShangHai, SHCard!, Id, true));
        SHCard = null;
        SHCardConnected = false;
    }

    [RelayCommand]
    public void ConfirmSZCard()
    {
        if (NewSZCard is not null)
        {
            SZCard = NewSZCard.CardNo;
            SZCardConnected = true;

            using var db = DbHelper.Base();
            db.GetCollection<SecurityCardLink>().Insert(new SecurityCardLink(0, SecurityCardType.ShenZhen, SZCard!, Id));
        }
    }

    [RelayCommand]
    public void DisconnectSZ()
    {
        using var db = DbHelper.Base();
        db.GetCollection<SecurityCardLink>().Insert(new SecurityCardLink(0, SecurityCardType.ShenZhen, SZCard!, Id, true));
        SZCard = null;
        SZCardConnected = false;
    }

    [RelayCommand]
    public void AddCredit()
    {
        if (Events.Any(x => x.Name == "信用账户")) return;

        var ev = new OpenAccountEvent { AccountId = Id, AccountType = nameof(StockAccount), Name = "信用账户" };
        using var db = DbHelper.Base();
        db.GetCollection<AccountEvent>().Insert(ev);
        Events.Add(Transfer(Id, ev));
        //Credit = new(Id, new OpenAccountEvent { AccountId = 0, AccountType = nameof(StockAccount), Name = "信用账户" });
    }
 
    [RelayCommand]
    public void DeleteEvent(AccountEventViewModel ev)
    {
        if (ev is null || ev.Name == "基本账户") return;

        if (HandyControl.Controls.MessageBox.Ask($"确认删除 {ev.Name} 吗") == MessageBoxResult.Cancel)
            return;

        Events.Remove(ev);

        using var db = DbHelper.Base();
        db.GetCollection<AccountEvent>().Delete(ev.Id);
    }


    [RelayCommand]
    public void AddQMT()
    {
        if (Events.Any(x => x.Name == "QMT")) return;

        AccountCredentialEvent ev = new() { AccountType = nameof(StockAccount), AccountId = Id, Name = "QMT" };
        Events.Add(new AccountCredentialEventViewModel(ev));
        using var db = DbHelper.Base();
        db.GetCollection<AccountEvent>().Insert(ev);
        var sa = db.GetCollection<TradingAccoutOfFund>().FindById(Id) as StockAccount;


        if (sa!.Events is null)
            sa.Events = [ev];
        else sa.Events.Add(ev);
        db.GetCollection<TradingAccoutOfFund>().Update(sa);
    }

    [RelayCommand]
    public void Close()
    {
        using var db = DbHelper.Base();
        if (db.GetCollection<TradingAccoutOfFund>().FindById(Id) is StockAccount a)
        {
            a.IsClosed = IsClosed;
            db.GetCollection<TradingAccoutOfFund>().Update(a);
        }
    }
}


public partial class AccountEventModifiableViewModel<TValue, TViewModel> : ModifiableViewModel<TValue, TViewModel>, IValueModifier<AccountEvent> where TValue : AccountEvent where TViewModel : IViewModel<TValue, TViewModel>
{
    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;

    AccountEvent? IValueModifier<AccountEvent>.OldValue { get => this.OldValue; set => this.OldValue = (TValue?)value; }
}

public abstract partial class AccountEventViewModel : ObservableObject//, IViewModel<AccountEvent, AccountEventViewModel>
{
    protected AccountEventViewModel(AccountEvent ae)
    {
        Id = ae.Id;
        Name = ae.Name;
        AccountId = ae.AccountId;
        AccountType = ae.AccountType;
    }

    public int Id { get; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;


    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }


    public int AccountId { get; set; }

    public string AccountType { get; set; }




    [RelayCommand]
    public void OpenRawFolder()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        var dir = AccountType switch
        {
            nameof(StockAccount) => "stock",
            nameof(FutureAccount) => "future",
            _ => "other"
        };

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "files", "accounts", dir, AccountId.ToString(), Name, "原始文件");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
    }


    [RelayCommand]
    public void OpenSealFolder()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        var dir = AccountType switch
        {
            nameof(StockAccount) => "stock",
            nameof(FutureAccount) => "future",
            _ => "other"
        };

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "files", "accounts", dir, AccountId.ToString(), Name, "用印文件");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
    }


    partial void OnIsExpandedChanged(bool value)
    {
        if (!value) IsReadOnly = true;
    }

}


/// <summary>
/// 带有账号的Event
/// </summary>
[EntityModifiable(typeof(AccountCredentialEvent))]
public partial class AccountCredentialEventViewModel : AccountEventViewModel
{
    private readonly AccountCredentialEvent _event;

    public AccountCredentialEventViewModel(AccountCredentialEvent val) : base(val)
    {

        FillBy(val);
        _event = val;
    }




    [RelayCommand]
    public void Save()
    {
        using var db = DbHelper.Base();


        IsReadOnly = true;
    }


    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<AccountEvent>().Update(_event);
    }




}


[EntityModifiable(typeof(OpenAccountEvent))]
public partial class BasicAccountViewModel : AccountEventViewModel
{
    private readonly OpenAccountEvent _event;

    public BasicAccountViewModel(OpenAccountEvent val) : base(val)
    {
        FillBy(val);


        BankLetter = new(val.BankLetter);
        BankLetter.FileChanged += f => UpdateFile($"{{ \"BankLetter\" : {BsonMapper.Global.ToDocument(f)} }}");


        ServiceAgreement = new(val.ServiceAgreement);
        ServiceAgreement.FileChanged += f => UpdateFile($"{{ \"ServiceAgreement\" : {BsonMapper.Global.ToDocument(f)} }}");
        _event = val;
    }


    private void UpdateFile(string expr)
    {

        if (Id == 0 || expr is null) return; // 新建时不保存

        using var db = DbHelper.Base();
        db.GetCollection<AccountEvent>().UpdateMany(expr, $"_id={Id}");

    }

     
    /// <summary>
    /// 银证、银期等
    /// </summary>
    public SimpleFileViewModel? BankLetter { get; }



    public SimpleFileViewModel? ServiceAgreement { get; }


    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<AccountEvent>().Update(_event);
    }

    //[RelayCommand]
    //public void Save()
    //{
    //    using var db = DbHelper.Base();
    //    var obj = db.GetCollection<StockAccount>().FindById(Id);

    //    if (Name == obj.Common?.Name)
    //    {
    //        obj.Common!.Account = Account;
    //        obj.Common!.TradePassword = TradePassword;
    //        obj.Common!.CapitalPassword = CapitalPassword;

    //        db.GetCollection<StockAccount>().Update(obj);
    //    }
    //    else if (Name == "信用账户")
    //    {
    //        if (obj.Credit is null)
    //            obj.Credit = new OpenAccountEvent { AccountId = 0, AccountType = nameof(StockAccount), Name = "信用账户" };

    //        obj.Credit.Account = Account;
    //        obj.Credit.TradePassword = TradePassword;
    //        obj.Credit.CapitalPassword = CapitalPassword;

    //        db.GetCollection<StockAccount>().Update(obj);
    //    }

    //    IsReadOnly = true;
    //}





}
