namespace FMO.Models;


/// <summary>
/// 社交账号
/// 微信 微博等
/// </summary>
public class SocialAccount
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public byte[]? QRCode { get; set; }
}

/// <summary>
/// 基金经理介绍
/// </summary>
/// <param name="Name"></param>
/// <param name="Profile"></param>
public record BrochureInvestManager(string Name, string Profile, byte[] Photo);

/// <summary>
/// 份额类型
/// </summary>
/// <param name="Name"></param>
/// <param name="Requirement"></param>
public record BrochureShareClass(string Name, string Requirement);


/// <summary>
/// 宣传用的要素
/// </summary>
public class BrochureFactor
{
    #region 管理人
    /// <summary>
    /// 管理人名称
    /// </summary>
    public required string ManagerName { get; set; }

    /// <summary>
    /// logo
    /// </summary>
    public byte[]? ManagerLogo { get; set; }


    /// <summary>
    /// 管理人名称
    /// </summary>
    public string? ManagerEnglishName { get; set; }

    /// <summary>
    /// 管理人备案号
    /// </summary>
    public required string ManagerAmacCode { get; set; }

    public string? ManagerProfile { get; set; }

    /// <summary>
    /// 公众账号
    /// </summary>
    public SocialAccount[]? ManagerSocialAccounts { get; set; }



    #endregion




    #region 基金全局属性

    public required string FundName { get; set; }

    public required string ShortName { get; set; }

    /// <summary>
    /// 份额类型
    /// </summary>
    public required BrochureShareClass[] ShareClasses { get; set; }

    /// <summary>
    /// 成立日期 yyyy-MM-dd
    /// </summary>
    public required string SetupDate { get; set; }

    /// <summary>
    /// 备案日期  yyyy-MM-dd
    /// </summary>
    public string? AuditDate { get; set; }

    /// <summary>
    /// 备案号
    /// </summary>
    public required string Code { get; set; }



    /// <summary>
    /// 公示网址
    /// </summary>
    public string? Url { get; set; }



    /// <summary>
    /// 结束日期  yyyy-MM-dd
    /// </summary>
    public required string ExpirationDate { get; set; }

    /// <summary>
    /// 存续期
    /// </summary>
    public required string Duration { get; set; }


    /// <summary>
    /// 是否结构化
    /// </summary>
    public bool IsStructured { get; set; }


    /// <summary>
    /// 基金类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 证券投资基金类型
    /// </summary>
    public string? SecurityFundType { get; set; }


    /// <summary>
    /// 运作方式（全份额共用，普通类型）
    /// 封闭、开放
    /// </summary>
    public string? FundModeInfo { get; set; }

    /// <summary>
    /// 锁定期规则
    /// </summary>
    public required string[] LockingRule { get; set; }


    /// <summary>
    /// 整体封闭期规则（基金全局规则）
    /// </summary>
    public required string SealingRule { get; set; }

    /// <summary>
    /// 风险等级
    /// </summary>
    public required string RiskLevel { get; set; }
    #endregion

    #region 账户信息（基金全局账户）
    /// <summary>
    /// 主募集账户（全份额共用）
    /// </summary>
    public required string CollectionAccount { get; set; }

    /// <summary>
    /// 主托管账户（全份额共用）
    /// </summary>
    public string? CustodyAccount { get; set; }
    #endregion

    #region 风控线（按份额区分，数组）
    /// <summary>
    /// 止损线
    /// </summary>
    public decimal? StopLine { get; set; }

    /// <summary>
    /// 预警线
    /// </summary>
    public decimal? WarningLine { get; set; }

    /// <summary>
    /// 巨额赎回规则
    /// </summary>
    public string? HugeRedemption { get; set; }
    #endregion

    #region 开放/赎回规则
    /// <summary>
    /// 开放日规则
    /// </summary>
    public required string[] OpenInfo { get; set; }

    /// <summary>
    /// 临时开放信息
    /// </summary>
    public string[]? TemporarilyOpenInfo { get; set; }

    /// <summary>
    /// 冷静期信息
    /// </summary>
    public required string CoolingPeriod { get; set; }

    /// <summary>
    /// 回访信息
    /// </summary>
    public required string Callback { get; set; }

    /// <summary>
    /// 认购规则
    /// </summary>
    public required string[] SubscriptionRule { get; set; }

    /// <summary>
    /// 申购规则
    /// </summary>
    public required string[] PurchasRule { get; set; }
    #endregion

    #region 机构信息
    /// <summary>
    /// 托管机构信息
    /// </summary>
    public required string TrusteeInfo { get; set; }


    /// <summary>
    /// 托管机构信息
    /// </summary>
    public required string TrusteeFee { get; set; }

    /// <summary>
    /// 外包机构信息
    /// </summary>
    public required string OutsourcingInfo { get; set; }


    /// <summary>
    /// 外包机构信息
    /// </summary>
    public required string OutsourcingFee { get; set; }
    #endregion

    #region 费用信息
    /// <summary>
    /// 管理费支付方式
    /// </summary>
    public string? ManageFeePay { get; set; }

    /// <summary>
    /// 管理费
    /// </summary>
    public string[]? ManageFee { get; set; }

    /// <summary>
    /// 赎回费
    /// </summary>
    public string[]? RedemptionFee { get; set; }

    /// <summary>
    /// 业绩报酬说明
    /// </summary>
    public string[]? PerformanceFeeStatement { get; set; }
    #endregion

    #region 投资经理/投资策略
    /// <summary>
    /// 基金经理列表
    /// </summary>
    public required BrochureInvestManager[] InvestmentManagers { get; set; }



    /// <summary>
    /// 业绩比较基准
    /// </summary>
    public string? PerformanceBenchmark { get; set; }

    /// <summary>
    /// 投资目标
    /// </summary>
    public string? InvestmentObjective { get; set; }

    /// <summary>
    /// 投资范围
    /// </summary>
    public string? InvestmentScope { get; set; }

    /// <summary>
    /// 投资策略
    /// </summary>
    public string? InvestmentStrategy { get; set; }
    #endregion


    public static BrochureFactor Create(Manager manager, byte[] logo, SocialAccount[] socialAccounts, BrochureInvestManager[] brochureManagers, Fund fund, FundFactors factories, int flowId)
    {
        const string unset = "未设置";

        // 1. 基础数据提取 + required 必填校验
        var shareClasses = factories.ShareClasses[flowId];
        if (shareClasses is null || shareClasses.Length is 0)
            throw new InvalidDataException("份额类型不能为空");

        // 基金基础名称
        var fundName = factories.FullName[flowId];
        var shortName = factories.ShortName[flowId];

        // 存续期（数值转字符串兜底）
        var durationVal = factories.DurationInMonths[flowId];
        string duration = durationVal is null ? unset : durationVal.ToString();

        // 单个实体 -> 字符串（统一 ToString + 空兜底）
        var fundModeInfo = factories.FundModeInfo[flowId]?.ToString() ?? unset;
        var sealingRule = factories.SealingRule[flowId]?.ToString() ?? unset;
        var riskLevel = factories.RiskLevel[flowId]?.ToString() ?? unset;
        var hugeRedemption = factories.HugeRedemption[flowId]?.ToString() ?? unset;
        var coolingPeriod = factories.CoolingPeriod[flowId]?.ToString() ?? unset;
        var callbackInfo = factories.Callback[flowId]?.ToString() ?? unset;
        var trusteeInfo = factories.TrusteeInfo[flowId]?.Name ?? unset;
        var trusteeFee = factories.TrusteeInfo[flowId]?.FeeInfo() ?? unset;
        var outsourcingInfo = factories.OutsourcingInfo[flowId]?.Name ?? unset;
        var outsourcingFee = factories.OutsourcingInfo[flowId]?.FeeInfo() ?? unset;
        var manageFeePay = factories.ManageFeePay[flowId]?.ToString() ?? unset;
        var perfBenchmark = factories.PerformanceBenchmark[flowId]?.ToString() ?? unset;
        var investObjective = factories.InvestmentObjective[flowId] ?? unset;
        var investScope = factories.InvestmentScope[flowId] ?? unset;
        var investStrategy = factories.InvestmentStrategy[flowId] ?? unset;

        // 实体数组 -> 字符串数组【C#14 集合表达式 []】
        var lockingRule = factories.LockingRule[flowId]?.Select(x => x?.ToString() ?? unset).ToArray() ?? [];
        var tempOpenInfo = factories.TemporarilyOpenInfo[flowId]?.Select(x => x?.ToString() ?? unset).ToArray() ?? [];
        var subscriptionRule = factories.SubscriptionRule[flowId]?.Select(x => x?.ToString() ?? unset).ToArray() ?? [];
        var purchasRule = factories.PurchasRule[flowId]?.Select(x => x?.ToString() ?? unset).ToArray() ?? [];
        var manageFee = factories.ManageFee[flowId]?.Select(x => x?.ToString() ?? unset).ToArray() ?? [];
        var redemptionFee = factories.RedemptionFee[flowId]?.Select(x => x?.ToString() ?? unset).ToArray() ?? [];
        string[] perfFeeStatement = factories.PerformanceFeeStatement[flowId]?.Select(x => x ?? unset)?.ToArray() ?? [unset];

        // 开放日文本规则
        var openRules = factories.FundOpenRule[flowId];
        string[] openDayInfo = openRules is null ? [unset] : [.. openRules.Select(x => string.Join(",", x?.ToString() ?? unset))];

        // 风控数值
        var stopLine = factories.StopLine[flowId];
        var warningLine = factories.WarningLine[flowId];

        // 银行账户实体
        var collectionAccount = factories.CollectionAccount[flowId]?.ToString() ?? unset;
        var custodyAccount = factories.CustodyAccount[flowId]?.ToString() ?? unset;

        // 基金经理映射 

        // 二次校验：基金经理必填
        if (brochureManagers is null || brochureManagers.Length is 0)
            throw new InvalidDataException("基金经理列表不能为空");

        // 完整赋值返回实例
        DateOnly? expire = factories.ExpirationDate[flowId];
        return new BrochureFactor
        {
            ManagerName = manager.Name ?? unset,
            ManagerLogo = logo,
            ManagerEnglishName = manager.EnglishName ?? unset,
            ManagerProfile = manager.Description ?? unset,
            ManagerAmacCode = manager.RegisterNo ?? unset,
            ManagerSocialAccounts = socialAccounts,

            FundName = fundName ?? unset,
            ShortName = shortName ?? unset,
            ShareClasses = shareClasses.Length == 1 ? [new BrochureShareClass("", "")] : [.. shareClasses.Select(x => new BrochureShareClass(x.Name, x.Requirement ?? unset))],
            SetupDate = fund.SetupDate == default ? unset : fund.SetupDate.ToString("yyyy-MM-dd"),
            AuditDate = fund.AuditDate == default ? unset : fund.AuditDate.ToString("yyyy-MM-dd"),
            Code = fund.Code ?? unset,
            Url = fund.Url ?? unset,
            ExpirationDate = expire is null || expire == default(DateOnly) ? unset : expire.Value.ToString("yyyy-MM-dd"),
            Duration = duration,
            IsStructured = factories.StructureInfo[flowId]?.IsStructured ?? false,
            Type = fund.Type == default ? unset : EnumDescriptionTypeConverter.GetEnumDescription(fund.Type),
            SecurityFundType = factories.SecurityFundType[flowId] switch { null or Models.SecurityFundType.Unk => unset, var x => EnumDescriptionTypeConverter.GetEnumDescription(x) },
            FundModeInfo = fundModeInfo,
            LockingRule = lockingRule,
            SealingRule = sealingRule,
            RiskLevel = riskLevel,

            CollectionAccount = collectionAccount,
            CustodyAccount = custodyAccount,

            StopLine = stopLine,
            WarningLine = warningLine,
            HugeRedemption = hugeRedemption,

            OpenInfo = openDayInfo,
            TemporarilyOpenInfo = tempOpenInfo,
            CoolingPeriod = coolingPeriod,
            Callback = callbackInfo,
            SubscriptionRule = subscriptionRule,
            PurchasRule = purchasRule,

            TrusteeInfo = trusteeInfo,
            TrusteeFee = trusteeFee,
            OutsourcingInfo = outsourcingInfo,
            OutsourcingFee = outsourcingFee,

            ManageFeePay = manageFeePay,
            ManageFee = manageFee,
            RedemptionFee = redemptionFee,
            PerformanceFeeStatement = perfFeeStatement,

            InvestmentManagers = brochureManagers,
            PerformanceBenchmark = perfBenchmark,
            InvestmentObjective = investObjective,
            InvestmentScope = investScope,
            InvestmentStrategy = investStrategy
        };
    }
}