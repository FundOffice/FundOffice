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

        Common = new(v.Id, v.Common);
        if (v.Credit is not null)
            Credit = new(v.Id, v.Credit);

        Events = [.. v.Events.Select(x => Transfer(x))];
    }

    public int Id { get; set; }

    public string? Company { get; set; }


    public BasicAccountViewModel Common { get; set; }

    [ObservableProperty]
    public partial BasicAccountViewModel? Credit { get; set; }



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

    private AccountEventViewModel Transfer(AccountEvent e)
    {
        return e switch
        {
            AccountCredentialEvent v => new AccountCredentialEventViewModel(Id, v),
            _ => new AccountEventViewModel(Id, e)
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
        Credit = new(Id, new OpenAccountEvent { Name = "信用账户" });
    }

    [RelayCommand]
    public void DeleteCredit()
    {
        if (HandyControl.Controls.MessageBox.Ask($"确认删除 信用账户 吗") == MessageBoxResult.Cancel)
            return;

        using var db = DbHelper.Base();
        var dd = BsonMapper.Global.ToDocument(new StockAccount { Credit = null }).ToString();
        db.GetCollection<StockAccount>().UpdateMany("{\"Credit\":null}", $"_id={Id}");
        Credit = null;
    }


    [RelayCommand]
    public void AddQMT()
    {
        if (Events.Any(x => x.Name == "QMT")) return;

        AccountCredentialEvent ev = new() { Name = "QMT" };
        Events.Add(new AccountCredentialEventViewModel(Id, ev));
        using var db = DbHelper.Base();
        var sa = db.GetCollection<StockAccount>().FindById(Id);

        if (sa.Events is null)
            sa.Events = [ev];
        else sa.Events.Add(ev);
        db.GetCollection<StockAccount>().Update(sa);
    }

    [RelayCommand]
    public void Close()
    {
        using var db = DbHelper.Base();
        if (db.GetCollection<StockAccount>().FindById(Id) is StockAccount a)
        {
            a.IsClosed = IsClosed;
            db.GetCollection<StockAccount>().Update(a);
        }
    }
}


public partial class AccountEventViewModel : ObservableObject
{
    public int Id { get; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;

    [ObservableProperty]
    public partial string? Name { get; set; }



    public AccountEventViewModel(int id, AccountEvent ev)
    {
        Id = id;
        Name = ev.Name;
    }


    [RelayCommand]
    public void OpenRawFolder()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "files", "accounts", "stock", Id.ToString(), Name, "原始文件");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
    }


    [RelayCommand]
    public void OpenSealFolder()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "files", "accounts", "stock", Id.ToString(), Name, "用印文件");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
    }




}

public partial class AccountCredentialEventViewModel : AccountEventViewModel
{
    /// <summary>
    ///  账号
    /// </summary>
    [ObservableProperty]
    public partial string? Account { get; set; }

    /// <summary>
    ///  密码
    /// </summary>
    [ObservableProperty]
    public partial string? Password { get; set; }

    public AccountCredentialEventViewModel(int id, AccountCredentialEvent ev) : base(id, ev)
    {
        Account = ev.Account;
        Password = ev.Password;
    }
}



public partial class BasicAccountViewModel : ObservableObject
{
    public BasicAccountViewModel(int id, OpenAccountEvent? common)
    {
        Id = id;

        if (common is not null)
        {
            IsReadOnly = true;
            Name = common.Name;
            Account = common.Account;
            TradePassword = common.TradePassword;
            CapitalPassword = common.CapitalPassword;


            BankLetter = new(common.BankLetter);
            BankLetter.FileChanged += f => UpdateFile(new { BankLetter = f });


            ServiceAgreement = new(common.ServiceAgreement);
            ServiceAgreement.FileChanged += f => UpdateFile(new { ServiceAgreement = f });

        }

    }

    private void UpdateFile<T>(T f)
    {
        if (Id == 0) return; // 新建时不保存
        using var db = DbHelper.Base();
        var acc = db.GetCollection<StockAccount>().FindById(Id);
        var eve = Name == acc.Common!.Name ? acc.Common : acc.Credit;
        if(eve is null) return;

        eve!.UpdateFrom(f);
        db.GetCollection<StockAccount>().Update(acc);
        
        //UpdateMany(BsonMapper.Global.ToDocument(f).ToString(), $"_id={Id}");
    }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    /// <summary>
    /// 资金账号
    /// </summary>
    [ObservableProperty]
    public partial string? Account { get; set; }

    /// <summary>
    /// 交易密码
    /// </summary>
    [ObservableProperty]
    public partial string? TradePassword { get; set; }

    /// <summary>
    /// 资金密码
    /// </summary>
    [ObservableProperty]
    public partial string? CapitalPassword { get; set; }

    /// <summary>
    /// 银证、银期等
    /// </summary>
    public SimpleFileViewModel? BankLetter { get; }



    public SimpleFileViewModel? ServiceAgreement { get; }


    public int Id { get; }



    [RelayCommand]
    public void OpenRawFolder()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "files", "accounts", "stock", Id.ToString(), Name, "原始文件");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
    }


    [RelayCommand]
    public void OpenSealFolder()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "files", "accounts", "stock", Id.ToString(), Name, "用印文件");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
    }





    [RelayCommand]
    public void Save()
    {
        using var db = DbHelper.Base();
        var obj = db.GetCollection<StockAccount>().FindById(Id);

        if (Name == obj.Common?.Name)
        {
            obj.Common!.Account = Account;
            obj.Common!.TradePassword = TradePassword;
            obj.Common!.CapitalPassword = CapitalPassword;

            db.GetCollection<StockAccount>().Update(obj);
        }
        else if (Name == "信用账户")
        {
            if (obj.Credit is null)
                obj.Credit = new OpenAccountEvent { Name = "信用账户" };

            obj.Credit.Account = Account;
            obj.Credit.TradePassword = TradePassword;
            obj.Credit.CapitalPassword = CapitalPassword;

            db.GetCollection<StockAccount>().Update(obj);
        }

        IsReadOnly = true;
    }

}
