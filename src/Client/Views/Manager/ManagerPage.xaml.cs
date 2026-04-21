using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.IO.AMAC;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using Microsoft.Win32;
using Serilog;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static FMO.OwnershipMapViewModel;

namespace FMO;

/// <summary>
/// ManagerPage.xaml 的交互逻辑
/// </summary>
public partial class ManagerPage : UserControl
{
    public ManagerPage()
    {
        InitializeComponent();
    }

    private void Grid_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is ManagerPageViewModel vm && e.Data.GetData(DataFormats.FileDrop) is string[] s)
        {
            vm.SetLogo(s[0]);
        }
    }
}


public partial class ManagerPageViewModel : EditableControlViewModelBase<Manager>, IRecipient<ParticipantChangedMessage>
{
    // private int FilesId;




    public string AmacPageUrl { get; set; }

    #region Property
    /// <summary>
    /// 管理人名称
    /// </summary>  
    public ChangeableViewModel<Manager, string> ManagerName { get; }

    /// <summary>
    /// 实控人
    /// </summary>
    public ChangeableViewModel<Manager, string> ArtificialPerson { get; }


    public ChangeableViewModel<Manager, string> RegisterNo { get; }

    /// <summary>
    /// 注册资本
    /// </summary>
    public ChangeableViewModel<Manager, decimal?> RegisterCapital { get; }

    /// <summary>
    /// 实缴
    /// </summary>
    public ChangeableViewModel<Manager, decimal?> RealCapital { get; }

    public ChangeableViewModel<Manager, DateTime?> SetupDate { get; }

    public ChangeableViewModel<Manager, BooleanDate?> ExpireDate { get; }



    public ChangeableViewModel<Manager, DateTime?> RegisterDate { get; }


    /// <summary>
    /// 电话
    /// </summary> 
    public ChangeableViewModel<Manager, string> Telephone { get; }

    /// <summary>
    /// 传真
    /// </summary> 
    public ChangeableViewModel<Manager, string> Fax { get; }


    /// <summary>
    /// 传真
    /// </summary> 
    public ChangeableViewModel<Manager, string> Email { get; }

    /// <summary>
    /// 统一信用代码
    /// </summary>  
    public ChangeableViewModel<Manager, string> InstitutionCode { get; }

    /// <summary>
    /// 注册地址
    /// </summary> 
    public ChangeableViewModel<Manager, string> RegisterAddress { get; }



    /// <summary>
    /// 办公地址
    /// </summary> 
    public ChangeableViewModel<Manager, string> OfficeAddress { get; }


    /// <summary>
    /// 经营范围
    /// </summary> 
    public ChangeableViewModel<Manager, string> BusinessScope { get; }



    /// <summary>
    /// 经营范围
    /// </summary> 
    public ChangeableViewModel<Manager, string> Description { get; }


    /// <summary>
    /// 官网
    /// </summary> 
    public ChangeableViewModel<Manager, string> WebSite { get; }
    #endregion

    [ObservableProperty]
    public partial ImageSource? MainLogo { get; set; }


    [ObservableProperty]
    public partial bool IsPolicyReadOnly { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAddingNewPolicy { get; set; }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddPolicyDocCommand))]
    public partial string? NewPolicyName { get; set; }

    public bool CanAddPolicy => !string.IsNullOrWhiteSpace(NewPolicyName) && PolicyDocuments.All(x => x.Label != NewPolicyName);

    /// <summary>
    /// 营业执照正本
    /// </summary>
    //[ObservableProperty]
    //public partial ObservableCollection<FileInfo>? BusinessLicense { get; set; }


    public MultiDualFileViewModel BusinessLicense { get; }

    /// <summary>
    /// 营业执照副本
    /// </summary>
    public MultiDualFileViewModel BusinessLicense2 { get; }

    /// <summary>
    /// 开户许可证
    /// </summary>
    public MultiDualFileViewModel AccountOpeningLicense { get; }


    /// <summary>
    /// 章程
    /// </summary>
    public MultiDualFileViewModel CharterDocument { get; }

    /// <summary>
    /// 法人身份证
    /// </summary>
    public MultiDualFileViewModel LegalPersonIdCard { get; }


    [ObservableProperty]
    public partial ObservableCollection<ParticipantViewModel> Members { get; set; }


    public ObservableCollection<RelationViewModel> ShareRelations { get; }


    public CollectionViewSource MemberSource { get; } = new();

    [ObservableProperty]
    public partial Participant? SelectedMember { get; set; }

    [ObservableProperty]
    public partial ManagerMemberViewModel? MemberContext { get; set; }

    /// <summary>
    /// 修改成员信息
    /// </summary>
    [ObservableProperty]
    public partial bool ShowMemberPopup { get; set; }

    /// <summary>
    /// 股权与注册资本不一致
    /// </summary>
    [ObservableProperty]
    public partial bool ShareNotPair { get; set; }


    public ObservableCollection<ManagerFlowViewModel> Flows { get; }


    public ObservableCollection<MultiDualFileViewModel> PolicyDocuments { get; }


    public ManagerPageViewModel()
    {
        WeakReferenceMessenger.Default.Register<ParticipantChangedMessage>(this);

        var db = DbHelper.Base();
        Manager manager = db.GetCollection<Manager>().FindById(1)!;
        var id = manager?.Identity?.Id;
        //var mfile = db.GetCollection<InstitutionFiles>().FindOne(x => x.InstitutionId == id);
        //if (mfile is null)
        //{
        //    mfile = new InstitutionFiles { InstitutionId = id };
        //    db.GetCollection<InstitutionFiles>().Insert(mfile);
        //}
        //FilesId = mfile.Id;


        if (db.FileStorage.Exists("icon.main"))
        {
            using var ms = new MemoryStream();
            db.FileStorage.Download("icon.main", ms);
            ms.Seek(0, SeekOrigin.Begin);
            BitmapImage bitmapSource = new BitmapImage();
            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.OnLoad;
            bitmapSource.StreamSource = ms;
            bitmapSource.EndInit();
            MainLogo = bitmapSource;
        }

        Members = new(db.GetCollection<Participant>().FindAll().ToArray().Select(x => new ParticipantViewModel(x))/*.Select(x => new PersonViewModel(x)*/);
        MemberSource.Source = Members;
        //if (manager.LegalAgent is not null)
        //    Members.Add( (manager.LegalAgent));


        var rel = db.GetCollection<Ownership>().Find(x => x.InstitutionId == 0).ToArray();
        var entities = db.GetCollection<IEntity>().FindAll().ToArray();
        var relations = rel.Select(x => new RelationViewModel
        {
            Id = x.Id,
            Holder = entities.FirstOrDefault(y => y.Id == x.HolderId),
            Institution = manager!,
            Share = x.Share,
            Ratio = x.Ratio == 0 ? x.Share / manager!.RegisterCapital : x.Ratio
        }).ToArray();
        ShareRelations = [.. relations.Where(x => x.Institution == manager)];
        ShareNotPair = rel.Sum(x => x.Share) != manager!.RegisterCapital;


        db.Dispose();

        AmacPageUrl = $"https://gs.amac.org.cn/amac-infodisc/res/pof/manager/{manager!.AmacId}.html";

        #region MyRegion
        ManagerName = new ChangeableViewModel<Manager, string>
        {
            Label = "管理人",
            InitFunc = x => x.Name,
            UpdateFunc = (x, y) => x.Name = y ?? "",
            ClearFunc = x => x.Name = string.Empty,
        };
        ManagerName.Init(manager);

        ArtificialPerson = new ChangeableViewModel<Manager, string>
        {
            Label = "实控人",
            InitFunc = x => x.ArtificialPerson,
            UpdateFunc = (x, y) => x.ArtificialPerson = y,
            ClearFunc = x => x.ArtificialPerson = null,
        };
        ArtificialPerson.Init(manager);



        RegisterNo = new ChangeableViewModel<Manager, string>
        {
            Label = "编码",
            InitFunc = x => x.RegisterNo,
            UpdateFunc = (x, y) => throw new Exception(),
            ClearFunc = x => throw new Exception(),
        };
        RegisterNo.Init(manager);


        RegisterCapital = new ChangeableViewModel<Manager, decimal?>
        {
            Label = "注册资本",
            InitFunc = x => x.RegisterCapital,
            UpdateFunc = (x, y) => x.RegisterCapital = y ?? 0,
            ClearFunc = x => x.RegisterCapital = 0,
            DisplayFunc = x => $"{x}万元"
        };
        RegisterCapital.Init(manager);

        RealCapital = new ChangeableViewModel<Manager, decimal?>
        {
            Label = "实缴资本",
            InitFunc = x => x.RealCapital,
            UpdateFunc = (x, y) => x.RealCapital = y ?? 0,
            ClearFunc = x => x.RealCapital = 0,
            DisplayFunc = x => $"{x}万元"
        };
        RealCapital.Init(manager);

        SetupDate = new ChangeableViewModel<Manager, DateTime?>
        {
            Label = "成立日期",
            InitFunc = x => new DateTime(x.SetupDate, default),
            UpdateFunc = (x, y) => x.SetupDate = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.SetupDate = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        SetupDate.Init(manager);


        ExpireDate = new ChangeableViewModel<Manager, BooleanDate?>
        {
            Label = "核销日期",
            InitFunc = x => new BooleanDate { IsLongTerm = x.ExpireDate == DateOnly.MaxValue, Date = x.ExpireDate == default || x.ExpireDate == DateOnly.MaxValue ? null : new DateTime(x.ExpireDate, default) },
            UpdateFunc = (x, y) => x.ExpireDate = y is null || y.Date is null ? default : (y.IsLongTerm ? DateOnly.MaxValue : DateOnly.FromDateTime(y.Date.Value)),
            ClearFunc = x => x.ExpireDate = default,
            DisplayFunc = x => x?.IsLongTerm ?? false ? "长期" : x?.Date?.ToString("yyyy-MM-dd")
        };
        ExpireDate.Init(manager);

        RegisterDate = new ChangeableViewModel<Manager, DateTime?>
        {
            Label = "登记日期",
            InitFunc = x => new DateTime(x.RegisterDate, default),
            UpdateFunc = (x, y) => x.RegisterDate = y is null ? default : DateOnly.FromDateTime(y.Value),
            ClearFunc = x => x.RegisterDate = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        RegisterDate.Init(manager);


        Telephone = new ChangeableViewModel<Manager, string>
        {
            Label = "固定电话",
            InitFunc = x => x.Telephone,
            UpdateFunc = (x, y) => x.Telephone = y,
            ClearFunc = x => x.Telephone = null,
        };
        Telephone.Init(manager);

        Fax = new ChangeableViewModel<Manager, string>
        {
            Label = "传真",
            InitFunc = x => x.Fax,
            UpdateFunc = (x, y) => x.Fax = y,
            ClearFunc = x => x.Fax = null,
        };
        Fax.Init(manager);

        Email = new ChangeableViewModel<Manager, string>
        {
            Label = "邮箱",
            InitFunc = x => x.Email,
            UpdateFunc = (x, y) => x.Email = y,
            ClearFunc = x => x.Email = null,
        };
        Email.Init(manager);

        InstitutionCode = new ChangeableViewModel<Manager, string>
        {
            Label = "统一信用代码",
            InitFunc = x => x.Identity?.Id,
            UpdateFunc = (x, y) => throw new Exception(),
            ClearFunc = x => throw new Exception(),
        };
        InstitutionCode.Init(manager);



        RegisterAddress = new ChangeableViewModel<Manager, string>
        {
            Label = "注册地址",
            InitFunc = x => x.RegisterAddress,
            UpdateFunc = (x, y) => x.RegisterAddress = y,
            ClearFunc = x => x.RegisterAddress = null,
        };
        RegisterAddress.Init(manager);



        OfficeAddress = new ChangeableViewModel<Manager, string>
        {
            Label = "办公地址",
            InitFunc = x => x.OfficeAddress,
            UpdateFunc = (x, y) => x.OfficeAddress = y,
            ClearFunc = x => x.OfficeAddress = null,
        };
        OfficeAddress.Init(manager);



        BusinessScope = new ChangeableViewModel<Manager, string>
        {
            Label = "经营范围",
            InitFunc = x => x.BusinessScope,
            UpdateFunc = (x, y) => x.BusinessScope = y,
            ClearFunc = x => x.BusinessScope = null,
        };
        BusinessScope.Init(manager);

        Description = new()
        {
            Label = "简介",
            InitFunc = x => x.Description,
            UpdateFunc = (x, y) => x.Description = y,
            ClearFunc = x => x.Description = null,
        };
        Description.Init(manager);


        WebSite = new ChangeableViewModel<Manager, string>
        {
            Label = "官网",
            InitFunc = x => x.WebSite,
            UpdateFunc = (x, y) => x.WebSite = y,
            ClearFunc = x => x.WebSite = null,
        };
        WebSite.Init(manager);


        #endregion


        var cef = db.GetCollection<InstitutionCertifications>().FindById(manager.Identity!.Id);
        if (cef is null)
        {
            cef = new() { Id = manager.Identity.Id };
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




        Flows = [.. db.GetCollection<ManagerFlow>().FindAll().Select(x => new ManagerFlowViewModel(x))];


        //db.GetCollection<PolicyDocument>().DeleteAll();
        var docs = db.GetCollection<PolicyDocument>().FindAll().ToList();//[new() { Label = "交易制度", Files = [new() { Normal = new("432urjeowr", "aaa.pdf", DateTime.Now, "fdf") }] }, new() { Label = "管理制度" }];

        var need = new List<string>
            {
                "运营风险控制制度",
                "信息披露制度",
                "机构内部交易记录制度",
                "防范内幕交易、利益冲突的投资交易制度",
                "合格投资者风险揭示制度",
                "合格投资者内部审核流程及相关制度",
                "募集相关规范制度",
                "公平交易制度",
                "从业人员买卖证券申报制度",
            };
        docs.AddRange(need.Except(docs.Select(x => x.Label)).Select(x => new PolicyDocument { Label = x, }));


        //PolicyDocuments = [.. docs.Select(x => new MultiDualFileViewModel(x))];

        Dictionary<MultiDualFileViewModel, int> PolicyFileMap = docs.Select(x => (x, new MultiDualFileViewModel(x))).ToDictionary(x => x.Item2, x => x.x.Id);
        PolicyDocuments = [.. PolicyFileMap.Keys];

        foreach (var item in PolicyDocuments)
        {
            item.FileChanged += (x) =>
            {
                using var db = DbHelper.Base();
                db.GetCollection<PolicyDocument>().Upsert(new PolicyDocument { Label = x.Label, Files = x.Files });
            };
        }

    }


    private void UpdateCerf<T1, T2>(T1 doc, T2 id)
    {
        using var db = DbHelper.Base();
        db.GetCollection<InstitutionCertifications>().UpdateMany(BsonMapper.Global.ToDocument(doc).ToString(), $"_id={new BsonValue(id)}");
    }


    public override Manager EntityOverride(LiteDB.ILiteDatabase db)
    {
        return db.GetCollection<Manager>().FindOne(x => x.IsMaster);
    }

    [RelayCommand]
    public void OpenLink()
    {
        if (!string.IsNullOrWhiteSpace(WebSite.OldValue))
            try { Process.Start(new ProcessStartInfo(WebSite.OldValue) { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    public void OpenAmacPage()
    {
        try { Process.Start(new ProcessStartInfo(AmacPageUrl) { UseShellExecute = true }); } catch { }
    }


    [RelayCommand]
    public void AddMember()
    {
        Participant obj = new();
        Members.Add(new(obj));
        MemberContext = new ManagerMemberViewModel(obj);
        ShowMemberPopup = true;
    }


    [RelayCommand]
    public void RemoveMember(ParticipantViewModel participant)
    {
        if (HandyControl.Controls.MessageBox.Show($"是否确认删除 {participant.Name}？", button: MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            using var db = DbHelper.Base();
            db.GetCollection<Participant>().Delete(participant.Id);

            Members.Remove(participant);
        }
    }

    [RelayCommand]
    public void EditMember(ParticipantViewModel participant)
    {
        if (participant.Id == 0) return;
        using var db = DbHelper.Base();
        var obj = db.GetCollection<Participant>().FindById(participant.Id);
        MemberContext = new ManagerMemberViewModel(obj);
        ShowMemberPopup = true;
    }

    partial void OnShowMemberPopupChanged(bool value)
    {
        if (!value && Members.LastOrDefault() is ParticipantViewModel p && p.Id == 0)
            Members.Remove(p);
    }
    partial void OnSelectedMemberChanged(Participant? value)
    {
        MemberContext = value is null ? null : new ManagerMemberViewModel(value);
    }

    [RelayCommand]
    public async Task LoadMemberFromAmac()
    {
        try
        {
            // 检查有没有账号
            using var db = DbHelper.Base();
            var acc = db.GetCollection<AmacAccount>().FindById("human");
            if (acc is null || string.IsNullOrWhiteSpace(acc.Name) || string.IsNullOrWhiteSpace(acc.Password))
            {
                HandyControl.Controls.Growl.Error($"请在【平台】中，设置协会的账号和密码");
                return;
            }

            var result = await AmacHuman.GetParticipants(acc.Name, acc.Password);

            switch (result.Code)
            {
                case AmacReturn.AccountError:
                    HandyControl.Controls.Growl.Error($"获取管理人成员失败，账号密码错误，请在【平台】中，修改账号");
                    return;
                case AmacReturn.Browser:
                case AmacReturn.InvalidResponse:
                    HandyControl.Controls.Growl.Error($"获取管理人成员失败，请查看log");
                    return;
                default:
                    break;
            }


            // 处理数据
            var data = db.GetCollection<Participant>().FindAll().ToArray();
            foreach (var x in result.Data)
            {
                var old = data.FirstOrDefault(y => y.Name == x.Name && y.Identity?.Id == x.Identity?.Id);
                x.Id = old?.Id ?? 0;
            }
            db.GetCollection<Participant>().Upsert(result.Data);


            Members = new(db.GetCollection<Participant>().FindAll().ToArray().Select(x => new ParticipantViewModel(x)));
            MemberSource.View.Refresh();

            HandyControl.Controls.Growl.Success($"获取管理人成员成功");
        }
        catch (Exception ex)
        {
            Log.Error($"获取管理人成员失败，{ex}");
            HandyControl.Controls.Growl.Error($"获取管理人成员失败，请查看log");
        }
    }


    [RelayCommand]
    public void AddShareHolder()
    {
        var db = DbHelper.Base();
        var manager = db.GetCollection<Manager>().FindById(1);

        var wnd = new AddOrModifyShareHolderWindow();
        wnd.DataContext = new AddOrModifyShareHolderWindowViewModel(manager);
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();

        var rel = db.GetCollection<Ownership>().Find(x => x.InstitutionId == 0).ToArray();
        var entities = db.GetCollection<IEntity>().FindAll().ToArray();
        foreach (var x in rel.ExceptBy(ShareRelations.Select(x => x.Holder?.Id), x => x.HolderId))
        {
            ShareRelations.Add(new RelationViewModel
            {
                Id = x.Id,
                Holder = entities.FirstOrDefault(y => y.Id == x.HolderId),
                Institution = manager,
                Share = x.Share,
                Ratio = x.Ratio == 0 ? x.Share / manager!.RegisterCapital : x.Ratio
            });
        }


        ShareNotPair = ShareRelations.Sum(x => x.Share) != manager.RegisterCapital;

    }

    [RelayCommand]
    public void AddPerson()
    {
        var wnd = new AddOrModifyPersonWindow();
        wnd.DataContext = new PersonViewModel(new Person { Name = "" });
        wnd.Owner = App.Current.MainWindow;
        wnd.ShowDialog();
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
        var db = DbHelper.Base();
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


    [RelayCommand]
    public void OpenOwnership()
    {
        //var wnd = new Window();
        //EquityStructureDiagram dig = new();
        //wnd.Content = dig;
        //dig.SetBinding(EquityStructureDiagram.NodesProperty, new Binding(nameof(EquityViewModel.CompanyNodes)));
        //wnd.DataContext = new EquityViewModel();
        //wnd.Owner = App.Current.MainWindow;
        //wnd.ShowDialog();


        //using var db = DbHelper.Base();
        //var per = db.GetCollection<IEntity>().FindAll().ToList();
        //per.Insert(0, db.GetCollection<Manager>().FindById(1));


        //var os = db.GetCollection<Ownership>().FindAll().ToArray();
        //// 解析股权结构图
        //List<OwnershipItem> data = new();

        //var ins = per.FirstOrDefault(x => x.Id == 1);
        //var oi = new OwnershipItem { Name = ins!.Name };
        //Parse(1, per, os, oi);




    }

    [RelayCommand]
    public void AddFlow()
    {
        Flows.Add(new ManagerFlowViewModel(new()));
    }

    [RelayCommand]
    public void DeleteFlow(ManagerFlowViewModel v)
    {
        if (v.Id != 0 && HandyControl.Controls.MessageBox.Show($"确认删除【{v.Title.NewValue}】吗？，无法恢复", button: MessageBoxButton.YesNo) == MessageBoxResult.No)
            return;

        using var db = DbHelper.Base();
        db.GetCollection<ManagerFlow>().Delete(v.Id);
        Flows.Remove(v);
    }


    [RelayCommand(CanExecute = nameof(CanAddPolicy))]
    public void AddPolicyDoc()
    {
        MultiDualFileViewModel item = new() { Label = NewPolicyName };
        item.FileChanged += (x) =>
        {
            using var db = DbHelper.Base();
            db.GetCollection<PolicyDocument>().Upsert(new PolicyDocument { Label = x.Label, Files = x.Files });
        };

        PolicyDocuments.Add(item);
        IsAddingNewPolicy = false;
    }

    private OwnershipItem Parse(int institutionId, IList<IEntity> per, IEnumerable<Ownership> os, OwnershipItem oi)
    {
        foreach (var item in os.Where(x => x.InstitutionId == institutionId))
        {
            var ins = per.FirstOrDefault(x => x.Id == item.InstitutionId);
            var holder = per.FirstOrDefault(x => x.Id == item.HolderId);

            if (holder is Person p)
            {
                oi.Childs.Add(new OwnershipItem { Name = p.Name, Ratio = item.Ratio });
                continue;
            }
            else if (holder is Institution)
                oi.Childs.Add(Parse(holder.Id, per, os, new OwnershipItem { Name = holder.Name, Ratio = item.Ratio }));


        }

        return oi;
    }


    #region MyRegion





    //private void BusinessLicense_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    //{
    //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
    //    {
    //        using var db = DbHelper.Base();

    //        foreach (FileInfo item in e.NewItems)
    //        {
    //            string hash = item.ComputeHash()!;

    //            // 保存副本
    //            var dir = Directory.CreateDirectory("manager");
    //            var tar = FileHelper.CopyFile(item, dir.FullName);

    //            FileVersion fileVersion = new FileVersion { Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), tar.Path), Hash = hash, Time = item.LastWriteTime };

    //            var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);
    //            if (files.BusinessLicense is null)
    //                files.BusinessLicense = new VersionedFileInfo { Name = nameof(BusinessLicense), Files = new() };
    //            files!.BusinessLicense.Files.Add(fileVersion);
    //            db.GetCollection<InstitutionFiles>().Update(files);
    //        }
    //    }
    //    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems is not null)
    //    {
    //        using var db = DbHelper.Base();
    //        var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);


    //        foreach (FileInfo item in e.OldItems)
    //        {
    //            var rp = Path.GetRelativePath(Directory.GetCurrentDirectory(), item.FullName);
    //            var file = files!.BusinessLicense?.Files.FirstOrDefault(x => rp == x.Path || x.Path == item.FullName);
    //            if (file is not null)
    //                files.BusinessLicense!.Files.Remove(file);
    //        }

    //        db.GetCollection<InstitutionFiles>().Update(files!);
    //    }

    //}
    //private void BusinessLicense2_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    //{
    //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
    //    {
    //        using var db = DbHelper.Base();

    //        foreach (FileInfo item in e.NewItems)
    //        {
    //            string hash = item.ComputeHash()!;

    //            // 保存副本
    //            var dir = Directory.CreateDirectory("manager");
    //            var tar = FileHelper.CopyFile(item, dir.FullName);

    //            FileVersion fileVersion = new FileVersion { Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), tar.Path), Hash = hash, Time = item.LastWriteTime };

    //            var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);
    //            if (files.BusinessLicense2 is null)
    //                files.BusinessLicense2 = new VersionedFileInfo { Name = nameof(BusinessLicense2), Files = new() };
    //            files!.BusinessLicense2.Files.Add(fileVersion);
    //            db.GetCollection<InstitutionFiles>().Update(files);
    //        }
    //    }
    //    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems is not null)
    //    {
    //        using var db = DbHelper.Base();
    //        var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);


    //        foreach (FileInfo item in e.OldItems)
    //        {
    //            var rp = Path.GetRelativePath(Directory.GetCurrentDirectory(), item.FullName);
    //            var file = files!.BusinessLicense2?.Files.FirstOrDefault(x => rp == x.Path || x.Path == item.FullName);
    //            if (file is not null)
    //                files.BusinessLicense2!.Files.Remove(file);
    //        }

    //        db.GetCollection<InstitutionFiles>().Update(files!);
    //    }

    //}
    //private void AccountOpeningLicense_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    //{
    //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
    //    {
    //        using var db = DbHelper.Base();

    //        foreach (FileInfo item in e.NewItems)
    //        {
    //            string hash = item.ComputeHash()!;

    //            // 保存副本
    //            var dir = Directory.CreateDirectory("manager");
    //            var tar = FileHelper.CopyFile(item, dir.FullName);

    //            FileVersion fileVersion = new FileVersion { Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), tar.Path), Hash = hash, Time = item.LastWriteTime };

    //            var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);
    //            if (files.AccountOpeningLicense is null)
    //                files.AccountOpeningLicense = new VersionedFileInfo { Name = nameof(AccountOpeningLicense), Files = new() };
    //            files!.AccountOpeningLicense.Files.Add(fileVersion);
    //            db.GetCollection<InstitutionFiles>().Update(files);
    //        }
    //    }
    //    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems is not null)
    //    {
    //        using var db = DbHelper.Base();
    //        var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);


    //        foreach (FileInfo item in e.OldItems)
    //        {
    //            var rp = Path.GetRelativePath(Directory.GetCurrentDirectory(), item.FullName);
    //            var file = files!.AccountOpeningLicense?.Files.FirstOrDefault(x => rp == x.Path || x.Path == item.FullName);
    //            if (file is not null)
    //                files.AccountOpeningLicense!.Files.Remove(file);
    //        }

    //        db.GetCollection<InstitutionFiles>().Update(files!);
    //    }

    //}
    //private void CharterDocument_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    //{
    //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
    //    {
    //        using var db = DbHelper.Base();

    //        foreach (FileInfo item in e.NewItems)
    //        {
    //            string hash = item.ComputeHash()!;

    //            // 保存副本
    //            var dir = Directory.CreateDirectory("manager");
    //            var tar = FileHelper.CopyFile(item, dir.FullName);

    //            FileVersion fileVersion = new FileVersion { Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), tar.Path), Hash = hash, Time = item.LastWriteTime };

    //            var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);
    //            if (files.CharterDocument is null)
    //                files.CharterDocument = new VersionedFileInfo { Name = nameof(CharterDocument), Files = new() };
    //            files!.CharterDocument.Files.Add(fileVersion);
    //            db.GetCollection<InstitutionFiles>().Update(files);
    //        }
    //    }
    //    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems is not null)
    //    {
    //        using var db = DbHelper.Base();
    //        var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);


    //        foreach (FileInfo item in e.OldItems)
    //        {
    //            var rp = Path.GetRelativePath(Directory.GetCurrentDirectory(), item.FullName);
    //            var file = files!.CharterDocument?.Files.FirstOrDefault(x => rp == x.Path || x.Path == item.FullName);
    //            if (file is not null)
    //                files.CharterDocument!.Files.Remove(file);
    //        }

    //        db.GetCollection<InstitutionFiles>().Update(files!);
    //    }

    //}
    //private void LegalPersonIdCard_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    //{
    //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
    //    {
    //        using var db = DbHelper.Base();

    //        foreach (FileInfo item in e.NewItems)
    //        {
    //            string hash = item.ComputeHash()!;

    //            // 保存副本
    //            var dir = Directory.CreateDirectory("manager");
    //            var tar = FileHelper.CopyFile(item, dir.FullName);

    //            FileVersion fileVersion = new FileVersion { Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), tar.Path), Hash = hash, Time = item.LastWriteTime };

    //            var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);
    //            if (files.LegalPersonIdCard is null)
    //                files.LegalPersonIdCard = new VersionedFileInfo { Name = nameof(LegalPersonIdCard), Files = new() };
    //            files!.LegalPersonIdCard.Files.Add(fileVersion);
    //            db.GetCollection<InstitutionFiles>().Update(files);
    //        }
    //    }
    //    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems is not null)
    //    {
    //        using var db = DbHelper.Base();
    //        var files = db.GetCollection<InstitutionFiles>().FindById(FilesId);


    //        foreach (FileInfo item in e.OldItems)
    //        {
    //            var rp = Path.GetRelativePath(Directory.GetCurrentDirectory(), item.FullName);
    //            var file = files!.LegalPersonIdCard?.Files.FirstOrDefault(x => rp == x.Path || x.Path == item.FullName);
    //            if (file is not null)
    //                files.LegalPersonIdCard!.Files.Remove(file);
    //        }

    //        db.GetCollection<InstitutionFiles>().Update(files!);
    //    }

    //}

    internal void SetLogo(string v)
    {
        using var db = DbHelper.Base();
        db.FileStorage.Upload("icon.main", v);
        if (db.FileStorage.Exists("icon.main"))
        {
            try
            {
                using var ms = new MemoryStream();
                db.FileStorage.Download("icon.main", ms);
                ms.Seek(0, SeekOrigin.Begin);
                BitmapImage bitmapSource = new BitmapImage();
                bitmapSource.BeginInit();
                bitmapSource.CacheOption = BitmapCacheOption.OnLoad;
                bitmapSource.StreamSource = ms;
                bitmapSource.EndInit();
                MainLogo = bitmapSource;

                App.Current.MainWindow.Icon = MainLogo;
            }
            catch (Exception e)
            {
                Log.Error($"SetLogo Error {e}");
            }
        }
    }

    protected override Manager InitNewEntity()
    {
        using var db = DbHelper.Base();
        var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster);
        return manager;
    }

    public void Receive(ParticipantChangedMessage message)
    {
        using var db = DbHelper.Base();
        var obj = db.GetCollection<Participant>().FindById(message.Id);


        if (obj is not null && (Members.FirstOrDefault(x => x.Id == message.Id) ?? Members.LastOrDefault(x => x.Id == 0)) is ParticipantViewModel old)
        {
            old.UpdateFrom(obj);
            MemberSource.View.Refresh();
        }
    }
    #endregion



    public partial class RelationViewModel : ObservableObject
    {
        public int Id { get; set; }

        /// <summary>
        /// 持有人
        /// </summary>
        public IEntity? Holder { get; set; }

        /// <summary>
        /// 持股的机构、公司
        /// 0 表示 管理人
        /// </summary>
        public required Institution Institution { get; set; }


        public string? InstitutionName { get; set; }

        public decimal Share { get; set; }

        public decimal Ratio { get; set; }


        public ObservableCollection<RelationViewModel>? Children { get; set; }

        [RelayCommand]
        public void AddShareHolder()
        {
            var wnd = new AddOrModifyShareHolderWindow();
            wnd.DataContext = new AddOrModifyShareHolderWindowViewModel(Institution);
            wnd.Owner = App.Current.MainWindow;
            wnd.ShowDialog();

        }

        [RelayCommand]
        public void AddPerson()
        {
            var wnd = new AddOrModifyPersonWindow();
            wnd.DataContext = new PersonViewModel(new Person { Name = "" });
            wnd.Owner = App.Current.MainWindow;
            wnd.ShowDialog();
        }


        [RelayCommand]
        public void RemoveShareHolder(RelationViewModel value)
        {
            if (HandyControl.Controls.MessageBox.Show($"是否确认删除 {value.Holder!.Name}？", button: MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using var db = DbHelper.Base();
                db.GetCollection<Ownership>().Delete(value.Id);

                Children?.Remove(value);
            }
        }

        [RelayCommand]
        public void EditShareHolder(RelationViewModel value)
        {
            var wnd = new AddOrModifyShareHolderWindow();
            AddOrModifyShareHolderWindowViewModel obj = new(Institution);
            obj.Holder = value.Holder;
            obj.Institution = value.Institution;
            obj.ShareAmount = value.Share;
            wnd.DataContext = obj;
            wnd.Owner = App.Current.MainWindow;
            wnd.ShowDialog();

        }


        [RelayCommand]
        public void OpenEntity()
        {
            if (Holder is Institution institution)
            {
                try
                {
                    var wnd = new InstitutionWindow();
                    var context = new InstitutionWindowViewModel(Holder.Id);
                    wnd.DataContext = context;
                    wnd.Owner = App.Current.MainWindow;
                    wnd.ShowDialog();
                }
                catch (Exception e) { Log.Error($"{e}"); }
            }
        }

    }
}


public partial class ParticipantViewModel : ObservableObject
{
    public ParticipantViewModel(FMO.Models.Participant? instance)
    {
        if (instance is FMO.Models.Participant obj)
        {
            Id = obj.Id;
            Name = obj.Name;
            Role = obj.Role;
            Identity = obj.Identity;
            Title = obj.Title;
            Phone = obj.Phone;
            Address = obj.Address;
            Email = obj.Email;
            Profile = obj.Profile;
            CertCode = obj.CertCode;
            Post = obj.Post;
        }
    }

    [ObservableProperty]
    public partial string? Name { get; set; }


    public int Id { get; set; }

    [ObservableProperty]
    public partial Identity Identity { get; set; }

    [ObservableProperty]
    public partial PersonRole Role { get; set; }

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Cellphone { get; set; }

    [ObservableProperty]
    public partial string? Phone { get; set; }

    [ObservableProperty]
    public partial string? Address { get; set; }

    [ObservableProperty]
    public partial string? Email { get; set; }

    [ObservableProperty]
    public partial string? Profile { get; set; }

    public string? CertCode { get; set; }
    public string? Post { get; private set; }
    [ObservableProperty]
    public partial FileInfo? IdFile { get; set; }


    [ObservableProperty]
    public partial FileInfo? SealedIdFile { get; set; }


    partial void OnIdentityChanged(Identity value)
    {
        var path = $@"manager\members\{Id}.{Identity.Id}";
        var di = new DirectoryInfo($@"manager\members");

        if (!di.Exists) return;
        var ff = di.GetFiles();
        IdFile = ff.LastOrDefault(x => x.Name.StartsWith($@"{Id}.{Identity.Id}"));
        SealedIdFile = ff.LastOrDefault(x => x.Name.StartsWith($@"sealed.{Id}.{Identity.Id}"));
    }


    [RelayCommand]
    public void View()
    {
        if (IdFile?.Exists ?? false)
            try { System.Diagnostics.Process.Start(new ProcessStartInfo { FileName = IdFile.FullName, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    public void SetFile()
    {
        var dlg = new OpenFileDialog();
        var r = dlg.ShowDialog();

        if (r is null || !r.Value) return;

        var di = new DirectoryInfo($@"manager\members");

        if (!di.Exists) di.Create();

        string tar = Path.Combine("manager", "members", $"{Id}.{Identity.Id}{Path.GetExtension(dlg.FileName)}");
        try { File.Copy(dlg.FileName, tar); IdFile = new FileInfo(tar); } catch { }
    }





}


public partial class ManagerFlowViewModel : EditableControlViewModelBase<ManagerFlow>
{

    public ChangeableViewModel<ManagerFlow, DateOnly?> Date { get; }

    public ChangeableViewModel<ManagerFlow, string> Title { get; }

    public ChangeableViewModel<ManagerFlow, string> Description { get; }


    [ObservableProperty]
    public partial bool IsTemple { get; set; }


    public bool IsFinished { get; set; }

    public ManagerFlowViewModel(ManagerFlow flow)
    {
        Id = flow.Id;

        Date = new()
        {
            Label = "日期",
            InitFunc = x => x.Date == default ? null : x.Date,
            UpdateFunc = (x, y) => { IsTemple = false; x.Date = y ?? default; },
            ClearFunc = x => x.Date = DateOnly.MinValue,
        };
        Date.Init(flow);

        Title = new()
        {
            Label = "标题",
            InitFunc = x => x.Title,
            UpdateFunc = (x, y) => { IsTemple = false; x.Title = y; },
            ClearFunc = x => x.Title = string.Empty,
        };
        Title.Init(flow);

        Description = new()
        {
            Label = "描述",
            InitFunc = x => x.Description,
            UpdateFunc = (x, y) => { IsTemple = false; x.Description = y; },
            ClearFunc = x => x.Description = string.Empty,
        };
        Description.Init(flow);

        IsTemple = Id == 0;


    }



    [RelayCommand]
    public void OpenNormal() => OpenFolder(@$"manager\flow\{Id}\normal");


    [RelayCommand]
    public void OpenSealed() => OpenFolder(@$"manager\flow\{Id}\sealed");


    [RelayCommand]
    public void AddSealedFile(DragEventArgs args) => SaveFile(args, @$"manager\flow\{Id}\sealed");


    [RelayCommand]
    public void AddNormalFile(DragEventArgs args) => SaveFile(args, @$"manager\flow\{Id}\normal");

    private void SaveFile(DragEventArgs e, string folder)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] s)
        {
            foreach (var item in s)
            {
                File.Copy(item, Path.Combine(folder, Path.GetFileName(item)), true);
            }
        }
    }


    private void OpenFolder(string folder)
    {
        try
        {
            DirectoryInfo di = new DirectoryInfo(folder);
            if (!di.Exists)
                di.Create();
            Process.Start(new ProcessStartInfo
            {
                FileName = di.FullName,
                UseShellExecute = true
            });
        }
        catch (Exception e)
        {
            Log.Error($"OpenFolder Error {e}");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "打开文件夹失败"));
        }
    }



    protected override ManagerFlow InitNewEntity() => new ManagerFlow();
}
