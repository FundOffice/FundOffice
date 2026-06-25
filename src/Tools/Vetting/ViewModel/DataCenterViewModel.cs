using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vetting.Data;
using Vetting.Models.Entities;

namespace Vetting.ViewModel;

public partial class DataCenterViewModel : ObservableObject
{
    public static ObservableCollection<StaffVM>? AllStaff { get; private set; }
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

        LoadList(db.Staffs, Staffs, e => { var vm = new StaffVM(e); vm.InitRoles(); return vm; });
        AllStaff = Staffs;
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
    private async Task GenerateMockAsync()
    {
        if (HandyControl.Controls.MessageBox.Show("生成模拟数据会覆盖现有数据，确定继续？", "确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;

        await Task.Run(() =>
        {
            using var db = new VettingDbContext();

            // 单例
            db.UpsertEntity(new Manager
            {
                Id = 1, Name = "鼎丰资产管理有限公司", RegisterNo = "91310000MA1FL8X93Q", ArtificialPerson = "张伟",
                RegisterCapital = "5000", RealCapital = "3000", SetupDate = "2018-06-15", BusinessScope = "资产管理、投资管理",
                RegisterAddress = "上海市浦东新区陆家嘴环路1000号", OfficeAddress = "上海市浦东新区世纪大道88号金茂大厦32层",
                Phone = "13812345678", Telephone = "021-58889999", Email = "contact@dingfeng.com", Fax = "021-58889998",
                EnglishName = "DingFeng Asset Management Co., Ltd.", WebSite = "www.dingfeng-am.com", AmacId = "P1068852",
                Membership = MembershipLevel.普通会员, InvestmentAdvisor = true, InstitutionType = "私募证券",
                RelatedCompany = "鼎丰资本控股有限公司", ActualController = "张伟", ContactName = "李芳",
                ContactPhoneAndEmail = "13987654321 / lifang@dingfeng.com", GoverningSecuritiesBureau = "上海证监局"
            });

            db.UpsertEntity(new CreditStanding
            {
                Id = 1, AdminPenalty = "无", BusinessException = "无", SeriousIllegal = "无", ExecutionInfo = "无",
                SecuritiesDishonesty = "无", CorePersonDishonesty = "无", FundAssocCreditReport = "正常",
                AICQuery = "正常", CSRCQuery = "无异常", AssociationQuery = "正常", JudicialQuery = "无涉诉",
                AntiMoneyLaundering = "已建立反洗钱内控制度"
            });

            db.UpsertEntity(new InvestmentInfo
            {
                Id = 1, Target = "追求绝对收益，年化目标15%-20%",
                Philosophy = "基于基本面深度研究，结合量化模型筛选，寻找被市场低估的优质标的",
                Research = "行业研究+个股深度调研，覆盖消费、科技、医药三大赛道",
                Decision = "投资决策委员会每月召开例会，重大投资需三分之二委员同意",
                Trading = "集中交易制度，交易员执行指令，风控实时监控",
                RiskControl = "事前风控审批+事中实时监控+事后归因分析",
                PortfolioAdjust = "月度调仓为主，极端行情下可临时调仓",
                PositionBuilding = "分批建仓，单票仓位上限20%",
                CommitteeRole = "投资决策委员会负责重大投资决策和风险审批",
                ResearchAuthority = "研究员独立出具研究报告，投资经理参考决策",
                SystemAndData = "恒生O3.5投资管理系统+Wind数据终端",
                DataStorage = "数据存储于阿里云金融云，定期异地备份",
                TradingControl = "系统自动限制单笔委托金额和个股集中度",
                TradingErrorFix = "发现错误交易30分钟内启动应急处置流程",
                AbnormalTrading = "异常交易实时预警，风控经理即时介入",
                AccountFairness = "各账户公平对待，采用统一指令分配系统"
            });

            db.UpsertEntity(new RiskControl
            {
                Id = 1, SystemIntro = "三级风控体系：交易员自查→风控部监控→合规部审计",
                DecisionMechanism = "投决会+风控委员会双层决策机制",
                RiskMgmtCommittee = "风控委员会由CRO、CIO、合规总监组成",
                DrawdownControl = "产品净值回撤达8%预警，达12%强制减仓",
                SystemicRiskResponse = "对冲工具+仓位管控+流动性储备",
                TradingMonitoring = "恒生系统实时监控，每笔交易留痕",
                RiskMeasures = "VaR、压力测试、情景分析",
                ManualVsSystem = "系统为主、人工为辅，重大异常由人工复核",
                RiskMeasurement = "每日计算组合VaR，每周压力测试",
                MaxDrawdownTolerance = "最大回撤容忍度15%",
                TailRisk = "期权对冲尾部风险，黑天鹅事件启动应急预案",
                RiskReserve = "管理费收入的5%计提风险准备金",
                LiquidityMgmt = "保持10%以上现金类资产，应对赎回需求",
                InsiderTradingPrevention = "信息隔离墙制度+员工交易申报制度",
                EmployeeTradingMonitor = "员工个人证券交易需提前报备，禁止与产品同向交易",
                ProductFairness = "统一交易指令分配系统，确保各产品公平执行"
            });

            // 清空旧数据
            db.Staffs.DeleteAll();
            db.Shareholders.DeleteAll();
            db.Departments.DeleteAll();
            db.Strategies.DeleteAll();
            db.FundInfos.DeleteAll();
            db.Awards.DeleteAll();
            db.AUMs.DeleteAll();
            db.DrawdownRecords.DeleteAll();
            db.FinancialStatements.DeleteAll();
            db.QA.DeleteAll();

            // 人员
            var staffData = new[] { ("王明", "总经理", EducationLevel.硕士, StaffRole.高管, 1), ("刘洋", "投资总监", EducationLevel.硕士, StaffRole.投资经理, 1), ("陈静", "研究总监", EducationLevel.博士, StaffRole.投研, 2), ("赵强", "风控总监", EducationLevel.硕士, StaffRole.风控, 3), ("周慧", "合规经理", EducationLevel.本科, StaffRole.合规, 4), ("吴涛", "高级研究员", EducationLevel.硕士, StaffRole.投研, 2), ("孙丽", "交易主管", EducationLevel.本科, StaffRole.运营, 4) };
            foreach (var (name, title, edu, role, deptId) in staffData)
                db.Staffs.Insert(new Staff { Name = name, Title = title, Education = edu, Role = role, DepartmentId = deptId, Years = Random.Shared.Next(3, 15).ToString(), MobilePhone = $"138{Random.Shared.Next(10000000, 99999999)}", Email = $"{name}@dingfeng.com" });

            // 股东
            foreach (var (name, ratio, nature) in new[] { ("张伟", "45%", "自然人"), ("李芳", "25%", "自然人"), ("鼎丰资本控股有限公司", "30%", "法人") })
                db.Shareholders.Insert(new Shareholder { Name = name, Ratio = ratio, Nature = nature, IsActualController = name == "张伟" });

            // 部门
            foreach (var (name, head, func) in new[] { ("投资部", "刘洋", "投资决策与执行"), ("研究部", "陈静", "行业研究与个股分析"), ("风控部", "赵强", "风险监控与合规管理"), ("运营部", "周慧", "基金运营与信息披露") })
                db.Departments.Insert(new Department { Name = name, Head = head, MainFunction = func });

            // 策略
            foreach (var (name, type, scale) in new[] { ("主观多头", "股票多头", "5.2"), ("量化对冲", "市场中性", "3.8"), ("固收增强", "债券+", "8.5") })
                db.Strategies.Insert(new Strategy { Name = name, Type = type, Scale = scale, Manager = "刘洋", Capacity = "20" });

            // 产品
            foreach (var (n, c, t, s, nav, r, dd, rk) in new[] { ("鼎丰价值优选1号","DF001","股票多头","5.2","1.352","15.2%","-8.5%","R4"), ("鼎丰量化对冲1号","DF002","市场中性","3.8","1.186","9.8%","-3.2%","R3"), ("鼎丰固收增强1号","DF003","债券+","8.5","1.092","6.5%","-1.8%","R2"), ("鼎丰成长精选2号","DF004","股票多头","4.1","1.523","22.6%","-12.3%","R5"), ("鼎丰CTA趋势1号","DF005","管理期货","2.5","1.278","18.4%","-9.7%","R4"), ("鼎丰宏观策略1号","DF006","宏观策略","6.0","1.145","11.3%","-5.6%","R3"), ("鼎丰指数增强1号","DF007","指数增强","7.2","1.218","13.7%","-10.1%","R4"), ("鼎丰事件驱动1号","DF008","事件驱动","3.3","1.089","7.2%","-6.8%","R4"), ("鼎丰FOF配置1号","DF009","FOF","10.0","1.068","5.8%","-2.5%","R2"), ("鼎丰多策略1号","DF010","多策略","5.8","1.312","16.9%","-7.4%","R3") })
                db.FundInfos.Insert(new FundInfo { Name = n, Code = c, Type = t, StrategyType = t, Scale = s, UnitNav = nav, AnnualReturn = r, MaxDrawdown = dd, RiskLevel = rk });

            // 奖项
            foreach (var (time, name, ev) in new[] { ("2024", "金牛私募基金管理公司", "中国证券报"), ("2023", "最佳私募基金公司", "上海证券报"), ("2023", "五年期金牛私募投资经理", "中国证券报") })
                db.Awards.Insert(new Award { Time = time, Name = name, Evaluator = ev, Entity = "鼎丰资产管理有限公司" });

            // 规模
            foreach (var (year, scale) in new[] { ("2021", "12.5"), ("2022", "18.3"), ("2023", "25.6"), ("2024", "32.8"), ("2025", "38.5") })
                db.AUMs.Insert(new AUM { Year = year, Scale = scale });

            // 回撤
            db.DrawdownRecords.Insert(new DrawdownRecord { ProductName = "鼎丰价值优选1号", Date = "2022-04", Amplitude = "-12.3%", Reason = "市场系统性下跌", Countermeasures = "减仓至60%，增加对冲头寸", RecoveryDays = "45" });

            // 财报
            foreach (var year in new[] { "2022", "2023", "2024" })
                db.FinancialStatements.Insert(new FinancialStatement { Year = year, TotalAssets = $"{Random.Shared.Next(800, 1500)}", TotalLiabilities = $"{Random.Shared.Next(200, 400)}", OwnersEquity = $"{Random.Shared.Next(500, 1100)}", Revenue = $"{Random.Shared.Next(100, 300)}", Cost = $"{Random.Shared.Next(50, 150)}", NetProfit = $"{Random.Shared.Next(30, 120)}" });

            // 问答
            foreach (var (q, a) in new[] { ("公司核心投资理念是什么？", "基于深度基本面研究，寻找被低估的优质标的"), ("风控体系如何运作？", "三级风控：交易员自查→风控部监控→合规部审计"), ("投研团队构成？", "6名研究员覆盖消费、科技、医药三大赛道") })
                db.QA.Insert(new QA { Question = q, Answer = a });
        });

        // 刷新 UI
        LoadAll();
        HandyControl.Controls.Growl.Success("模拟数据已生成");
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
                staffVm.InitRoles();
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
