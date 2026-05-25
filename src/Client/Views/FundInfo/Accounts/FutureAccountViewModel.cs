using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace FMO;


public partial class FutureAccountViewModel : ObservableObject
{
    private FutureAccount _account;

    public FutureAccountViewModel(FutureAccount v)
    {
        Company = v.Company;
        Id = v.Id;
        FundId = v.FundId;
        IsClosed = v.IsClosed;

        using var db = DbHelper.Base();
        var events = db.GetCollection<AccountEvent>().Find(x => x.AccountId == v.Id).ToList();


        Events = [.. events.Select(x => Transfer(x))];
        _account = v;
    }

    public int Id { get; set; }

    public int FundId { get; }
    public string? Company { get; set; }


    [ObservableProperty]
    public partial bool IsClosed { get; set; }


    public ObservableCollection<AccountEventViewModel> Events { get; }

    private AccountEventViewModel Transfer(AccountEvent e)
    {
        return e switch
        {
            AccountCredentialEvent v => new AccountCredentialEventViewModel(v),
            OpenAccountEvent v => new FutureOpenAccountViewModel(v),
            _ => throw new NotImplementedException(),
        };
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

}



[EntityModifiable(typeof(OpenAccountEvent))]
public partial class FutureOpenAccountViewModel : AccountEventViewModel
{
    private readonly OpenAccountEvent _event;

    public FutureOpenAccountViewModel(OpenAccountEvent val) : base(val)
    {
        FillBy(val);


        BankLetter = new(val.BankLetter);
        BankLetter.FileChanged += f => UpdateFile($"{{ \"BankLetter\" : {BsonMapper.Global.ToDocument(f)} }}");


        ServiceAgreement = new(val.ServiceAgreement);
        ServiceAgreement.FileChanged += f => UpdateFile($"{{ \"ServiceAgreement\" : {BsonMapper.Global.ToDocument(f)} }}");

        AccountLetter = new(val.AccountLetter);
        AccountLetter.FileChanged += f => UpdateFile($"{{ \"AccountLetter\" : {BsonMapper.Global.ToDocument(f)} }}");
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


    /// <summary>
    /// 账户信息
    /// </summary>
    public SimpleFileViewModel? AccountLetter { get; }

     
    public int FundId { get; }

    public string? Company { get; }

     


    [RelayCommand]
    public void GenerateOpenAccountFiles()
    {
        // 验证数据 
        //1 有托管
        {
            using var db = DbHelper.Base();
            var ele = db.GetCollection<FundElements>().FindById(FundId);
            if (ele is null || ele.TrusteeInfo.Value is null)
            {
                HandyControl.Controls.Growl.Warning("请先【要素】中设置 托管信息");
                return;
            }
            var im = db.GetCollection<FundInvestmentManager>().Find(x => x.FundId == FundId).ToArray();
            if (im.Length == 0)
            {
                HandyControl.Controls.Growl.Warning("请先在【策略】中设置 投资经理");
                return;
            }

            var per = db.GetCollection<Participant>().FindAll().ToArray();
            if (!per.Any(x => x.Role.HasFlag(PersonRole.Agent)) ||
                !per.Any(x => x.Role.HasFlag(PersonRole.OrderPlacer)) ||
                !per.Any(x => x.Role.HasFlag(PersonRole.FundTransferor)) ||
                !per.Any(x => x.Role.HasFlag(PersonRole.ConfirmationPerson)))
            {
                HandyControl.Controls.Growl.Warning("请先在【管理人】 【成员】中设置 开户代理人、指定下单人、资金划转人、结算单确认人等");
                return;
            }




        }



        var wnd = new FutureOpenFilesGeneratorWindow();
        wnd.Owner = App.Current.MainWindow;
        wnd.DataContext = new FutureOpenFilesGeneratorWindowViewModel
        {
            FundId = FundId,
            Company = Company!,
            TemplatePath = @$"files\tpl\{Company}.docx",
            TargetFolder = Path.Combine(Directory.GetCurrentDirectory(), "files", "accounts", "future", Id.ToString(), Name!, "原始文件")
        };
        wnd.ShowDialog();

    }


    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<AccountEvent>().Update(_event);
    }



}
