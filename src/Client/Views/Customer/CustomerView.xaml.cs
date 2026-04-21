using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FMO.Logging;
using FMO.Models;
using FMO.PDF;
using FMO.Shared;
using FMO.Utilities;
using Microsoft.Win32;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FMO;

/// <summary>
/// CustomerView.xaml 的交互逻辑
/// </summary>
public partial class CustomerView : UserControl
{
    public CustomerView()
    {
        InitializeComponent();
    }

    private void RiskDrop(object sender, DragEventArgs e)
    {
        if (DataContext is CustomerViewModel vm && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            vm.OnChooseAssessmentFile(files[0]);
    }
}



/// <summary>
/// customer vm
/// </summary>
public partial class CustomerViewModel : EditableControlViewModelBase<Investor>, IRecipient<IEnumerable<LinkOrderMessage>>, IRecipient<EntityDeleted>
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))] public enum NaturalType { [Description("非员工")] NonEmployee, [Description("员工")] Employee };

    public static RiskEvaluation[] RiskEvaluations { get; } = [RiskEvaluation.C1, RiskEvaluation.C2, RiskEvaluation.C3, RiskEvaluation.C4, RiskEvaluation.C5];

    public static EntityType[] EntityTypes { get; } = [Models.EntityType.Natural, Models.EntityType.Institution, Models.EntityType.Product,];

    public static string QualificationTips { get; } =
        """
        专业投资者

        （一）经有关金融监管部门批准设立的金融机构，包括证券公司、期货公司、基金管理公司及其子公司、商业银行、保险公司、信托公司、财务公司等；经行业协会备案或者登记的证券公司子公司、期货公司子公司、私募基金管理人。

        （二）上述机构面向投资者发行的理财产品，包括但不限于证券公司资产管理产品、基金管理公司及其子公司产品、期货公司资产管理产品、银行理财产品、保险产品、信托产品、经行业协会备案的私募基金。

        （三）社会保障基金、企业年金等养老基金，慈善基金等社会公益基金，合格境外机构投资者（QFII）、人民币合格境外机构投资者（RQFII）。

        （四）同时符合下列条件的法人或者其他组织：

            1.最近1年末净资产不低于2000万元；

            2.最近1年末金融资产不低于1000万元；

            3.具有2年以上证券、基金、期货、黄金、外汇等投资经历。

        （五）同时符合下列条件的自然人：

            1.金融资产不低于500万元，或者最近3年个人年均收入不低于50万元；

            2.具有2年以上证券、基金、期货、黄金、外汇等投资经历，或者具有2年以上金融产品设计、投资、风险管理及相关工作经历，或者属于本条第（一）项规定的专业投资者的高级管理人员、获得职业资格认证的从事金融相关业务的注册会计师和律师。
            前款所称金融资产，是指银行存款、股票、债券、基金份额、资产管理计划、银行理财产品、信托计划、保险产品、期货及其他衍生产品等。

        ------------------------------------------------------------------------------------

        普通投资者

        （一）净资产不低于1000万元的单位；
        （二）金融资产不低于300万元或者最近三年个人年均收入不低于50万元的个人
        
        """;

    public ChangeableViewModel<Investor, string> Name { get; }

    public ChangeableViewModel<Investor, EntityType> EntityType { get; } = new() { InitFunc = x => x.EntityType, UpdateFunc = (x, y) => x.EntityType = y, ClearFunc = x => x.EntityType = Models.EntityType.Unk, Label = "客户类型" };


    [ObservableProperty]
    public partial AmacInvestorType[]? InvestorTypes { get; set; }


    public ChangeableViewModel<Investor, AmacInvestorType> Type { get; } = new() { InitFunc = x => x.Type, UpdateFunc = (x, y) => x.Type = y, ClearFunc = x => x.Type = AmacInvestorType.None, Label = "" };



    public ChangeableViewModel<Investor, IDType> IDType { get; }

    public ChangeableViewModel<Investor, string> Identity { get; }

    public ChangeableViewModel<Investor, string> Email { get; } = new() { InitFunc = x => x.Email, UpdateFunc = (x, y) => x.Email = y, ClearFunc = x => x.Email = null, Label = "Email" };

    public ChangeableViewModel<Investor, string> Phone { get; } = new() { InitFunc = x => x.Phone, UpdateFunc = (x, y) => x.Phone = y, ClearFunc = x => x.Phone = null, Label = "联系方式" };

    public ChangeableViewModel<Investor, string> Address { get; } = new() { InitFunc = x => x.Address, UpdateFunc = (x, y) => x.Address = y, ClearFunc = x => x.Address = null, Label = "联系地址" };

    public MultiFileViewModel IDCards { get; }


    [ObservableProperty]
    public partial IDType[]? IDTypes { get; set; }


    [ObservableProperty]
    public partial QualifiedInvestorType QualifiedInvestorType { get; set; }

    /// <summary>
    /// 特殊合格投资者
    /// </summary>
    [ObservableProperty]
    public partial bool IsSpecial { get; set; }

    /// <summary>
    /// 民族
    /// </summary>

    [ObservableProperty]
    public partial string? Nation { get; set; }


    //public EntityDateEfficientViewModel<Investor> Efficient { get; } = new() { InitFunc = x => x.Efficient, UpdateFunc = (x, y) => x.Efficient = y, ClearFunc = x => x.Efficient = default, Label = "证件有效期" };
    public ChangeableViewModel<Investor, DateEfficientViewModel> Efficient { get; } = new() { InitFunc = x => new DateEfficientViewModel(x.Efficient), UpdateFunc = (x, y) => x.Efficient = y?.Build() ?? default, Label = "证件有效期" };

    [ObservableProperty]
    public partial RiskLevel? RiskLevel { get; set; }

    /// <summary>
    /// 允许豁免风测
    /// </summary>
    public bool CanSkipRiskEvaluation => Type.OldValue switch { AmacInvestorType.Employee or AmacInvestorType.Manager => true, _ => false } | Qualifications.Any(x => x.IsProfessional);

    [ObservableProperty]
    public partial Enum[]? DetailTypes { get; set; }


    public ObservableCollection<QualificationViewModel> Qualifications { get; }


    public ObservableCollection<RiskAssessmentViewModel> RiskAssessments { get; }


    public IEnumerable<TransferRecordByFund>? TransferRecords { get; set; }


    [ObservableProperty]
    public partial QualificationViewModel? SelectedQualification { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAssessmentFileChoosed))]
    public partial string? AssessmentFile { get; set; }


    public bool IsAssessmentFileChoosed => File.Exists(AssessmentFile);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRiskAssessmentCommand))]
    public partial RiskEvaluation? NewEvaluation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RiskConflict))]
    [NotifyCanExecuteChangedFor(nameof(AddRiskAssessmentCommand))]
    public partial DateTime? NewDate { get; set; }


    public bool RiskConflict => RiskAssessments.Any(x => x.Date.Year == NewDate?.Year && x.Date.Month == NewDate?.Month && x.Date.Day == NewDate?.Day);

    public bool CanAddRiskAssessment => IsAssessmentFileChoosed && NewDate != default && NewEvaluation != default;


    [ObservableProperty]
    public partial bool IsRiskEvaluationSkiped { get; set; }


    [ObservableProperty]
    public partial decimal? TotalProfit { get; set; }



    public ObservableCollection<InvestorBankAccount> Accounts { get; }

    //public CustomerViewModel()
    //{
    //    Qualifications = new();
    //}


    public CustomerViewModel(int id)
    {
        using var db = DbHelper.Base();
        var investor = db.GetCollection<Investor>().FindById(id);
        var cards = db.GetCollection<InvestorCertifications>().FindById(id);
        Id = id;

        WeakReferenceMessenger.Default.RegisterAll(this);

        Name = new ChangeableViewModel<Investor, string>(investor, init: x => x.Name, update: (x, y) => x.Name = y ?? string.Empty, clear: x => x.Name = string.Empty);


        //Name.Init(investor);
        EntityType.Init(investor);
        EntityType.PropertyChanged += EntityType_PropertyChanged;
        EntityType_PropertyChanged(null, null);

        Type.Init(investor);
        Type.PropertyChanged += Type_PropertyChanged;
        Type_PropertyChanged(null, null);

        Identity = new()
        {
            InitFunc = x => x.Identity?.Id,
            UpdateFunc = (x, y) => x.Identity = x.Identity is null ? new Identity { Id = y! } : x.Identity with { Id = y! },
            ClearFunc = x => x.Identity = x.Identity is null ? null : x.Identity with { Id = string.Empty }
        };

        Identity.Init(investor);

        IDType = new()
        {
            InitFunc = x => x.Identity?.Type ?? Models.IDType.Unknown,
            UpdateFunc = (x, y) => x.Identity = x.Identity is null ? new Identity { Type = y } : x.Identity with { Type = y },
            ClearFunc = x => x.Identity = x.Identity is null ? null : x.Identity with { Type = default },
            Label = "证件类型"
        };

        IDType.Init(investor);

        Email.Init(investor);
        Phone.Init(investor);
        Address.Init(investor);




        IDCards = new(cards);
        IDCards.FileChanged += (x) =>
        {
            using var db = DbHelper.Base();
            db.GetCollection<InvestorCertifications>().Upsert(new InvestorCertifications
            {
                Id = Id,
                Files = x.Files
            });
        };

        Efficient.Init(investor);


        Accounts = [.. db.GetCollection<InvestorBankAccount>().Find(x => x.OwnerId == investor.Id)];



        var iq = db.GetCollection<InvestorQualification>().Find(x => x.InvestorId == Id/* || (x.InvestorId == 0 && x.InvestorName == investor.Name && x.IdentityCode == investor.Identity.Id)*/).ToArray();
        Qualifications = [.. iq.Select(x => QualificationViewModel.From(x, investor.Type, investor.EntityType))];
        Qualifications.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CanSkipRiskEvaluation));


        RiskAssessments = [.. db.GetCollection<RiskAssessment>().Find(x => x.InvestorId == Id).Select(x => new RiskAssessmentViewModel(x))];

        // TA 记录汇总
        var ta = db.GetCollection<TransferRecord>().Find(x => x.InvestorId == Id).GroupBy(x => x.FundName!);
        List<TransferRecordByFund> trbf = new();
        foreach (var tf in ta)
        {
            var fund = tf.Key;
            var Share = tf.Sum(x => x.ShareChange());
            var Deposit = tf.Where(x => x.Type switch { TransferRecordType.Subscription or TransferRecordType.Purchase or TransferRecordType.MoveIn => true, _ => false }).Sum(x => x.ConfirmedNetAmount);
            var Withdraw = tf.Where(x => x.Type switch { TransferRecordType.Redemption or TransferRecordType.Redemption or TransferRecordType.MoveOut or TransferRecordType.Distribution => true, _ => false }).Sum(x => x.ConfirmedNetAmount);

            var daily = db.GetDailyCollection(tf.First().FundId).Find(x => x.NetValue > 0).OrderByDescending(x => x.Date).FirstOrDefault();
            var nv = daily?.NetValue ?? 0;
            var Asset = Share * nv;
            var profit = Asset + Withdraw - Deposit;

            var rvm = tf.Where(x => TAHelper.RequiredOrder(x.Type)).OrderBy(x => x.ConfirmedDate).Select(x => new TransferRecordViewModel(x)).ToList();

            rvm[0].FirstTrade = true;
            decimal share = rvm[0].ShareChange();
            // 同日同向单
            for (int i = 1; i < rvm.Count; i++)
            {
                if (rvm[i].ConfirmedDate == rvm[i - 1].ConfirmedDate)
                {
                    rvm[i - 1].HasBrotherRecord = true;
                    rvm[i].HasBrotherRecord = true;
                }

                if (share == 0) rvm[i].FirstTrade = true;

                share += rvm[i].ShareChange();
            }




            TransferRecordByFund rbf = new()
            {
                FundId = tf.First().FundId,
                FundName = Fund.GetDefaultShortName(tf.First().FundName),
                FundCode = tf.First().FundCode,
                Asset = Asset,
                Profit = profit,
                Records = rvm
            };
            trbf.Add(rbf);
        }

        TotalProfit = trbf.Sum(x => x.Profit) switch { 0 => null, var n => n };
        TransferRecords = trbf;
    }


    private void Type_PropertyChanged(object? sender, PropertyChangedEventArgs? e)
    {
        OnPropertyChanged(nameof(CanSkipRiskEvaluation));

        switch (Type.NewValue)
        {
            // 请选择
            case AmacInvestorType.None:
                break;

            // 自然人（非员工跟投）
            case AmacInvestorType.NonEmployee:
            // 自然人（员工跟投）
            case AmacInvestorType.Employee:
                IDTypes = Enum.GetValues<IDType>().Where(x => x >= Models.IDType.IdentityCard && x < Models.IDType.Institusion).ToArray();
                break;

            // 本产品管理人跟投
            case AmacInvestorType.Manager:
            // 境内法人机构（公司等）
            case AmacInvestorType.LegalEntity:
            // 境内非法人机构（个人独资企业）
            case AmacInvestorType.IndividualProprietorship:
            // 境内非法人机构（一般合伙企业等）
            case AmacInvestorType.NonLegalEntity:
            // 境外机构
            case AmacInvestorType.Foreign:
            // 财政直接出资
            case AmacInvestorType.DirectFinancialInvestment:
            // 保险资产管理计划
            case AmacInvestorType.InsuranceAssetManagementPlan:
            // 慈善基金、捐赠基金等社会公益基金
            case AmacInvestorType.SocialWelfareFund:
            // 养老基金
            case AmacInvestorType.PensionFund:
            // 社会保障基金
            case AmacInvestorType.SocialSecurityFund:
            // 企业年金
            case AmacInvestorType.EnterpriseAnnuity:
            // 政府类引导基金
            case AmacInvestorType.GovernmentGuidanceFund:
                IDTypes = [Models.IDType.UnifiedSocialCreditCode, Models.IDType.OrganizationCode, Models.IDType.BusinessLicenseNumber, Models.IDType.RegistrationNumber, Models.IDType.Other];
                break;

            // 境外资金（QFII、RQFII等）
            case AmacInvestorType.QFII:
                IDTypes = [Models.IDType.SecuritiesBusinessLicense, Models.IDType.Other];
                break;

            // 产品
            case AmacInvestorType.Product:
            // 私募基金产品
            case AmacInvestorType.PrivateFundProduct:
            // 证券公司及其子公司资产管理计划
            case AmacInvestorType.SecuritiesCompanyAssetManagementPlan:
            // 基金公司及其子公司资产管理计划
            case AmacInvestorType.FundCompanyAssetManagementPlan:
            // 期货公司及其子公司资产管理计划
            case AmacInvestorType.FuturesCompanyAssetManagementPlan:
                IDTypes = [Models.IDType.ProductFilingCode, Models.IDType.Other];
                break;

            // 信托计划
            case AmacInvestorType.TrustPlan:
                IDTypes = [Models.IDType.UnifiedSocialCreditCode, Models.IDType.OrganizationCode, Models.IDType.BusinessLicenseNumber, Models.IDType.RegistrationNumber, Models.IDType.TrustRegistrationSystemProductCode, Models.IDType.Other];
                break;

            // 商业银行理财产品
            case AmacInvestorType.CommercialBankFinancialProduct:
                IDTypes = [Models.IDType.ProductRegistrationCode, Models.IDType.Other];
                break;

            default:
                break;
        }
    }

    private void EntityType_PropertyChanged(object? sender, PropertyChangedEventArgs? e)
    {
        switch (EntityType.NewValue)
        {
            case Models.EntityType.Natural:
                InvestorTypes = [AmacInvestorType.NonEmployee, AmacInvestorType.Employee];
                break;
            case Models.EntityType.Institution:
                InvestorTypes = [AmacInvestorType.Manager, AmacInvestorType.LegalEntity, AmacInvestorType.IndividualProprietorship, AmacInvestorType.NonLegalEntity, AmacInvestorType.QFII, AmacInvestorType.Foreign, AmacInvestorType.DirectFinancialInvestment];
                break;
            case Models.EntityType.Product:
                InvestorTypes = [.. Enum.GetValues<AmacInvestorType>().Where(x => x > AmacInvestorType.Product), AmacInvestorType.QFII];
                break;
            default:
                break;
        }
    }

    //[RelayCommand]
    //public void Delete(UnitViewModel unit)
    //{
    //    if (unit is IEntityViewModel<Investor> entity)
    //    {
    //        using var db = DbHelper.Base();
    //        var v = db.GetCollection<Investor>().FindById(Id);

    //        if (v is not null)
    //        {
    //            entity.RemoveValue(v);
    //            unit.Reset();
    //            db.GetCollection<Investor>().Upsert(v);

    //            WeakReferenceMessenger.Default.Send(v);
    //        }
    //    }
    //}

    //[RelayCommand]
    //public void Reset(UnitViewModel unit)
    //{
    //    var ps = unit.GetType().GetProperties().Where(x => x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(PropertyViewModel<>));
    //    foreach (var item in ps)
    //    {
    //        var ty = item.PropertyType!;
    //        object? obj = item.GetValue(unit);
    //        ty.GetProperty("New")!.SetValue(obj, ty.GetProperty("Old")!.GetValue(obj));
    //    }
    //}

    //[RelayCommand]
    //public void Modify(UnitViewModel unit)
    //{
    //    if (unit is IEntityViewModel<Investor> entity)
    //    {
    //        using var db = DbHelper.Base();
    //        var v = db.GetCollection<Investor>().FindById(Id);

    //        if (v is not null)
    //            entity.UpdateEntity(v);
    //        else if (Name.OldValue is not null)
    //            v = new Investor { Name = Name.OldValue };

    //        if (v is not null)
    //        {
    //            db.GetCollection<Investor>().Upsert(v);
    //            if (Id == 0) Id = v.Id;

    //            WeakReferenceMessenger.Default.Send(v);
    //        }
    //    }
    //    unit.Apply();
    //}


    protected override void ModifyOverride(IPropertyModifier unit)
    {
        base.ModifyOverride(unit);

        var db = DbHelper.Base();
        var customer = db.GetCollection<Investor>().FindById(Id);
        db.Dispose();
        if (customer is not null)
            WeakReferenceMessenger.Default.Send(customer);
    }


    protected override void SaveOverride()
    {
        base.SaveOverride();


        var db = DbHelper.Base();
        var customer = db.GetCollection<Investor>().FindById(Id);
        db.Dispose();
        if (customer is not null)
            WeakReferenceMessenger.Default.Send(customer);
    }


    [RelayCommand]
    public void AddQualification()
    {
        if (Qualifications.Any(x => x.Date.OldValue == default))
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Info, "存在未完成的合格投资者认定流程，请先完成！"));
            return;
        }

        using var db = DbHelper.Base();
        InvestorQualification entity = new InvestorQualification { InvestorId = Id };
        db.GetCollection<InvestorQualification>().Insert(entity);
        QualificationViewModel q = QualificationViewModel.From(entity, Type.OldValue, EntityType.OldValue);

        // 复用投资经历
        if (Qualifications.Select(x => x.ProofOfExperience).FirstOrDefault(x => x.Exists) is FileMetaViewModel sf && sf.Exists)
        {
            q.ProofOfExperience.Meta = sf.Meta;
        }
        Qualifications.Add(q);
    }

    [RelayCommand]
    public void ChooseAssessmentFile()
    {
        var fd = new OpenFileDialog();
        fd.Filter = "文档|*.pdf;*.jpg;*.png;*.jpeg";
        if (!fd.ShowDialog() ?? true) return;

        OnChooseAssessmentFile(fd.FileName);
    }

    [RelayCommand]
    public void DeleteRisk(RiskAssessmentViewModel v)
    {
        using var db = DbHelper.Base();
        db.GetCollection<RiskAssessment>().Delete(v.Id);
        RiskAssessments.Remove(v);
    }


    public void OnChooseAssessmentFile(string s)
    {
        var fi = new FileInfo(s);
        AssessmentFile = fi.FullName;

        // 判断文件名中是否有日期和评估等级
        if (DateTimeHelper.TryFindDate(fi.Name) is DateOnly date)
            NewDate = new DateTime(date, default);

        var m = Regex.Match(fi.Name, @"\bC\d\b");
        if (m.Success)
        {
            try
            {
                NewEvaluation = Enum.Parse<RiskEvaluation>(m.Value, true);
            }
            catch { }
        }

        // 解析评估结果
        var texts = PdfHelper.GetTexts(s);
        if (texts.Length == 0) return;
        // 验证是否有投资人名
        if (!texts.Any(x => x.Contains(Name.OldValue!)))
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, $"请确认是否为 [{Name.OldValue}] 的风险测评文件"));
            return;
        }
        foreach (var str in texts)
        {
            if (DateTimeHelper.TryFindDate(str) is DateOnly date2)
                NewDate = new DateTime(date2, default);

            m = Regex.Match(str, @"\b[CD](\d)\b", RegexOptions.IgnoreCase);
            if (m.Success)
                NewEvaluation = m.Groups[1].Value switch
                {
                    "1" => RiskEvaluation.C1,
                    "2" => RiskEvaluation.C2,
                    "3" => RiskEvaluation.C3,
                    "4" => RiskEvaluation.C4,
                    "5" => RiskEvaluation.C5,
                    _ => RiskEvaluation.Unk
                };
        }

    }


    [RelayCommand(CanExecute = nameof(CanAddRiskAssessment))]
    public void AddRiskAssessment()
    {
        var fd = new FileInfo(AssessmentFile!);

        var r = new RiskAssessment
        {
            Date = DateOnly.FromDateTime(NewDate!.Value),
            Level = NewEvaluation!.Value,
            InvestorId = Id,
        };

        using var db = DbHelper.Base();
        db.GetCollection<RiskAssessment>().DeleteMany(x => x.InvestorId == Id && x.Date == DateOnly.FromDateTime(NewDate.Value));

        db.GetCollection<RiskAssessment>().Insert(r);

        //string destFileName = @$"files\evaluation\{r.Id}{Path.GetExtension(fd.Name)}";
        //File.Copy(fd.FullName, destFileName, true);
        //r.Path = destFileName;

        r.File = FileMeta.Create(fd);

        db.GetCollection<RiskAssessment>().Update(r);
        db.Dispose();

        foreach (var item in RiskAssessments.ToArray())
        {
            if (item.InvestorId == Id && item.Date == DateOnly.FromDateTime(NewDate.Value))
                RiskAssessments.Remove(item);
        }
        RiskAssessments.Add(new RiskAssessmentViewModel(r));

        // 通知
        WeakReferenceMessenger.Default.Send(r);

        AssessmentFile = null;
        NewDate = null;
        NewEvaluation = null;
    }

    [RelayCommand]
    public void CancelRiskAssessment()
    {
        AssessmentFile = null;
        NewDate = null;
        NewEvaluation = null;
    }

    [RelayCommand]
    public void DeleteQualification(QualificationViewModel v)
    {
        if (v.Date.OldValue is not null && HandyControl.Controls.MessageBox.Show("确认删除吗？", button: System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.No)
            return;


        Qualifications.Remove(v);

        if (!v.IsFinished)
        {
            using var db = DbHelper.Base();
            db.GetCollection<InvestorQualification>().Delete(v.Id);
        }
    }


    [RelayCommand]
    public void SkipRiskEvaluation()
    {
        if (HandyControl.Controls.MessageBox.Show("仅管理人自身及员工和专业投资者可以豁免，是否确认", "提示", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            using var db = DbHelper.Base();
            if (db.GetCollection<Investor>().FindById(Id) is Investor investor)
            {
                investor.RiskEvaluation = RiskEvaluation.C5;
                db.GetCollection<Investor>().Update(investor);
                WeakReferenceMessenger.Default.Send(investor);
            }
        }
    }


    [RelayCommand]
    public void AddBank()
    {
        Accounts.Add(new InvestorBankAccount { OwnerId = Id, Name = Name.OldValue });
    }

    [RelayCommand]
    public void UpdateBank(InvestorBankAccount account)
    {
        try
        {
            using var db = DbHelper.Base();
            db.GetCollection<InvestorBankAccount>().Upsert(account);

            WeakReferenceMessenger.Default.Send(account);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(CustomerViewModel)} {nameof(UpdateBank)} {e}");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "更新银行账户出错"));
        }
    }

    [RelayCommand]
    public void DeleteBank(InvestorBankAccount account)
    {
        try
        {
            using var db = DbHelper.Base();
            db.GetCollection<InvestorBankAccount>().Delete(account.Id);
            WeakReferenceMessenger.Default.Send(new EntityDeleted<InvestorBankAccount>(account));

            Accounts.Remove(account);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(CustomerViewModel)} {nameof(DeleteBank)} {e}");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "删除银行账户出错"));
        }
    }


    [RelayCommand]
    public void MergeCardPhoto()
    {
        var ofd = new OpenFileDialog();
        ofd.Filter = "图片|*.png;*.jpg;*.jpeg;";

        ofd.Multiselect = true;

        bool? result = ofd.ShowDialog();

        if (result != true)
            return;

        string[] files = ofd.FileNames;

        if (files.Length != 2)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "请选择且仅选择两个图片文件"));
            return;
        }

        try
        {
            // 加载图像
            BitmapSource bitmap1 = LoadBitmapFromFile(files[0]);
            BitmapSource bitmap2 = LoadBitmapFromFile(files[1]);

            if (bitmap1 == null || bitmap2 == null)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "无法加载图片，请检查文件格式"));
                return;
            }

            // 计算总宽度和最大高度
            int totalWidth = bitmap1.PixelWidth + bitmap2.PixelWidth;
            int maxHeight = Math.Max(bitmap1.PixelHeight, bitmap2.PixelHeight);

            // 创建绘图目标
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawImage(bitmap1, new Rect(0, 0, bitmap1.PixelWidth, bitmap1.PixelHeight));
                context.DrawImage(bitmap2, new Rect(bitmap1.PixelWidth, 0, bitmap2.PixelWidth, bitmap2.PixelHeight));
            }

            // 渲染为位图
            RenderTargetBitmap rtb = new RenderTargetBitmap(
                totalWidth, maxHeight,
                96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            // 保存为 JPG 文件
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 90; // 设置画质
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            var di = new DirectoryInfo($@"files\investor\{Id}\cards");
            if (!di.Exists) di.Create();
            var path = Path.Combine(di.FullName, $"photo-{DateTime.Now:yyyyMMdd}.jpg");
            using (var fs = new FileStream(path, FileMode.Create))
                encoder.Save(fs);

            // 保存
            var fi = new FileInfo(path);

            //var hash = fi.ComputeHash();
            //FileStorageInfo fsi = new()
            //{
            //    Title = "",
            //    Path = path,
            //    Hash = hash,
            //    Time = DateTime.Now
            //};
            //using var db = DbHelper.Base();
            //var cus = db.GetCollection<Investor>().FindById(Id);
            //if (cus.IDCards is null) cus.IDCards = [fsi];
            //else cus.IDCards.Add(fsi);
            //db.GetCollection<Investor>().Update(cus);

            IDCards.AddFile(fi.FullName);
        }
        catch (Exception ex)
        {
            Log.Error($"合并证件图片出错 {ex}");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "合并证件图片出错"));
        }

    }
    private BitmapSource LoadBitmapFromFile(string path)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
        }
    }



    //[RelayCommand]
    //public void AddFile(IFileSelector obj)
    //{
    //    if (obj is not MultiFileViewModel<Investor> v) return;

    //    var fd = new OpenFileDialog();
    //    fd.Filter = v.Filter;
    //    if (fd.ShowDialog() != true)
    //        return;

    //    var fi = new FileInfo(fd.FileName);
    //    if (fi is not null)
    //        SetFile(v, fi);

    //}




    protected override Investor InitNewEntity() => new Investor { Name = string.Empty };

    public void Receive(LinkOrderMessage message)
    {
        if (TransferRecords is null) return;
        try
        {
            foreach (var item in TransferRecords.Where(x => x.Records is not null).SelectMany(x => x.Records!))
            {
                if (item.Id == message.RecordId && item.OrderId != message.OrderId)
                {
                    item.OrderId = message.OrderId;
                    item.OnPropertyChanged(nameof(item.HasOrder));
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"void Receive(TransferRecordLinkOrderMessage message) {e}");
        }
    }

    public void Receive(EntityDeleted<RiskAssessment> message)
    {
        if (RiskAssessments.FirstOrDefault(x => x.Id == message.Value.Id) is RiskAssessmentViewModel r)
            RiskAssessments.Remove(r);
    }

    public void Receive(EntityDeleted message)
    {
        if (message.Type == typeof(RiskAssessment) && message.Id is int id && RiskAssessments.FirstOrDefault(x => x.Id == id) is RiskAssessmentViewModel r)
            RiskAssessments.Remove(r);
    }

    public void Receive(IEnumerable<LinkOrderMessage> message)
    {
        if (TransferRecords is null) return;
        try
        {

            foreach (var (confirm, link) in TransferRecords.Where(x => x.Records is not null).SelectMany(x => x.Records!).Join(message, x => x.Id, x => x.RecordId, (confirm, link) => (confirm, link)))
            {
                confirm.OrderId = link.OrderId;
                confirm.OnPropertyChanged(nameof(confirm.HasOrder));
                confirm.OnPropertyChanged(nameof(confirm.LackOrder));
            }

        }
        catch (Exception e)
        {
            Log.Error($"void Receive(TransferRecordLinkOrderMessage message) {e}");
        }
    }

    public partial class TransferRecordByFund : ObservableObject
    {
        public int FundId { get; set; }

        public string? FundName { get; set; }

        public decimal Asset { get; set; }



        public IEnumerable<TransferRecordViewModel>? Records { get; set; }

        public decimal Profit { get; internal set; }
        public string? FundCode { get; internal set; }

        [RelayCommand]
        public void OpenFund()
        {
            WeakReferenceMessenger.Default.Send(new OpenFundMessage(FundId));
        }
    }
}



//public partial class EntityDateEfficientViewModel<T> : UnitViewModel, IEntityViewModel<T> where T : notnull
//{
//    public PropertyViewModel<bool> LongTerm { get; } = new();

//    public PropertyViewModel<DateTime?> Begin { get; } = new();

//    public PropertyViewModel<DateTime?> End { get; } = new();

//    public override bool CanConfirm => HasUnsavedValue;

//    public override bool CanDelete => !HasUnsavedValue && (Begin.Old is not null || End.Old is not null || LongTerm.Old);

//    public override bool HasUnsavedValue => Begin.IsChanged || End.IsChanged || LongTerm.IsChanged;

//    public Func<T, DateEfficient>? InitFunc { get; set; }

//    public Action<T, DateEfficient>? UpdateFunc { get; set; }

//    public Action<T>? ClearFunc { get; set; }

//    public void Init(T param)
//    {
//        if (param is not null && InitFunc is not null)
//        {
//            var v = InitFunc(param);
//            Begin.Old = v.Begin.HasValue ? new DateTime(v.Begin.Value, default) : null;
//            Begin.New = v.Begin.HasValue ? new DateTime(v.Begin.Value, default) : null;
//            End.Old = v.End.HasValue ? new DateTime(v.End.Value, default) : null;
//            End.New = v.End.HasValue ? new DateTime(v.End.Value, default) : null;
//            LongTerm.Old = v.LongTerm;
//            LongTerm.New = v.LongTerm;
//        }
//        SubscribeChanges();
//    }

//    public void UpdateEntity(T obj)
//    {
//        if (obj is not null && UpdateFunc is not null && CanConfirm && BuildDateEfficient() is DateEfficient efficient)
//        {
//            UpdateFunc(obj, efficient);
//            Apply();
//        }
//    }

//    public void RemoveValue(T obj)
//    {
//        LongTerm.New = false;
//        LongTerm.Old = false;
//        Begin.New = null;
//        Begin.Old = null;
//        End.New = null;
//        End.Old = null;

//        if (obj is not null && ClearFunc is not null)
//            ClearFunc(obj);
//    }

//    public override string ToString()
//    {
//        return $"{Begin.New?.ToString("yyyy-MM-dd")}-{(LongTerm.New ? "长期" : End.New?.ToString("yyyy-MM-dd"))}";
//    }

//    private DateEfficient? BuildDateEfficient()
//    {
//        if (LongTerm.New)
//            return Begin.New is not null && Begin.New != default(DateTime) ? new DateEfficient { LongTerm = true, Begin = DateOnly.FromDateTime(Begin.New!.Value) } : null;
//        else
//            return Begin.New is not null && Begin.New != default(DateTime) && End.New is not null && End.New != default(DateTime) ? new DateEfficient { LongTerm = false, Begin = DateOnly.FromDateTime(Begin.New!.Value), End = DateOnly.FromDateTime(End.New!.Value) } : null;
//    }
//}

public partial class DateEfficientViewModel : ObservableObject, IEquatable<DateEfficientViewModel>
{

    [ObservableProperty]
    public partial DateOnly? Begin { get; set; }

    [ObservableProperty]
    public partial DateOnly? End { get; set; }

    [ObservableProperty]
    public partial bool IsLongTerm { get; set; }


    public DateEfficientViewModel() { }

    public DateEfficientViewModel(DateEfficient efficient)
    {
        Begin = efficient.Begin == default ? null : efficient.Begin;
        End = efficient.End == default || efficient.End == DateOnly.MaxValue ? null : efficient.End;
        IsLongTerm = efficient.LongTerm;
    }

    public DateEfficient Build() => new DateEfficient { LongTerm = IsLongTerm, Begin = Begin, End = End };

    public bool Equals(DateEfficientViewModel? other)
    {
        return Begin == other?.Begin && End == other?.End && IsLongTerm == other?.IsLongTerm;
    }

    public override string ToString()
    {
        if (Begin >= End) return string.Empty;

        return $"{Begin?.ToString("yyyy.MM.dd")}-{(IsLongTerm ? "长期" : End?.ToString("yyyy.MM.dd"))}";
    }
}


public partial class RiskAssessmentViewModel : ObservableObject
{
    public int Id { get; set; }

    public int InvestorId { get; set; }

    public DateOnly Date { get; set; }

    public RiskEvaluation Level { get; set; }



    public FileMeta? File { get; set; }

    public bool FileIsExists => File?.Exists ?? false;// string.IsNullOrWhiteSpace(Path) ? false : File.Exists(Path);

    public RiskAssessmentViewModel(RiskAssessment risk)
    {
        Id = risk.Id;
        InvestorId = risk.InvestorId;
        Date = risk.Date;
        Level = risk.Level;
      
        File = risk.File;
    }


    [RelayCommand(CanExecute = nameof(FileIsExists))]
    public void ViewFile()
    {
        if (File is null) return;

        try
        {
            Directory.CreateDirectory(@$"temp\{File.Id}");
            string tmp = @$"temp\{File.Id}\{File.Name}";

            if (!System.IO.File.Exists(tmp))
                FileMeta.CreateHardLink(@$"files\hardlink\{File.Id}", tmp);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tmp) { UseShellExecute = true });
        }
        catch (Exception e) { LogEx.Error(e); WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "风险调查问卷不存在")); }
    }

    [RelayCommand]
    public void Delete()
    {
        using var db = DbHelper.Base();
        db.GetCollection<RiskAssessment>().Delete(Id);
        WeakReferenceMessenger.Default.Send(new EntityDeleted(typeof(RiskAssessment), Id));
    }
}