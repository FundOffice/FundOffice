namespace FMO.Models;

/// <summary>
/// 包含全部基金信息的聚合类
/// </summary>
public class ReadonlyFundInfo
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    /// <summary>
    /// 发起日期
    /// </summary>
    public DateOnly InitiateDate { get; set; }

    /// <summary>
    /// 成立日期
    /// </summary>
    public DateOnly SetupDate { get; set; }

    /// <summary>
    /// 备案日期
    /// </summary>
    public DateOnly AuditDate { get; set; }

    /// <summary>
    /// 备案号
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// 最新更新日期
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// 清算日期
    /// </summary>
    public DateOnly ClearDate { get; set; }

    /// <summary>
    /// 在协会的id
    /// </summary>
    public string? AmacID { get; set; }


    /// <summary>
    /// 公示网址
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public FundStatus Status { get; set; }

    /// <summary>
    /// 是否作为投资顾问
    /// </summary>
    public bool AsAdvisor { get; set; }


    /// <summary>
    /// 公示信息同步时间
    /// </summary>
    public DateTime PublicDisclosureSynchronizeTime { get; set; }

    /// <summary>
    /// 备案系统同步时间
    /// </summary>
    public DateTime AmbersSynchronizeTime { get; set; }

    /// <summary>
    /// 结束日期
    /// </summary>
    public DateOnly? ExpirationDate { get; set; }

    /// <summary>
    /// 存续期
    /// </summary>
    public string? SurvivalPeriod { get; set; }


    /// <summary>
    /// 是否结构化
    /// </summary>
    public bool IsStructured { get; set; }




    /// <summary>
    /// 基金类型
    /// </summary>
    public FundType Type { get; set; }

    /// <summary>
    /// 管理类型
    /// </summary>
    public ManageType ManageType { get; set; }

    // 新增属性（来自 FundElements）
    public SecurityFundType SecurityFundType { get; set; }

    public DataExtra<FundMode>? FundModeInfo { get; set; }
    public SealingRule? SealingRule { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public int? DurationInMonths { get; set; }
    public BankAccount? CollectionAccount { get; set; }
    public BankAccount? CustodyAccount { get; set; }
    public ShareClass? ShareClass { get; set; }
    public decimal? StopLine { get; set; }
    public decimal? WarningLine { get; set; }
    public string? OpenDayInfo { get; set; }
    public OpenRule? FundOpenRule { get; set; }
    public AgencyInfo? TrusteeInfo { get; set; }
    public FundFeeInfo? TrusteeFee { get; set; }
    public AgencyInfo? OutsourcingInfo { get; set; }
    public FundFeeInfo? OutsourcingFee { get; set; }
    public FundInvestmentManager[]? InvestmentManagers { get; set; }


    public string? InvestmentManager { get; set; }
    public string? PerformanceBenchmarks { get; set; }
    public string? InvestmentObjective { get; set; }
    public string? InvestmentScope { get; set; }
    public string? InvestmentStrategy { get; set; }
    public TemporarilyOpenInfo? TemporarilyOpenInfo { get; set; }
    public decimal? HugeRedemptionRatio { get; set; }
    public CoolingPeriodInfo? CoolingPeriod { get; set; }
    public CallbackInfo? Callback { get; set; }
    public ValueWithEnum<SealingType, int>? LockingRule { get; set; }
    public FundFeeInfo? ManageFee { get; set; }
    public FeePayInfo? ManageFeePay { get; set; }
    public FundPurchaseRule? SubscriptionRule { get; set; }
    public FundPurchaseRule? PurchasRule { get; set; }
    public RedemptionFeeInfo? RedemptionFee { get; set; }
    public string? PerformanceFeeStatement { get; set; }



    public static ReadonlyFundInfo[] Load(Fund fund, FundElements elements)
    {
        if (elements is null)
        {
            var n = new ReadonlyFundInfo();
            n.FillFrom(fund);
            return [n];
        }

        // 判断是否分级
        var sc = elements.ShareClasses.Value;

        ReadonlyFundInfo[] r = new ReadonlyFundInfo[sc.Length];

        for (int i = 0; i < sc.Length; i++)
        {
            r[i] = new();
            r[i].FillFrom(fund);
            r[i].FillFrom(elements, sc[i]);
        }


        return r;
    }

    private void FillFrom(FundElements elements, ShareClass shareClass)
    {
        if (shareClass.Name == "单一份额") shareClass.Name = "";

        var v = elements.FullName.Value;
        if (!string.IsNullOrWhiteSpace(v)) Name = v;
        v = elements.ShortName.Value;
        if (!string.IsNullOrWhiteSpace(v)) ShortName = v;

        SecurityFundType = elements.SecurityFundType.Value;
        FundModeInfo = elements.FundModeInfo?.Value;
        SealingRule = elements.SealingRule?.Value;
        RiskLevel = elements.RiskLevel?.Value;
        DurationInMonths = elements.DurationInMonths?.Value;
        ExpirationDate = elements.ExpirationDate?.Value;
        CollectionAccount = elements.CollectionAccount?.Value;
        CustodyAccount = elements.CustodyAccount?.Value;
        ShareClass = shareClass;
        StopLine = elements.StopLine?.Value;
        WarningLine = elements.WarningLine?.Value;
        OpenDayInfo = elements.OpenDayInfo?.Value;
        FundOpenRule = elements.FundOpenRule?.Value;
        TrusteeInfo = elements.TrusteeInfo?.Value;
        TrusteeFee = elements.TrusteeFee?.Value;
        OutsourcingInfo = elements.OutsourcingInfo?.Value;
        OutsourcingFee = elements.OutsourcingFee?.Value;
        InvestmentManagers = elements.InvestmentManagers?.Value?.ToArray();
        InvestmentManager = elements.InvestmentManager?.Value;
        PerformanceBenchmarks = elements.PerformanceBenchmarks?.Value switch { null => "", var n => n.IsAdopted ? n.Value : "" };
        InvestmentObjective = elements.InvestmentObjective?.Value;
        InvestmentScope = elements.InvestmentScope?.Value;
        InvestmentStrategy = elements.InvestmentStrategy?.Value;
        TemporarilyOpenInfo = elements.TemporarilyOpenInfo?.Value;
        HugeRedemptionRatio = elements.HugeRedemptionRatio?.Value;
        CoolingPeriod = elements.CoolingPeriod?.Value;
        Callback = elements.Callback?.Value ?? new CallbackInfo();

        // 映射 PortionMutable<T> 属性（取默认值）
        LockingRule = elements.LockingRule?.GetValue(shareClass.Id, int.MaxValue).Value;
        ManageFee = elements.ManageFee?.GetValue(shareClass.Id, int.MaxValue).Value;
        ManageFeePay = elements.ManageFeePay?.Value; // 注意这是Mutable
        SubscriptionRule = elements.SubscriptionRule?.GetValue(shareClass.Id, int.MaxValue).Value;
        PurchasRule = elements.PurchasRule?.GetValue(shareClass.Id, int.MaxValue).Value;
        RedemptionFee = elements.RedemptionFee?.GetValue(shareClass.Id, int.MaxValue).Value;
        PerformanceFeeStatement = elements.PerformanceFeeStatement?.GetValue(shareClass.Id, int.MaxValue).Value;


    }

    // 将 Fund 对象的属性填充到 ReadonlyFundInfo 中
    private void FillFrom(Fund fund)
    {
        Id = fund.Id;
        Name = fund.Name;
        ShortName = fund.ShortName;
        InitiateDate = fund.InitiateDate;
        SetupDate = fund.SetupDate;
        AuditDate = fund.AuditDate;
        Code = fund.Code;
        LastUpdate = fund.LastUpdate;
        ClearDate = fund.ClearDate;
        AmacID = fund.AmacID;
        Url = fund.Url;
        Status = fund.Status;
        AsAdvisor = fund.AsAdvisor;
        PublicDisclosureSynchronizeTime = fund.PublicDisclosureSynchronizeTime;
        AmbersSynchronizeTime = fund.AmbersSynchronizeTime;
        Type = fund.Type;
        ManageType = fund.ManageType;
    }
}