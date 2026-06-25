using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vetting.Data;
using Vetting.Models.Entities;

namespace Vetting.ViewModel;

public partial class DataCenterViewModel : ObservableObject
{
    // 唯一项
    public ManagerViewModel ManagerVM { get; private set; } = null!;
    public CreditStandingViewModel CreditStandingVM { get; private set; } = null!;
    public InvestmentInfoViewModel InvestmentInfoVM { get; private set; } = null!;
    public RiskControlViewModel RiskControlVM { get; private set; } = null!;

    // 列表项
    [ObservableProperty] public partial StaffVM? SelectedStaff { get; set; }
    public ObservableCollection<StaffVM> Staffs { get; } = [];
    public ObservableCollection<ShareholderVM> Shareholders { get; } = [];
    public ObservableCollection<DepartmentVM> Departments { get; } = [];
    public ObservableCollection<StrategyVM> Strategies { get; } = [];
    public ObservableCollection<FundInfoVM> FundInfos { get; } = [];
    public ObservableCollection<AwardVM> Awards { get; } = [];
    public ObservableCollection<AUMVM> AUMs { get; } = [];
    public ObservableCollection<DrawdownRecordVM> DrawdownRecords { get; } = [];
    public ObservableCollection<FinancialStatementVM> FinancialStatements { get; } = [];
    public ObservableCollection<QAVM> QAs { get; } = [];

    public DataCenterViewModel() => LoadAll();

    private void LoadAll()
    {
        using var db = new VettingDbContext();
        ManagerVM = new ManagerViewModel(LoadOrInit(db.Managers));
        CreditStandingVM = new CreditStandingViewModel(LoadOrInit(db.CreditStandings));
        InvestmentInfoVM = new InvestmentInfoViewModel(LoadOrInit(db.InvestmentInfos));
        RiskControlVM = new RiskControlViewModel(LoadOrInit(db.RiskControls));

        LoadList(db.Staffs, Staffs, e => new StaffVM(e));
        LoadList(db.Shareholders, Shareholders, e => new ShareholderVM(e));
        LoadList(db.Departments, Departments, e => new DepartmentVM(e));
        LoadList(db.Strategies, Strategies, e => new StrategyVM(e));
        LoadList(db.FundInfos, FundInfos, e => new FundInfoVM(e));
        LoadList(db.Awards, Awards, e => new AwardVM(e));
        LoadList(db.AUMs, AUMs, e => new AUMVM(e));
        LoadList(db.DrawdownRecords, DrawdownRecords, e => new DrawdownRecordVM(e));
        LoadList(db.FinancialStatements, FinancialStatements, e => new FinancialStatementVM(e));
        LoadList(db.QA, QAs, e => new QAVM(e));
    }

    private static T LoadOrInit<T>(LiteDB.ILiteCollection<T> source) where T : new()
    {
        var item = source.FindById(1);
        if (item == null) { item = new T(); source.Upsert(item); }
        return item;
    }

    private static void LoadList<T, Tvm>(LiteDB.ILiteCollection<T> source, ObservableCollection<Tvm> target, Func<T, Tvm> wrap) where T : class
    {
        target.Clear();
        foreach (var item in source.FindAll()) target.Add(wrap(item));
    }

    [RelayCommand]
    private void DeleteStaff()
    {
        if (SelectedStaff == null) return;
        using var db = new VettingDbContext();
        db.DeleteEntity(typeof(Staff), SelectedStaff.Entity.Id);
        Staffs.Remove(SelectedStaff);
        SelectedStaff = null;
    }

    [RelayCommand]
    private void AddItem(string? category)
    {
        using var db = new VettingDbContext();
        switch (category)
        {
            case "人员":
                var newStaff = new Staff();
                db.Staffs.Insert(newStaff);
                var staffVm = new StaffVM(newStaff);
                Staffs.Add(staffVm);
                SelectedStaff = staffVm;
                break;
            case "股东": AddAndSave(db.Shareholders, Shareholders, e => new ShareholderVM(e), new Shareholder()); break;
            case "部门": AddAndSave(db.Departments, Departments, e => new DepartmentVM(e), new Department()); break;
            case "策略": AddAndSave(db.Strategies, Strategies, e => new StrategyVM(e), new Strategy()); break;
            case "产品": AddAndSave(db.FundInfos, FundInfos, e => new FundInfoVM(e), new FundInfo()); break;
            case "奖项": AddAndSave(db.Awards, Awards, e => new AwardVM(e), new Award()); break;
            case "规模": AddAndSave(db.AUMs, AUMs, e => new AUMVM(e), new AUM()); break;
            case "回撤": AddAndSave(db.DrawdownRecords, DrawdownRecords, e => new DrawdownRecordVM(e), new DrawdownRecord()); break;
            case "财务": AddAndSave(db.FinancialStatements, FinancialStatements, e => new FinancialStatementVM(e), new FinancialStatement()); break;
            case "问答": AddAndSave(db.QA, QAs, e => new QAVM(e), new QA()); break;
        }
    }

    private static void AddAndSave<T, Tvm>(LiteDB.ILiteCollection<T> table, ObservableCollection<Tvm> col, Func<T, Tvm> wrap, T item) where T : class, new()
    {
        table.Insert(item);
        col.Add(wrap(item));
    }
}
