using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; 
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using System.Collections.ObjectModel;
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


[EntityModifiable(typeof(Institution))]
public partial class InstitutionWindowViewModel : ObservableObject
{

    public int Id { get; }

    private Institution _org;

    public ModifiableViewModel<BooleanDate> ExpireDate { get; }


    public ModifiableViewModel<IdentityViewMdoel> Identity { get; private set; } = null!;


    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;

    public IDType[] IDTypes { get; } = [Models.IDType.UnifiedSocialCreditCode, Models.IDType.OrganizationCode, Models.IDType.BusinessLicenseNumber, Models.IDType.RegistrationNumber, Models.IDType.Other];
           

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
        _org = (db.GetCollection<IEntity>().FindById(id) as Institution)!;
        if (_org is null) throw new Exception();

        Id = _org.Id;

        ExpireDate = new() { NewValue = new(_org.ExpireDate), OldValue = new(_org.ExpireDate) };
        ExpireDate.Changed += (s, e) => { _org.ExpireDate = DateOnly.FromDateTime(ExpireDate.NewValue?.Date ?? default); OnEntityChanged(); };
        FillBy(_org);

        var rel = db.GetCollection<Ownership>().Find(x => x.InstitutionId == id).ToArray();
        var entities = db.GetCollection<IEntity>().FindAll().ToArray();
        var relations = rel.Select(x => new RelationViewModel
        {
            Id = x.Id,
            Holder = entities.FirstOrDefault(y => y.Id == x.HolderId),
            Institution = entities.Select(x => x as Institution).FirstOrDefault(y => y?.Id == x.InstitutionId)!,
            Share = x.Share,
            Ratio = _org.RegisterCapital == 0 ? 0 : x.Share / _org!.RegisterCapital
        }).ToArray();

        ShareRelations = [.. relations];



        ShowFileList = !string.IsNullOrWhiteSpace(Identity.OldValue!.Id);
        Identity.Changed += (s, e) =>
        {

            ShowFileList = !string.IsNullOrWhiteSpace(Identity.OldValue!.Id);
            UpdateFiles();

        };



        UpdateFiles();
    }

    private void UpdateFiles()
    {
        var id = Identity.OldValue!.Id;
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



    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<IEntity>().Upsert(_org);
    }

}