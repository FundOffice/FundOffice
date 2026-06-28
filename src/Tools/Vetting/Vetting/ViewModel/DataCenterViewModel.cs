using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;

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
    [ObservableProperty] public partial FinancialStatementVM? SelectedFinancialStatement { get; set; }
    [ObservableProperty] public partial object? CurrentItem { get; set; }
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

    // 全局推荐产品
    public ObservableCollection<FundInfoVM> GlobalRecommendedFunds { get; } = [];
    [ObservableProperty] public partial FundInfoVM? GlobalSelectedAvailable { get; set; }
    [ObservableProperty] public partial FundInfoVM? GlobalSelectedRecommended { get; set; }

    // 已有附件文件（files/vetting/pred/）
    public ObservableCollection<string> PredFileNames { get; } = [];
    [ObservableProperty] public partial string? SelectedPredFile { get; set; }
    public ObservableCollection<CommonFileVM> CommonFiles { get; } = [];

    private static readonly string[] CommonFileNames = BuildCommonFileNames();

    private static string[] BuildCommonFileNames()
    {
        var list = new List<string>
        {
            "营业执照正本", "营业执照副本",
            "管理人登记证明", "会员证书",
            "法定代表人身份证", "基金经理身份证",
            "基金从业资格证",
            "公司章程", "信用报告"
        };
        // 审计报告按年份，加最近 3 年
        var year = DateTime.Now.Year;
        for (int i = 0; i < 3; i++) list.Add($"审计报告_{year - i}");
        return list.ToArray();
    }

    public DataCenterViewModel()
    {
        foreach (var n in CommonFileNames) CommonFiles.Add(new CommonFileVM { Name = n });
        LoadAll();
        LoadPredFiles();
    }

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
        // 财报按年份从新到旧排列
        var sorted = FinancialStatements.OrderByDescending(f => f.Year).ToList();
        FinancialStatements.Clear();
        foreach (var f in sorted) FinancialStatements.Add(f);
        SelectedFinancialStatement = FinancialStatements.FirstOrDefault();
        LoadList(db.QA, QAs, e => new QAVM(e));

        // 加载全局推荐产品
        var rec = db.TemplateRecommends.FindOne(r => r.FileName == "__global__");
        if (rec?.FundIds != null)
        {
            var ids = rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
            foreach (var id in ids)
            {
                var fund = FundInfos.FirstOrDefault(f => f.Entity.Id == id);
                if (fund != null) GlobalRecommendedFunds.Add(fund);
            }
        }
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
    private void DeleteItem()
    {
        if (CurrentItem == null) return;
        var entity = CurrentItem.GetType().GetProperty("Entity")?.GetValue(CurrentItem);
        if (entity == null) return;
        var id = (int)(entity.GetType().GetProperty("Id")?.GetValue(entity) ?? 0);
        if (id <= 0) return;
        using var db = new VettingDbContext();
        db.DeleteEntity(entity.GetType(), id);
        FindCollection(CurrentItem)?.Remove(CurrentItem);
        CurrentItem = null;
    }

    [RelayCommand]
    private void ClearItems(string? collectionName)
    {
        if (string.IsNullOrEmpty(collectionName)) return;
        var col = FindCollectionByName(collectionName);
        if (col == null || col.Count == 0) return;
        if (HandyControl.Controls.MessageBox.Show($"确认清空全部 {col.Count} 条数据？", "确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
        using var db = new VettingDbContext();
        db.DropCollection(collectionName);
        col.Clear();
    }

    private System.Collections.IList? FindCollection(object item)
    {
        var t = item.GetType().Name;
        return t switch
        {
            nameof(StaffVM) => Staffs,
            nameof(ShareholderVM) => Shareholders,
            nameof(DepartmentVM) => Departments,
            nameof(StrategyVM) => Strategies,
            nameof(FundInfoVM) => FundInfos,
            nameof(AwardVM) => Awards,
            nameof(AUMVM) => AUMs,
            nameof(DrawdownRecordVM) => DrawdownRecords,
            nameof(FinancialStatementVM) => FinancialStatements,
            nameof(QAVM) => QAs,
            _ => null
        };
    }

    private System.Collections.IList? FindCollectionByName(string name) => name switch
    {
        "Staff" => Staffs,
        "Shareholder" => Shareholders,
        "Department" => Departments,
        "Strategy" => Strategies,
        "FundInfo" => FundInfos,
        "Award" => Awards,
        "AUM" => AUMs,
        "DrawdownRecord" => DrawdownRecords,
        "FinancialStatement" => FinancialStatements,
        "QA" => QAs,
        _ => null
    };

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
    private void DeleteFinancialStatement()
    {
        if (SelectedFinancialStatement == null) return;
        using var db = new VettingDbContext();
        db.DeleteEntity(typeof(FinancialStatement), SelectedFinancialStatement.Entity.Id);
        FinancialStatements.Remove(SelectedFinancialStatement);
        SelectedFinancialStatement = null;
    }

    [RelayCommand]
    private async Task GenerateMockAsync()
    {
        if (HandyControl.Controls.MessageBox.Show("生成模拟数据会覆盖现有数据，确定继续？", "确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;

        await Task.Run(() =>
        {
            using var db = new VettingDbContext();

            var r = Random.Shared;
            var surnames = new[] { "王","李","张","刘","陈","杨","赵","黄","周","吴","徐","孙","胡","朱","高","林","何","郭","马","罗","梁","宋","郑","谢","韩","唐","冯","于","董","萧" };
            var givenNames = new[] { "明","洋","静","强","慧","涛","丽","伟","芳","磊","军","平","刚","桂英","秀英","敏","娜","燕","华","建国","文","志强","海燕","小红","思远","天宇","雨晴","嘉欣","子轩","浩然" };
            string RandName() => surnames[r.Next(surnames.Length)] + givenNames[r.Next(givenNames.Length)];
            string RandPhone() => $"1{new[]{"38","39","58","59","86","87","36","37"}[r.Next(8)]}{r.Next(10000000,99999999)}";
            DateTime RandBirth(int lo, int hi) => new(lo + r.Next(hi - lo), r.Next(1, 13), r.Next(1, 28));

            // 单例
            db.UpsertEntity(new Manager
            {
                Id = 1, Name = "鼎丰资产管理有限公司", RegisterNo = "91310000MA1FL8X93Q", ArtificialPerson = "张伟",
                RegisterCapital = "5000", RealCapital = "3000", SetupDate = "2018-06-15",
                BusinessScope = "私募证券投资基金管理、投资管理、资产管理",
                RegisterAddress = "上海市浦东新区陆家嘴环路1000号恒生银行大厦18层",
                OfficeAddress = "上海市浦东新区世纪大道88号金茂大厦32层",
                Phone = RandPhone(), Telephone = "021-58889999", Email = "contact@dingfeng-am.com",
                Fax = "021-58889998", EnglishName = "DingFeng Asset Management Co., Ltd.",
                WebSite = "www.dingfeng-am.com", AmacId = "P1068852",
                Membership = MembershipLevel.普通会员, InvestmentAdvisor = true,
                InstitutionType = "私募证券投资基金管理人",
                RelatedCompany = "鼎丰资本控股有限公司、鼎丰财富管理有限公司",
                ActualController = "张伟", ContactName = "李芳",
                ContactPhoneAndEmail = $"{RandPhone()} / lifang@dingfeng-am.com",
                GoverningSecuritiesBureau = "上海证监局",
                Description = "鼎丰资产管理有限公司成立于2018年，是一家专注于二级市场投资的私募基金管理公司。公司秉承'深度研究、价值发现'的投资理念，致力于为高净值客户和机构投资者创造长期稳定的绝对收益。",
                HistoricalEvolution = "2018年6月 公司成立，注册资本5000万元\n2019年3月 完成基金业协会登记（P1068852）\n2020年 管理规模突破10亿元\n2022年 获得基金业协会普通会员资格\n2024年 管理规模突破30亿元",
                OrgStructureIntro = "公司设有投资决策委员会、研究部、投资部、风控部、运营部。投资决策委员会为最高投资决策机构，由CIO、CRO及核心投资经理组成。",
                FutureStrategicPlan = "未来三年计划拓展量化策略线，引入CTA和宏观策略，目标管理规模突破50亿元。同时加强合规风控体系建设，申请投顾资质。"
            });

            db.UpsertEntity(new CreditStanding
            {
                Id = 1,
                AdminPenalty = "无行政处罚记录",
                BusinessException = "无经营异常信息",
                SeriousIllegal = "无严重违法失信记录",
                ExecutionInfo = "无被执行人信息",
                SecuritiesDishonesty = "无证券期货市场失信记录",
                CorePersonDishonesty = "核心人员无失信被执行人记录",
                FundAssocCreditReport = "基金业协会信用信息报告正常，无负面信息",
                AICQuery = "工商登记信息正常，无异常经营",
                CSRCQuery = "证监会行政许可及处罚查询无异常",
                AssociationQuery = "基金业协会信息公示系统查询正常",
                JudicialQuery = "中国裁判文书网查询无涉诉记录",
                AntiMoneyLaundering = "已建立反洗钱内部控制制度，配备专职反洗钱岗，定期报送可疑交易报告"
            });

            db.UpsertEntity(new InvestmentInfo
            {
                Id = 1,
                Target = "追求长期绝对收益，年化目标收益率15%-20%，最大回撤控制在15%以内",
                Philosophy = "基于深度基本面研究，结合量化模型辅助筛选，在消费、科技、医药三大核心赛道中寻找被市场低估的优质企业，通过长期持有获取企业成长带来的价值增值。",
                Research = "采用'自上而下'与'自下而上'相结合的研究框架。宏观层面关注货币政策、产业政策；中观层面筛选景气度向上的行业；微观层面通过实地调研、产业链验证精选个股。覆盖消费、科技、医药三大核心赛道。",
                Decision = "投资决策委员会每月召开例会审议投资策略和组合调整方案。单只个股投资需研究员出具深度报告并经投资经理推荐，投决会三分之二以上委员同意方可执行。超过组合净值5%的单笔投资需CRO会签。",
                Trading = "采用集中交易制度，由交易部统一执行投资经理下达的交易指令。交易指令通过恒生O3.5系统下达，交易员实时执行，执行结果实时反馈。大额交易采用TWAP/VWAP算法拆分执行。",
                RiskControl = "事前：投资范围限制、个股集中度限制、行业集中度限制；事中：实时监控组合风险指标、异常交易预警；事后：每日归因分析、每周压力测试、每月风险报告。",
                PortfolioAdjust = "以月度调仓为主，根据市场环境和个股基本面变化动态调整。极端行情（如单日跌幅超3%）可启动临时调仓机制。调仓需经投资经理审批，超过10%仓位变动需投决会审批。",
                PositionBuilding = "新建仓标的采用分批建仓策略，首次建仓不超过目标仓位的30%，根据市场走势和基本面验证逐步加仓。单票仓位上限20%，前十大重仓合计不超过60%。",
                CommitteeRole = "投资决策委员会为公司最高投资决策机构，负责审议年度投资策略、重大投资决策、风控政策制定。委员会由CIO（张伟）担任主席，CRO（赵强）、研究总监（陈静）、核心投资经理为委员。",
                ResearchAuthority = "研究员独立出具行业研究报告和个股深度报告，报告需经研究总监审核后发布。投资经理根据研究报告自主做出投资决策，但需遵守投资范围和集中度限制。",
                SystemAndData = "投资管理：恒生O3.5投资管理系统；研究平台：Wind金融终端、iFind；交易执行：券商PB系统；风控系统：恒生风控模块+自研预警模型",
                DataStorage = "核心数据存储于阿里云金融专区，采用同城双活+异地灾备架构。交易数据保留20年，通讯记录保留3年，符合监管要求。",
                TradingControl = "系统自动限制：单笔委托金额不超过产品净值的5%；单日买卖同一证券金额不超过产品净值的10%；单只个股持仓不超过产品净值的20%。超出限制需CRO审批。",
                TradingErrorFix = "发现错误交易后30分钟内启动应急处置：1)立即停止相关交易；2)评估损失范围；3)制定纠错方案（对冲/平仓）；4)CRO审批后执行；5)事后出具差错报告并完善内控。",
                AbnormalTrading = "系统实时监控异常交易行为（频繁撤单、对敲对倒、尾盘异动等），触发预警后风控经理即时介入核查，必要时暂停相关账户交易权限。",
                AccountFairness = "采用恒生指令分配系统实现多账户公平交易。同一投资经理管理的多个产品，买卖同一证券时采用按比例分配原则，确保各产品获得相同的执行价格和数量比例。"
            });

            db.UpsertEntity(new RiskControl
            {
                Id = 1,
                SystemIntro = "公司建立了三级风控体系：第一级-交易员自查与合规审查；第二级-风控部独立监控与预警；第三级-合规部定期审计与外部评估。三道防线相互独立、逐级强化。",
                DecisionMechanism = "双层决策机制：投资层面-投决会负责投资策略和重大投资决策；风控层面-风控委员会负责风险政策、止损审批、合规审查。两个委员会人员部分重叠但投票独立。",
                RiskMgmtCommittee = "风控委员会由首席风控官（CRO）赵强担任主席，成员包括CIO张伟、合规总监周慧、风控经理。每月召开风控例会，审议风险报告、压力测试结果和风控政策调整。",
                DrawdownControl = "预警线：产品净值回撤达8%时系统自动预警，投资经理需在24小时内出具回撤分析报告；止损线：回撤达12%时强制减仓至半仓以下；清盘线：回撤达20%时启动产品清盘程序。",
                SystemicRiskResponse = "应对系统性风险的三层防线：1)对冲工具（股指期货、期权）对冲Beta风险；2)仓位管控（极端行情下总仓位降至50%以下）；3)流动性储备（保持15%以上现金及高流动性资产）。",
                TradingMonitoring = "恒生系统实时监控所有交易指令和成交记录。监控指标包括：个股集中度、行业集中度、换手率异常、频繁撤单、尾盘交易等。异常情况实时推送至风控经理终端。",
                RiskMeasures = "主要风险度量工具：1)VaR（99%置信度，10日持有期）；2)压力测试（历史情景：2015股灾、2018贸易战、2020疫情）；3)情景分析（利率冲击、汇率波动等）；4)敏感性分析（久期、Beta等）。",
                ManualVsSystem = "系统为主（80%风控指标自动监控）、人工为辅（20%需人工判断的例外事项）。系统预警分级处理：绿色自动记录、黄色风控经理复核、红色CRO审批。",
                RiskMeasurement = "每日计算组合VaR、跟踪误差、Beta、行业偏离度等风险指标，每周进行压力测试，每月出具综合风险报告提交投决会审议。",
                MaxDrawdownTolerance = "各产品最大回撤容忍度：股票多头策略15%、量化对冲策略5%、固收增强策略3%。超过容忍度需启动止损程序并报告CRO。",
                TailRisk = "通过期权组合（保护性看跌期权）对冲尾部风险。黑天鹅事件应急预案：1)立即评估敞口；2)启动对冲头寸；3)减仓至安全水平；4)启动投资者沟通机制。",
                RiskReserve = "按管理费收入的5%计提风险准备金，累计达到产品规模的1%后不再计提。风险准备金专户管理，用于弥补因管理人过错导致的投资者损失。",
                LiquidityMgmt = "保持10%以上现金及货币基金等高流动性资产。对持仓证券进行流动性分级评估（A/B/C级），C级资产合计不超过10%。大额赎回（超过产品规模10%）需提前5个工作日预约。",
                InsiderTradingPrevention = "信息隔离墙制度：研究部与交易部物理隔离，敏感信息分级管理。内幕信息知情人登记制度：知情人及其近亲属证券账户报备，敏感期禁止交易。",
                EmployeeTradingMonitor = "员工个人证券交易需提前3个工作日报备，审批通过后方可执行。禁止与公司产品同方向交易同一证券。员工证券账户每季度申报，由合规部统一核查。",
                ProductFairness = "统一交易指令分配系统确保各产品公平执行。同一投资经理管理的产品，买卖同一证券时按产品规模比例分配。禁止利益输送和不公平对待投资者。"
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
            var roles = new[] { StaffRole.高管, StaffRole.投资经理, StaffRole.投研, StaffRole.风控, StaffRole.合规, StaffRole.运营, StaffRole.投研|StaffRole.投资经理 };
            var titles = new[] { "总经理", "投资总监", "研究总监", "风控总监", "合规经理", "高级研究员", "交易主管", "投资经理", "行业研究员", "风控经理" };
            var edus = new[] { EducationLevel.博士, EducationLevel.硕士, EducationLevel.MBA, EducationLevel.本科 };
            var specialties = new[] { "消费行业", "科技行业", "医药行业", "TMT", "新能源", "大金融", "先进制造", "量化策略" };
            var profiles = new[] {
                "10年证券从业经验，曾任某大型公募基金研究员，专注消费行业研究，覆盖食品饮料、家电等子行业。",
                "CFA持证人，8年投资研究经验，擅长宏观经济分析和大类资产配置，曾任券商研究所策略分析师。",
                "清华大学金融学博士，6年量化投资经验，精通多因子模型和机器学习选股策略。",
                "注册会计师，12年风控经验，曾任某信托公司风控部总经理，熟悉各类金融产品风控体系。",
                "复旦大学法学硕士，持有法律职业资格证，8年基金合规管理经验。"
            };
            var staffData2 = new[] {
                ("王明", "总经理", EducationLevel.MBA, StaffRole.高管, 1, "公司管理、战略规划", "1980"),
                ("刘洋", "投资总监", EducationLevel.硕士, StaffRole.投资经理|StaffRole.高管, 1, "价值投资、消费行业", "1983"),
                ("陈静", "研究总监", EducationLevel.博士, StaffRole.投研, 2, "TMT、半导体", "1985"),
                ("赵强", "风控总监", EducationLevel.硕士, StaffRole.风控, 3, "信用风险、市场风险", "1982"),
                ("周慧", "合规经理", EducationLevel.硕士, StaffRole.合规, 4, "基金合规、信息披露", "1988"),
                (RandName(), "高级研究员", EducationLevel.博士, StaffRole.投研, 2, "医药行业、创新药", "1990"),
                (RandName(), "交易主管", EducationLevel.本科, StaffRole.运营, 4, "交易执行、算法交易", "1992"),
                (RandName(), "投资经理", EducationLevel.硕士, StaffRole.投资经理, 1, "量化对冲、CTA策略", "1987"),
                (RandName(), "行业研究员", EducationLevel.硕士, StaffRole.投研, 2, "新能源、光伏", "1991"),
                (RandName(), "风控经理", EducationLevel.本科, StaffRole.风控, 3, "操作风险、合规审查", "1993"),
                (RandName(), "研究员", EducationLevel.硕士, StaffRole.投研, 2, "大金融、银行保险", "1994"),
                (RandName(), "投资经理", EducationLevel.MBA, StaffRole.投资经理, 1, "宏观策略、大类资产配置", "1986"),
                (RandName(), "运营专员", EducationLevel.本科, StaffRole.运营, 4, "基金估值、信息披露", "1995"),
                (RandName(), "合规专员", EducationLevel.本科, StaffRole.合规, 4, "反洗钱、投资者适当性", "1993"),
                (RandName(), "研究员", EducationLevel.博士, StaffRole.投研, 2, "先进制造、军工", "1989"),
            };
            foreach (var (name, title, edu, role, deptId, spec, birthYear) in staffData2)
                db.Staffs.Insert(new Staff
                {
                    Name = name, Title = title, Education = edu, Role = role, DepartmentId = deptId,
                    Years = r.Next(3, 18).ToString(), BirthDate = RandBirth(int.Parse(birthYear), int.Parse(birthYear) + 3),
                    Specialty = spec, ResearchFocus = spec, MobilePhone = RandPhone(), Telephone = $"021-{r.Next(5000,6000)}{r.Next(1000,9999)}",
                    Email = $"{name}@dingfeng-am.com", Profile = profiles[r.Next(profiles.Length)],
                    IdNumber = $"310115{r.Next(1970,2000)}{r.Next(1,13):D2}{r.Next(1,29):D2}{r.Next(1000,9999)}"
                });

            // 股东
            foreach (var (name, ratio, nature, intro, paid, identity, role2, pos) in new[] {
                ("张伟", "45%", "自然人", "公司创始人，实际控制人。清华大学MBA，20年证券从业经验，曾任某知名私募基金投资总监。", "2250万元", "中国籍，无境外永久居留权", "董事长兼CIO", "全面负责公司战略和投资决策"),
                ("李芳", "25%", "自然人", "公司联合创始人，负责运营和市场。复旦大学金融学硕士，15年金融行业从业经验。", "1250万元", "中国籍，无境外永久居留权", "董事兼COO", "负责公司运营管理、市场拓展和客户服务"),
                ("鼎丰资本控股有限公司", "30%", "法人", "控股股东，成立于2015年，注册资本1亿元，主营业务为股权投资和资产管理。", "1500万元", "统一社会信用代码：91310000MA1FL5XX8K", "控股股东", "战略投资与资本运作")
            })
                db.Shareholders.Insert(new Shareholder { Name = name, Ratio = ratio, Nature = nature, Intro = intro, PaidInAmount = paid, IdentityBrief = identity, CompanyRole = role2, CompanyPosition = pos, IsActualController = name == "张伟" });

            // 部门
            foreach (var (name, head, func) in new[] {
                ("投资部", "刘洋", "负责投资决策执行、投资组合管理和交易执行。管理投资经理团队，监督各产品投资运作。"),
                ("研究部", "陈静", "负责宏观经济研究、行业研究和个股研究。出具研究报告，为投资决策提供研究支持。"),
                ("风控部", "赵强", "负责公司全面风险管理，包括市场风险、信用风险、操作风险的识别、评估、监控和报告。"),
                ("运营部", "周慧", "负责基金运营、信息披露、投资者服务、合规管理和行政事务。")
            })
                db.Departments.Insert(new Department { Name = name, Head = head, MainFunction = func });

            // 策略
            foreach (var (name, type, scale, cap, factor, capRisk, repl, style, turn, hold, weight, ws) in new[] {
                ("主观多头", "股票多头", "5.2", "20亿", "基本面+行业景气度", "容量充足，当前规模远低于容量上限", "策略可复制性较强，依赖核心投研团队", "偏成长风格，重仓消费和科技", "年化换手率约300%", "平均持仓3-6个月", "前十大重仓占比约55%", "预警-8%，止损-15%"),
                ("量化对冲", "市场中性", "3.8", "10亿", "多因子模型（价值/动量/质量/波动率）", "策略容量有限，超额收益随规模增长递减", "模型标准化程度高，可复制性强", "市场中性，无明显风格暴露", "年化换手率约800%", "持仓周期1-4周", "行业中性，个股权重上限2%", "预警-3%，止损-5%"),
                ("固收增强", "债券+", "8.5", "30亿", "信用分析+利率择时+转债增强", "容量充足，固收策略规模效应明显", "策略标准化，可高度复制", "偏稳健，以高等级信用债为主", "年化换手率约150%", "债券持仓1-3年，股票部分3-6个月", "债券80%+股票20%", "预警-2%，止损-3%")
            })
                db.Strategies.Insert(new Strategy { Name = name, Type = type, Scale = scale, Manager = "刘洋", Capacity = cap, FactorPool = factor, CapacityAndRisk = capRisk, Replicated = repl, StyleExposure = style, Turnover = turn, HoldingPeriod = hold, WeightAllocation = weight, WarningStoploss = ws });

            // 产品
            foreach (var (n, c, t, s, nav, ret, dd, rk, dur, freq, cust, mfee, sfee, scope, est, sharpe, cumRet, vol) in new[] {
                ("鼎丰价值优选1号","DF001","股票多头","5.2","1.352","15.2%","-8.5%","R4","无固定期限","月度开放","招商证券","1.5%","开放期申购1%，持有满1年赎回免费","沪深A股、港股通","2019-03-15","1.25","35.2%","12.8%"),
                ("鼎丰量化对冲1号","DF002","市场中性","3.8","1.186","9.8%","-3.2%","R3","无固定期限","月度开放","中信证券","1.5%","申购1%，赎回0.5%","沪深A股+股指期货对冲","2020-06-20","1.82","18.6%","5.2%"),
                ("鼎丰固收增强1号","DF003","债券+","8.5","1.092","6.5%","-1.8%","R2","无固定期限","季度开放","国泰君安","0.8%","申购0.5%，赎回0.3%","利率债、信用债、可转债","2019-09-01","2.15","9.2%","3.1%"),
                ("鼎丰成长精选2号","DF004","股票多头","4.1","1.523","22.6%","-12.3%","R5","无固定期限","月度开放","海通证券","2.0%","申购1.5%，持有满2年赎回免费","沪深A股、科创板","2020-01-10","1.08","52.3%","18.5%"),
                ("鼎丰CTA趋势1号","DF005","管理期货","2.5","1.278","18.4%","-9.7%","R4","无固定期限","月度开放","中信建投","1.5%","申购1%，赎回0.5%","商品期货、金融期货","2021-03-08","1.35","27.8%","14.2%"),
                ("鼎丰宏观策略1号","DF006","宏观策略","6.0","1.145","11.3%","-5.6%","R3","无固定期限","季度开放","华泰证券","1.2%","申购0.8%，赎回0.5%","股票、债券、商品、外汇","2020-08-15","1.52","14.5%","7.8%"),
                ("鼎丰指数增强1号","DF007","指数增强","7.2","1.218","13.7%","-10.1%","R4","无固定期限","月度开放","广发证券","1.0%","申购0.5%，赎回0.3%","沪深300成分股+股指期货","2019-12-01","1.18","21.8%","15.3%"),
                ("鼎丰事件驱动1号","DF008","事件驱动","3.3","1.089","7.2%","-6.8%","R4","2年","封闭期后季度开放","申万宏源","1.5%","封闭期内不可赎回","沪深A股（并购重组、定增等）","2022-06-01","0.95","8.9%","11.5%"),
                ("鼎丰FOF配置1号","DF009","FOF","10.0","1.068","5.8%","-2.5%","R2","无固定期限","季度开放","国信证券","0.6%","申购0.3%，赎回0.2%","公募基金、私募基金","2021-01-15","1.68","6.8%","3.8%"),
                ("鼎丰多策略1号","DF010","多策略","5.8","1.312","16.9%","-7.4%","R3","无固定期限","月度开放","东方证券","1.5%","申购1%，持有满1年赎回免费","股票+期货+期权多策略","2020-05-01","1.42","31.2%","10.5%")
            })
                db.FundInfos.Insert(new FundInfo { Name = n, Code = c, Type = t, StrategyType = t, Scale = s, UnitNav = nav, AnnualReturn = ret, MaxDrawdown = dd, RiskLevel = rk, Duration = dur, Frequency = freq, Custodian = cust, MgmtFee = mfee, BuySellFee = sfee, Scope = scope, EstablishmentDate = est, Sharpe = sharpe, CumulativeReturn = cumRet, Volatility = vol });

            // 奖项
            foreach (var (time, name, ev, ent) in new[] {
                ("2024", "金牛私募基金管理公司（五年期）", "中国证券报", "鼎丰资产管理有限公司"),
                ("2024", "年度最佳私募基金公司", "证券时报", "鼎丰资产管理有限公司"),
                ("2023", "金牛私募投资经理（三年期）", "中国证券报", "刘洋"),
                ("2023", "最佳私募基金风控团队", "上海证券报", "鼎丰资产管理有限公司"),
                ("2022", "英华奖·最佳私募基金公司", "中国基金报", "鼎丰资产管理有限公司")
            })
                db.Awards.Insert(new Award { Time = time, Name = name, Evaluator = ev, Entity = ent });

            // 规模
            foreach (var (year, scale) in new[] { ("2019", "3.2"), ("2020", "8.7"), ("2021", "15.3"), ("2022", "22.1"), ("2023", "28.6"), ("2024", "35.2"), ("2025", "38.5") })
                db.AUMs.Insert(new AUM { Year = year, Scale = scale });

            // 回撤
            foreach (var (pn, dt, amp, reason, counter, days) in new[] {
                ("鼎丰价值优选1号", "2022-04", "-12.3%", "市场系统性下跌，俄乌冲突引发全球风险偏好急剧下降", "减仓至60%，增加股指期货空头对冲，增持防御性板块", "45"),
                ("鼎丰成长精选2号", "2022-04", "-18.5%", "成长股大幅回调，科技板块领跌", "大幅减仓科技股，增配价值股和高股息标的", "62"),
                ("鼎丰CTA趋势1号", "2023-08", "-9.7%", "商品市场震荡加剧，趋势策略反复止损", "降低仓位至50%，缩短持仓周期，增加均值回归策略权重", "28"),
                ("鼎丰指数增强1号", "2024-02", "-10.1%", "小微盘股流动性危机，量化策略集体回撤", "暂停小市值因子暴露，增配大盘蓝筹对冲", "15")
            })
                db.DrawdownRecords.Insert(new DrawdownRecord { ProductName = pn, Date = dt, Amplitude = amp, Reason = reason, Countermeasures = counter, RecoveryDays = days });

            // 财报
            foreach (var (year, ta, tl, oe, rev, cost, gp, op, tp, tax, np, ocf, icf, fcf, cash, alr, gm, nm) in new[] {
                ("2022", "1280", "320", "960", "185", "98", "87", "62", "70", "8", "62", "58", "-30", "-10", "120", "25.0%", "47.0%", "33.5%"),
                ("2023", "1520", "380", "1140", "245", "125", "120", "88", "98", "10", "88", "75", "-35", "5", "150", "25.0%", "49.0%", "35.9%"),
                ("2024", "1850", "420", "1430", "310", "155", "155", "118", "130", "12", "118", "95", "-40", "20", "180", "22.7%", "50.0%", "38.1%")
            })
                db.FinancialStatements.Insert(new FinancialStatement { Year = year, TotalAssets = ta, TotalLiabilities = tl, OwnersEquity = oe, Revenue = rev, OperatingCost = cost, GrossProfit = gp, OperatingProfit = op, TotalProfit = tp, IncomeTax = tax, NetProfit = np, OperatingCashFlow = ocf, InvestingCashFlow = icf, FinancingCashFlow = fcf, CashEquivalents = cash, AssetLiabilityRatio = alr, GrossMargin = gm, NetMargin = nm });

            // 问答
            foreach (var (q, a) in new[] {
                ("公司核心投资理念是什么？", "基于深度基本面研究，结合量化模型辅助筛选，在消费、科技、医药三大核心赛道中寻找被市场低估的优质企业，追求长期绝对收益。"),
                ("风控体系如何运作？", "三级风控体系：交易员自查→风控部独立监控→合规部定期审计。产品净值回撤达8%预警、12%强制减仓、20%清盘。"),
                ("投研团队构成？", "研究部6名研究员覆盖消费、科技、医药、新能源、大金融、量化策略六大方向，投资部4名投资经理管理10只产品。"),
                ("公司历史业绩如何？", "自2019年首只产品成立以来，主观多头策略年化收益15.2%，量化对冲策略年化收益9.8%，固收增强策略年化收益6.5%，均跑赢同期基准。"),
                ("如何保证各产品公平对待？", "采用恒生指令分配系统，同一投资经理管理的产品买卖同一证券时按规模比例分配，禁止利益输送。"),
                ("公司的竞争优势是什么？", "1)核心团队稳定，平均从业经验超过10年；2)投研体系完善，覆盖三大核心赛道；3)风控体系严格，历史最大回撤控制在预期范围内。"),
                ("不同投资策略的权重分配？", "主观多头占比35%，量化对冲占比20%，固收增强占比30%，CTA及多策略占比15%。根据市场环境动态调整。"),
                ("投资策略的预警止损管理措施？", "各策略设有独立预警线和止损线：股票多头预警-8%止损-15%，量化对冲预警-3%止损-5%，固收增强预警-2%止损-3%。触发预警后投资经理24小时内出具分析报告。"),
                ("研究流程是怎样的？", "宏观筛选行业→行业研究员深度覆盖→实地调研验证→出具研究报告→研究总监审核→入库备选。覆盖消费、科技、医药、新能源、大金融五大方向。"),
                ("决策流程是怎样的？", "研究员出具深度报告→投资经理评估并推荐→投资决策委员会审议→三分之二以上委员同意方可执行。超过组合净值5%的单笔投资需CRO会签。"),
                ("交易流程是怎样的？", "投资经理通过恒生O3.5系统下达交易指令→交易部统一执行→执行结果实时反馈→风控实时监控。大额交易采用TWAP/VWAP算法拆分执行。"),
                ("评估流程是怎样的？", "每日：组合收益归因分析；每周：压力测试与风险评估；每月：投资策略回顾与绩效归因；每季度：投资经理述职与策略有效性评估。"),
                ("风控流程是怎样的？", "事前：投资范围与集中度限制审批；事中：实时监控组合VaR、行业偏离度、个股集中度等指标；事后：每日风险报告、每周压力测试、每月综合风险评估提交投决会。"),
                ("组合调整流程是怎样的？", "月度调仓为主：投资经理提出调仓方案→投决会审议→交易部执行。极端行情（单日跌幅超3%）可启动临时调仓机制，需CRO审批。"),
                ("建仓过程仓位情况？", "新建仓标的采用分批建仓：首次建仓不超过目标仓位30%，根据市场走势和基本面验证逐步加仓。单票仓位上限20%，前十大重仓合计不超过60%。"),
                ("投资决策委员会的职责是什么？", "投决会为公司最高投资决策机构，负责审议年度投资策略、重大投资决策、风控政策制定。由CIO张伟任主席，CRO赵强、研究总监陈静及核心投资经理为委员。"),
                ("内部研究与外部研究、投委会对投资结果的权限或影响？", "研究员独立出具研究报告，投资经理根据报告自主决策，但需遵守投资范围和集中度限制。投决会负责重大投资审批和策略方向把控。外部研究仅作参考，不直接作为投资依据。"),
                ("投研使用系统情况、数据库及交易数据管理？", "投资管理：恒生O3.5；研究平台：Wind金融终端、iFind；交易执行：券商PB系统；风控系统：恒生风控模块+自研预警模型。交易数据保留20年，通讯记录保留3年。"),
                ("策略、交易、持仓数据及其历史数据的存储和管理机制？", "核心数据存储于阿里云金融专区，采用同城双活+异地灾备架构。策略参数、交易记录、持仓数据实时同步备份，历史数据按月归档，保留期限符合监管要求。"),
                ("管理人在具体交易过程中的操作流程和控制管理办法？", "交易授权机制：投资经理下达指令→交易员执行→风控实时监控。单笔委托金额不超过产品净值5%，单日买卖同一证券不超过产品净值10%。超出限制需CRO审批。"),
                ("针对交易过程中出现的交易错误的解决办法？", "发现错误交易后30分钟内启动应急处置：1)立即停止相关交易；2)评估损失范围；3)制定纠错方案（对冲/平仓）；4)CRO审批后执行；5)事后出具差错报告并完善内控。"),
                ("管理人是否建立异常交易管理机制？", "系统实时监控异常交易行为（频繁撤单、对敲对倒、尾盘异动等），触发预警后风控经理即时介入核查，必要时暂停相关账户交易权限。同向交易按比例分配，禁止反向交易。"),
                ("管理人对多个资金账户间交易公平性的保障？", "采用恒生指令分配系统实现多账户公平交易。同一投资经理管理的多个产品，买卖同一证券时按产品规模比例分配，确保各产品获得相同的执行价格和数量比例。"),
                ("累计发行或管理产品数量？", "自2019年成立以来，累计发行产品12只，其中3只已到期清算，当前存续产品10只。"),
                ("当前管理产品数量？", "当前管理产品10只，覆盖股票多头、量化对冲、固收增强、管理期货、宏观策略、指数增强、事件驱动、FOF、多策略等类型。"),
                ("管理人已发行产品的历史最大回撤？", "历史最大回撤出现在2022年4月，鼎丰成长精选2号回撤-18.5%，主要因成长股系统性下跌。通过减仓和对冲，62个交易日内修复。"),
                ("管理人已发行产品是否有违法违规记录？", "无任何违法违规记录。所有产品均按时完成信息披露、净值报送、定期报告等合规义务。"),
                ("管理人已发行产品的业绩报酬计提方式？", "业绩报酬按高水位法计提，计提比例为超额收益的20%。封闭期内按季度计提，开放期按赎回时计提。"),
                ("风险准备金制度及其运行机制？", "按管理费收入的5%计提风险准备金，累计达到产品规模的1%后不再计提。专户管理，用于弥补因管理人过错导致的投资者损失。"),
                ("风险管理委员会及其工作机制？", "风控委员会由CRO赵强任主席，成员包括CIO张伟、合规总监周慧、风控经理。每月召开风控例会，审议风险报告、压力测试结果和风控政策调整。"),
                ("产品净值回撤控制机制？", "预警线：产品净值回撤达8%时系统自动预警，投资经理需24小时内出具回撤分析报告；止损线：回撤达12%强制减仓至半仓以下；清盘线：回撤达20%启动产品清盘程序。"),
                ("系统性风险、重仓品种重大风险的应对机制？", "三层防线：1)对冲工具（股指期货、期权）对冲Beta风险；2)仓位管控（极端行情下总仓位降至50%以下）；3)流动性储备（保持15%以上现金及高流动性资产）。"),
                ("产品赎回时的流动性管理？", "保持10%以上现金及货币基金等高流动性资产。持仓证券进行流动性分级评估（A/B/C级），C级资产合计不超过10%。大额赎回（超过产品规模10%）需提前5个工作日预约。"),
                ("管理人对内幕交易、操纵市场、老鼠仓等违法行为的防范措施？", "信息隔离墙制度：研究部与交易部物理隔离，敏感信息分级管理。内幕信息知情人登记制度：知情人及其近亲属证券账户报备，敏感期禁止交易。"),
                ("管理人是否建立员工投资行为监控或备案机制？", "员工个人证券交易需提前3个工作日报备，审批通过后方可执行。禁止与公司产品同方向交易同一证券。员工证券账户每季度申报，由合规部统一核查。"),
                ("员工个人账户交易管理？", "员工个人证券账户需向合规部报备，交易前3个工作日提交申请。禁止与公司产品同向交易，敏感期禁止交易。违规者将受到纪律处分。"),
                ("交易流程、监控核准程序？", "投资经理通过恒生系统下达指令→交易部确认执行→风控实时监控→合规定期审计。所有交易留痕，异常交易实时预警推送至风控经理终端。"),
                ("产品独立性、公平性管理？", "各产品独立建账、独立核算、独立托管。统一交易指令分配系统确保公平执行。禁止利益输送，禁止产品之间交叉交易，定期由外部审计机构审查。"),
                ("管理人合法合规性：证券期货市场失信记录查询？", "经查询证券期货市场失信记录查询平台，管理人无失信记录，无行政处罚，无市场禁入。"),
                ("核心人员合法合规性：证券期货市场失信记录查询？", "经查询，公司核心人员（CIO、CRO、合规总监、投资经理）均无失信记录、无行政处罚、无市场禁入。"),
                ("管理人是否存在尚未结案的私募立案调查事项？", "不存在。经查询中国证监会及各地证监局公开信息，管理人无任何尚未结案的立案调查事项。"),
                ("管理人是否存在公开的负面信息报道？", "不存在。经查询主流财经媒体和网络信息，未发现关于管理人的负面舆情报道。"),
                ("管理人法人代表及高管是否存在违规或被立案调查记录？", "不存在。经查询公开信息，公司法人代表张伟及其他高管人员无违规记录，无被立案调查记录。"),
                ("其他负面舆情信息？", "经全面排查，未发现其他负面舆情信息。公司经营正常，团队稳定，无劳动纠纷、无重大诉讼。")
            })
                db.QA.Insert(new QA { Question = q, Answer = a });
        });

        // 刷新 UI
        LoadAll();
        Vetting.Copilot.PredFiles.CreatePlaceholders(CommonFiles.Select(c => c.Name));
        LoadPredFiles();
        HandyControl.Controls.Growl.Success("模拟数据已生成");
    }

    public void SaveGlobalRecommend()
    {
        using var db = new VettingDbContext();
        var existing = db.TemplateRecommends.FindOne(r => r.FileName == "__global__");
        var ids = string.Join(",", GlobalRecommendedFunds.Select(f => f.Entity.Id));
        if (existing != null)
        {
            existing.FundIds = ids;
            db.TemplateRecommends.Update(existing);
        }
        else if (GlobalRecommendedFunds.Count > 0)
        {
            db.TemplateRecommends.Insert(new TemplateRecommend { FileName = "__global__", FundIds = ids });
        }
    }

    [RelayCommand]
    private void AddGlobalRecommend()
    {
        if (GlobalSelectedAvailable == null || GlobalRecommendedFunds.Contains(GlobalSelectedAvailable)) return;
        GlobalRecommendedFunds.Add(GlobalSelectedAvailable);
        SaveGlobalRecommend();
    }

    [RelayCommand]
    private void RemoveGlobalRecommend()
    {
        if (GlobalSelectedRecommended == null) return;
        GlobalRecommendedFunds.Remove(GlobalSelectedRecommended);
        SaveGlobalRecommend();
    }

    [RelayCommand]
    private void MoveGlobalUp()
    {
        var idx = GlobalSelectedRecommended != null ? GlobalRecommendedFunds.IndexOf(GlobalSelectedRecommended) : -1;
        if (idx <= 0) return;
        GlobalRecommendedFunds.Move(idx, idx - 1);
        SaveGlobalRecommend();
    }

    [RelayCommand]
    private void MoveGlobalDown()
    {
        var idx = GlobalSelectedRecommended != null ? GlobalRecommendedFunds.IndexOf(GlobalSelectedRecommended) : -1;
        if (idx < 0 || idx >= GlobalRecommendedFunds.Count - 1) return;
        GlobalRecommendedFunds.Move(idx, idx + 1);
        SaveGlobalRecommend();
    }

    [RelayCommand]
    private void AddItem(string? category)
    {
        using var db = new VettingDbContext();
        switch (category)
        {
            case "Staff":
                var newStaff = new Staff();
                db.Staffs.Insert(newStaff);
                var staffVm = new StaffVM(newStaff);
                staffVm.InitRoles();
                Staffs.Add(staffVm);
                SelectedStaff = staffVm;
                break;
            case "Shareholder": AddAndSave(db.Shareholders, Shareholders, e => new ShareholderVM(e), new Shareholder()); break;
            case "Department": AddAndSave(db.Departments, Departments, e => new DepartmentVM(e), new Department()); break;
            case "Strategy": AddAndSave(db.Strategies, Strategies, e => new StrategyVM(e), new Strategy()); break;
            case "FundInfo": AddAndSave(db.FundInfos, FundInfos, e => new FundInfoVM(e), new FundInfo()); break;
            case "Award": AddAndSave(db.Awards, Awards, e => new AwardVM(e), new Award()); break;
            case "AUM": AddAndSave(db.AUMs, AUMs, e => new AUMVM(e), new AUM()); break;
            case "DrawdownRecord": AddAndSave(db.DrawdownRecords, DrawdownRecords, e => new DrawdownRecordVM(e), new DrawdownRecord()); break;
            case "FinancialStatement":
                var newFs = new FinancialStatement();
                db.FinancialStatements.Insert(newFs);
                var fsVm = new FinancialStatementVM(newFs);
                FinancialStatements.Add(fsVm);
                SelectedFinancialStatement = fsVm;
                break;
            case "QA": AddAndSave(db.QA, QAs, e => new QAVM(e), new QA()); break;
        }
    }

    private static void AddAndSave<T, Tvm>(LiteDB.ILiteCollection<T> table, ObservableCollection<Tvm> col, Func<T, Tvm> wrap, T item) where T : class, new()
    {
        table.Insert(item);
        col.Add(wrap(item));
    }

    // ── 已有附件文件 ─────────────────────────────────────

    public void LoadPredFiles()
    {
        PredFileNames.Clear();
        foreach (var n in Vetting.Copilot.PredFiles.ListNames())
            PredFileNames.Add(n);
        RefreshCommonStatus();
    }

    private void RefreshCommonStatus()
    {
        var names = PredFileNames;
        foreach (var cf in CommonFiles)
        {
            cf.HasScan = names.Any(n => Path.GetFileNameWithoutExtension(n) == cf.Name);
            cf.HasStamp = names.Any(n => Path.GetFileNameWithoutExtension(n) == cf.Name + "_用印");
        }
    }

    /// <summary>复制外部文件到 pred 目录并刷新列表（覆盖同名）</summary>
    public void ImportPredFiles(string[] paths)
    {
        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            Vetting.Copilot.PredFiles.CopyIn(p);
        }
        LoadPredFiles();
    }

    /// <summary>把外部文件作为某个常用文件的扫描件/用印件导入，自动改名</summary>
    public void ImportCommonFile(CommonFileVM cf, string zone, string sourcePath)
    {
        if (!File.Exists(sourcePath)) return;
        var ext = Path.GetExtension(sourcePath);
        var name = zone == "stamp" ? $"{cf.Name}_用印{ext}" : $"{cf.Name}{ext}";
        Directory.CreateDirectory(Vetting.Copilot.PredFiles.Dir);
        File.Copy(sourcePath, Path.Combine(Vetting.Copilot.PredFiles.Dir, name), overwrite: true);
        LoadPredFiles();
    }

    [RelayCommand]
    private void DeletePredFile()
    {
        if (SelectedPredFile == null) return;
        var path = Path.Combine(Vetting.Copilot.PredFiles.Dir, SelectedPredFile);
        try { if (File.Exists(path)) File.Delete(path); } catch { }
        PredFileNames.Remove(SelectedPredFile);
        SelectedPredFile = null;
        RefreshCommonStatus();
    }

    [RelayCommand]
    private void OpenPredFile()
    {
        if (SelectedPredFile == null) return;
        var path = Path.Combine(Vetting.Copilot.PredFiles.Dir, SelectedPredFile);
        if (File.Exists(path)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private void RefreshPredFiles() => LoadPredFiles();
}

/// <summary>常用附件文件项，含扫描件/用印件两个拖拽区状态</summary>
public partial class CommonFileVM : ObservableObject
{
    public required string Name { get; init; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(ScanText))] public partial bool HasScan { get; set; }
    [ObservableProperty][NotifyPropertyChangedFor(nameof(StampText))] public partial bool HasStamp { get; set; }
    public string ScanText => HasScan ? "扫描件 ✓" : "拖入扫描件";
    public string StampText => HasStamp ? "用印件 ✓" : "拖入用印件";
}
