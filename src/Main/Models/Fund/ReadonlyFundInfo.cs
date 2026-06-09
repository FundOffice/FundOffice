namespace FMO.Models;

/// <summary>
/// 包含全部基金信息的聚合类
/// </summary>
public partial class ReadonlyFundInfo
{
    public int Id { get; init; }
     
    /// <summary>
    /// 管理人名称
    /// </summary>
    public required string ManagerName { get; init; }

   
    /// <summary>
    /// 管理人名称
    /// </summary>
    public string? ManagerEnglishName { get; init; }

    /// <summary>
    /// 管理人备案号
    /// </summary>
    public required string ManagerAmacCode { get; init; }

    /// <summary>
    /// 管理人简介
    /// </summary>
    public string? ManagerProfile { get; init; }


    /// <summary>
    /// 成立日期 yyyy-MM-dd
    /// </summary>
    public required string SetupDate { get; init; }

    /// <summary>
    /// 备案日期  yyyy-MM-dd
    /// </summary>
    public string? AuditDate { get; init; }

    /// <summary>
    /// 备案号
    /// </summary>
    public required string Code { get; init; }



    /// <summary>
    /// 公示网址
    /// </summary>
    public string? Url { get; init; }
     

    /// <summary>
    /// 最新更新日期
    /// </summary>
    public DateTime LastUpdate { get; init; }

    /// <summary>
    /// 清算日期
    /// </summary>
    public DateOnly ClearDate { get; init; }

    /// <summary>
    /// 在协会的id
    /// </summary>
    public string? AmacID { get; init; }
     

    /// <summary>
    /// 状态
    /// </summary>
    public FundStatus Status { get; init; }

    /// <summary>
    /// 是否作为投资顾问
    /// </summary>
    public bool AsAdvisor { get; init; }


    /// <summary>
    /// 公示信息同步时间
    /// </summary>
    public DateTime PublicDisclosureSynchronizeTime { get; init; }

    /// <summary>
    /// 备案系统同步时间
    /// </summary>
    public DateTime AmbersSynchronizeTime { get; init; }
 

 



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
        PerformanceBenchmark = elements.PerformanceBenchmark;// switch { null => "", var n => n.IsAdopted ? n.Value : "" };
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