using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static FMO.ManagerPageViewModel;

namespace FMO;

/// <summary>
/// InstitutionWindow.xaml 的交互逻辑
/// </summary>
public partial class InstitutionWindow : Window
{
    public InstitutionWindow()
    {
        InitializeComponent();
    }
}

public partial class InstitutionWindowViewModel : EditableControlViewModelBase<Institution>
{

    /// <summary>
    /// 管理人名称
    /// </summary>  
    public ChangeableViewModel<Institution, string> InstitutionName { get; }

    /// <summary>
    /// 实控人
    /// </summary>
    public ChangeableViewModel<Institution, string> ArtificialPerson { get; }


    /// <summary>
    /// 注册资本
    /// </summary>
    public ChangeableViewModel<Institution, decimal?> RegisterCapital { get; }

    /// <summary>
    /// 实缴
    /// </summary>
    public ChangeableViewModel<Institution, decimal?> RealCapital { get; }

    public ChangeableViewModel<Institution, DateTime?> SetupDate { get; }

    public ChangeableViewModel<Institution, BooleanDate?> ExpireDate { get; }




    /// <summary>
    /// 电话
    /// </summary> 
    public ChangeableViewModel<Institution, string> Telephone { get; }

    /// <summary>
    /// 传真
    /// </summary> 
    public ChangeableViewModel<Institution, string> Fax { get; }

    /// <summary>
    /// 统一信用代码
    /// </summary>  
    public ChangeableViewModel<Institution, string> InstitutionCode { get; }

    /// <summary>
    /// 注册地址
    /// </summary> 
    public ChangeableViewModel<Institution, string> RegisterAddress { get; }



    /// <summary>
    /// 办公地址
    /// </summary> 
    public ChangeableViewModel<Institution, string> OfficeAddress { get; }


    /// <summary>
    /// 经营范围
    /// </summary> 
    public ChangeableViewModel<Institution, string> BusinessScope { get; }


    /// <summary>
    /// 官网
    /// </summary> 
    public ChangeableViewModel<Institution, string> WebSite { get; }



    [ObservableProperty]
    public partial MultiDualFileViewModel? BusinessLicense { get; set; }

    /// <summary>
    /// 营业执照副本
    /// </summary>
    [ObservableProperty]
    public partial MultiDualFileViewModel? BusinessLicense2 { get; set; }

    /// <summary>
    /// 开户许可证
    /// </summary>
    [ObservableProperty]
    public partial MultiDualFileViewModel? AccountOpeningLicense { get; set; }


    /// <summary>
    /// 章程
    /// </summary>
    [ObservableProperty]
    public partial MultiDualFileViewModel? CharterDocument { get; set; }

    /// <summary>
    /// 法人身份证
    /// </summary>
    [ObservableProperty]
    public partial MultiDualFileViewModel? LegalPersonIdCard { get; set; }


    [ObservableProperty]
    public partial bool ShowFileList { get; set; }

    public ObservableCollection<RelationViewModel> ShareRelations { get; }

    /// <summary>
    /// 股权与注册资本不一致
    /// </summary>
    [ObservableProperty]
    public partial bool ShareNotPair { get; set; }


    public InstitutionWindowViewModel(int id)
    {
        using var db = DbHelper.Base();
        var org = db.GetCollection<IEntity>().FindById(id) as Institution;
        if (org is null) throw new Exception();

        Id = org.Id;
        var rel = db.GetCollection<Ownership>().Find(x => x.InstitutionId == id).ToArray();
        var entities = db.GetCollection<IEntity>().FindAll().ToArray();
        var relations = rel.Select(x => new RelationViewModel
        {
            Id = x.Id,
            Holder = entities.FirstOrDefault(y => y.Id == x.HolderId),
            Institution = entities.Select(x => x as Institution).FirstOrDefault(y => y?.Id == x.InstitutionId)!,
            Share = x.Share,
            Ratio = org.RegisterCapital == 0 ? 0 : x.Share / org!.RegisterCapital
        }).ToArray();

        ShareRelations = [.. relations];

        db.Dispose();


        InstitutionName = new ChangeableViewModel<Institution, string>
        {
            Label = "管理人",
            InitFunc = x => x.Name,
            UpdateFunc = (x, y) => x.Name = y ?? "",
            ClearFunc = x => x.Name = string.Empty,
        };
        InstitutionName.Init(org);

        ArtificialPerson = new ChangeableViewModel<Institution, string>
        {
            Label = "实控人",
            InitFunc = x => x.ArtificialPerson,
            UpdateFunc = (x, y) => x.ArtificialPerson = y,
            ClearFunc = x => x.ArtificialPerson = null,
        };
        ArtificialPerson.Init(org);



        RegisterCapital = new ChangeableViewModel<Institution, decimal?>
        {
            Label = "注册资本(万)",
            InitFunc = x => x.RegisterCapital,
            UpdateFunc = (x, y) => x.RegisterCapital = y ?? 0,
            ClearFunc = x => x.RegisterCapital = 0,
            DisplayFunc = x => $"{x}万元"
        };
        RegisterCapital.Init(org);

        RealCapital = new ChangeableViewModel<Institution, decimal?>
        {
            Label = "实缴资本(万)",
            InitFunc = x => x.RealCapital switch { 0 => null, var a => a },
            UpdateFunc = (x, y) => x.RealCapital = y ?? 0,
            ClearFunc = x => x.RealCapital = 0,
            DisplayFunc = x => $"{x}万元"
        };
        RealCapital.Init(org);

        SetupDate = new ChangeableViewModel<Institution, DateTime?>
        {
            Label = "成立日期",
            InitFunc = x => x.SetupDate == default ? null : new DateTime(x.SetupDate, default),
            UpdateFunc = (x, y) => x.SetupDate = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.SetupDate = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        SetupDate.Init(org);


        ExpireDate = new ChangeableViewModel<Institution, BooleanDate?>
        {
            Label = "核销日期",
            InitFunc = x => new BooleanDate { IsLongTerm = x.ExpireDate == DateOnly.MaxValue, Date = x.ExpireDate == default || x.ExpireDate == DateOnly.MaxValue ? null : new DateTime(x.ExpireDate, default) },
            UpdateFunc = (x, y) => x.ExpireDate = y is null || y.Date is null ? default : (y.IsLongTerm ? DateOnly.MaxValue : DateOnly.FromDateTime(y.Date.Value)),
            ClearFunc = x => x.ExpireDate = default,
            DisplayFunc = x => x?.IsLongTerm switch { null => "未设置", true => "长期", _ => x?.Date?.ToString("yyyy-MM-dd") }
        };
        ExpireDate.Init(org);


        Telephone = new ChangeableViewModel<Institution, string>
        {
            Label = "固定电话",
            InitFunc = x => x.Telephone,
            UpdateFunc = (x, y) => x.Telephone = y,
            ClearFunc = x => x.Telephone = null,
        };
        Telephone.Init(org);

        Fax = new ChangeableViewModel<Institution, string>
        {
            Label = "传真",
            InitFunc = x => x.Fax,
            UpdateFunc = (x, y) => x.Fax = y,
            ClearFunc = x => x.Fax = null,
        };
        Fax.Init(org);



        InstitutionCode = new ChangeableViewModel<Institution, string>
        {
            Label = "统一信用代码",
            InitFunc = x => x.Identity?.Id,
            UpdateFunc = (x, y) => x.Identity = new Identity { Id = y!, Type = x.Identity?.Type ?? IDType.UnifiedSocialCreditCode, Other = x.Identity?.Other },
            ClearFunc = x => x.Identity = new Identity { Id = "", Type = x.Identity?.Type ?? IDType.UnifiedSocialCreditCode, Other = x.Identity?.Other }
        };
        InstitutionCode.Init(org);



        RegisterAddress = new ChangeableViewModel<Institution, string>
        {
            Label = "注册地址",
            InitFunc = x => x.RegisterAddress,
            UpdateFunc = (x, y) => x.RegisterAddress = y,
            ClearFunc = x => x.RegisterAddress = null,
        };
        RegisterAddress.Init(org);



        OfficeAddress = new ChangeableViewModel<Institution, string>
        {
            Label = "办公地址",
            InitFunc = x => x.OfficeAddress,
            UpdateFunc = (x, y) => x.OfficeAddress = y,
            ClearFunc = x => x.OfficeAddress = null,
        };
        OfficeAddress.Init(org);



        BusinessScope = new ChangeableViewModel<Institution, string>
        {
            Label = "经营范围",
            InitFunc = x => x.BusinessScope,
            UpdateFunc = (x, y) => x.BusinessScope = y,
            ClearFunc = x => x.BusinessScope = null,
        };
        BusinessScope.Init(org);

        WebSite = new ChangeableViewModel<Institution, string>
        {
            Label = "官网",
            InitFunc = x => x.WebSite,
            UpdateFunc = (x, y) => x.WebSite = y,
            ClearFunc = x => x.WebSite = null,
        };
        WebSite.Init(org);


        ShowFileList = !string.IsNullOrWhiteSpace(InstitutionCode.OldValue);
        InstitutionCode.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(InstitutionCode.OldValue))
            {
                ShowFileList = !string.IsNullOrWhiteSpace(InstitutionCode.OldValue);
                UpdateFiles();
            }
        };





        UpdateFiles();
    }

    private void UpdateFiles()
    {
        var id = InstitutionCode.OldValue;
        if (string.IsNullOrWhiteSpace(id)) return;

        using var db = DbHelper.Base();
        var cef = db.GetCollection<InstitutionCertifications>().FindById(id);
        if (cef is null)
        {
            cef = new() { Id = id };
            db.GetCollection<InstitutionCertifications>().Insert(cef);
        }

        BusinessLicense = new(cef.BusinessLicense);
        BusinessLicense.FileChanged += (x) => UpdateCerf(new { BusinessLicense = x }, cef.Id);

        BusinessLicense2 = new(cef.BusinessLicense2);
        BusinessLicense2.FileChanged += (x) => UpdateCerf(new { BusinessLicense2 = x }, cef.Id);

        AccountOpeningLicense = new(cef.AccountOpeningLicense);
        AccountOpeningLicense.FileChanged += (x) => UpdateCerf(new { AccountOpeningLicense = x }, cef.Id);

        CharterDocument = new(cef.CharterDocument);
        CharterDocument.FileChanged += (x) => UpdateCerf(new { CharterDocument = x }, cef.Id);

        LegalPersonIdCard = new(cef.LegalPersonIdCard);
        LegalPersonIdCard.FileChanged += (x) => UpdateCerf(new { LegalPersonIdCard = x }, cef.Id);

        //BusinessLicense.Files = [.. (cef.BusinessLicense ?? new())];
        //BusinessLicense2.Files = [.. (cef.BusinessLicense2 ?? new())];
        //AccountOpeningLicense.Files = [.. (cef.AccountOpeningLicense ?? new())];
        //CharterDocument.Files = [.. (cef.CharterDocument ?? new())];
        //LegalPersonIdCard.Files = [.. (cef.LegalPersonIdCard ?? new())];

    }

    private void UpdateCerf<T1, T2>(T1 doc, T2 id)
    {
        using var db = DbHelper.Base();
        db.GetCollection<InstitutionCertifications>().UpdateMany(BsonMapper.Global.ToDocument(doc).ToString(), $"_id={new BsonValue(id)}");
    }


  



    [RelayCommand]
    public void AddShareHolder()
    {
        using var db = DbHelper.Base();
        var manager = (db.GetCollection<IEntity>().FindById(Id) as Institution)!;

        var wnd = new AddOrModifyShareHolderWindow();
        wnd.DataContext = new AddOrModifyShareHolderWindowViewModel(manager);
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();

        var rel = db.GetCollection<Ownership>().Find(x => x.InstitutionId == Id).ToArray();
        var entities = db.GetCollection<IEntity>().FindAll().ToArray();
        foreach (var x in rel.ExceptBy(ShareRelations.Select(x => x.Holder?.Id), x => x.HolderId))
        {
            ShareRelations.Add(new RelationViewModel
            {
                Id = x.Id,
                Holder = entities.FirstOrDefault(y => y.Id == x.HolderId),
                Institution = manager,
                Share = x.Share,
                Ratio = manager!.RegisterCapital != 0 ? x.Share / manager!.RegisterCapital : 0
            });
        }


        ShareNotPair = ShareRelations.Sum(x => x.Share) != manager.RegisterCapital;

    }


    [RelayCommand]
    public void RemoveShareHolder(RelationViewModel value)
    {
        if (HandyControl.Controls.MessageBox.Show($"是否确认删除 {value.Holder!.Name}？", button: MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            using var db = DbHelper.Base();
            db.GetCollection<Ownership>().Delete(value.Id);

            ShareRelations.Remove(value);

            ShareNotPair = ShareRelations.Sum(x => x.Share) != RegisterCapital.OldValue;
        }
    }

    [RelayCommand]
    public void EditShareHolder(RelationViewModel value)
    {
        using var db = DbHelper.Base();
        var manager = db.GetCollection<Manager>().FindById(1);
        var wnd = new AddOrModifyShareHolderWindow();
        AddOrModifyShareHolderWindowViewModel obj = new(manager);
        obj.Holder = obj.Entities.FirstOrDefault(x => x.Id == value.Holder!.Id);
        obj.HolderName = value.Holder!.Name;
        obj.Institution = value.Institution;
        obj.ShareAmount = value.Share;
        wnd.DataContext = obj;
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();

    }




    public override Institution? EntityOverride(ILiteDatabase db)
    {
        return db.GetCollection<IEntity>().FindById(Id) as Institution;
    }


    public override void UpdateOverride(ILiteDatabase db, Institution v)
    {
        db.GetCollection<IEntity>().Upsert(v);
    }

    protected override Institution InitNewEntity()
    {
        return new Institution { Name = "" };
    }
}