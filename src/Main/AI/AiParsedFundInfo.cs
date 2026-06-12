using FMO.Models;

namespace FMO.AI;

/// <summary>
/// 置信度包装器
/// </summary>
internal class ConfidenceWrapper<T>
{
    public T? Value { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// AI 解析中间层（internal），所有字段与 FundElements 同构 + 置信度
/// </summary>
internal class AiParsedFundInfo
{
    // ===== ReadonlyFundInfo 自有字段 =====
    public ConfidenceWrapper<string>? ManagerProfile { get; set; }
    public ConfidenceWrapper<string>? AuditDate { get; set; }

    // ===== 全局属性（非份额相关）=====

    /// <summary>基金全称</summary>
    public ConfidenceWrapper<string>? FullName { get; set; }

    /// <summary>基金简称</summary>
    public ConfidenceWrapper<string>? ShortName { get; set; }

    /// <summary>证券基金类型</summary>
    public ConfidenceWrapper<SecurityFundType>? SecurityFundType { get; set; }

    /// <summary>运作方式</summary>
    public ConfidenceWrapper<FundModeInfo>? FundModeInfo { get; set; }

    /// <summary>封闭期</summary>
    public ConfidenceWrapper<SealingRule>? SealingRule { get; set; }

    /// <summary>风险等级</summary>
    public ConfidenceWrapper<RiskLevel>? RiskLevel { get; set; }

    /// <summary>存续期</summary>
    public ConfidenceWrapper<FundDuration>? DurationInMonths { get; set; }

    /// <summary>结束日期</summary>
    public ConfidenceWrapper<string>? ExpirationDate { get; set; }

    /// <summary>止损线</summary>
    public ConfidenceWrapper<decimal>? StopLine { get; set; }

    /// <summary>预警线</summary>
    public ConfidenceWrapper<decimal>? WarningLine { get; set; }

    /// <summary>开放日规则</summary>
    public ConfidenceWrapper<OpenRule>? FundOpenRule { get; set; }

    /// <summary>临时开放</summary>
    public ConfidenceWrapper<TemporarilyOpenInfo>? TemporarilyOpenInfo { get; set; }

    /// <summary>巨额赎回比例（小数）</summary>
    public ConfidenceWrapper<decimal>? HugeRedemptionRatio { get; set; }

    /// <summary>主募集账户</summary>
    public ConfidenceWrapper<BankAccount>? CollectionAccount { get; set; }

    /// <summary>主托管账户</summary>
    public ConfidenceWrapper<BankAccount>? CustodyAccount { get; set; }

    /// <summary>托管机构（含费用信息）</summary>
    public ConfidenceWrapper<AgencyInfo>? TrusteeInfo { get; set; }

    /// <summary>外包机构（含费用信息）</summary>
    public ConfidenceWrapper<AgencyInfo>? OutsourcingInfo { get; set; }

    /// <summary>管理费支付方式</summary>
    public ConfidenceWrapper<FeePayInfo>? ManageFeePay { get; set; }

    /// <summary>基金经理（结构化数组）</summary>
    public ConfidenceWrapper<AiInvestmentManager[]>? InvestmentManagers { get; set; }

    /// <summary>基金经理（字符串描述）</summary>
    public ConfidenceWrapper<string>? InvestmentManager { get; set; }

    /// <summary>业绩比较基准</summary>
    public ConfidenceWrapper<PerformanceBenchmark>? PerformanceBenchmark { get; set; }

    /// <summary>投资目标</summary>
    public ConfidenceWrapper<string>? InvestmentObjective { get; set; }

    /// <summary>投资范围</summary>
    public ConfidenceWrapper<string>? InvestmentScope { get; set; }

    /// <summary>投资策略</summary>
    public ConfidenceWrapper<string>? InvestmentStrategy { get; set; }

    /// <summary>冷静期</summary>
    public ConfidenceWrapper<CoolingPeriodInfo>? CoolingPeriod { get; set; }

    /// <summary>回访</summary>
    public ConfidenceWrapper<CallbackInfo>? Callback { get; set; }

    // ===== 份额相关（数组，压缩逻辑）=====

    /// <summary>份额类别</summary>
    public ConfidenceWrapper<AiShareClass[]>? ShareClasses { get; set; }

    /// <summary>锁定期规则（按份额）</summary>
    public ConfidenceWrapper<SealingRule[]>? LockingRule { get; set; }

    /// <summary>管理费（按份额）</summary>
    public ConfidenceWrapper<FundFeeInfo[]>? ManageFee { get; set; }

    /// <summary>认购规则（按份额）</summary>
    public ConfidenceWrapper<FundPurchaseRule[]>? SubscriptionRule { get; set; }

    /// <summary>申购规则（按份额）</summary>
    public ConfidenceWrapper<FundPurchaseRule[]>? PurchasRule { get; set; }

    /// <summary>赎回费（按份额）</summary>
    public ConfidenceWrapper<RedemptionFeeInfo[]>? RedemptionFee { get; set; }

    /// <summary>业绩报酬说明（按份额）</summary>
    public ConfidenceWrapper<string[]>? PerformanceFeeStatement { get; set; }
}

/// <summary>
/// AI 返回的份额类别（不含 Id 等内部字段）
/// </summary>
internal class AiShareClass
{
    public string Name { get; set; } = "";
    public string? Requirement { get; set; }
}

/// <summary>
/// AI 返回的基金经理（日期为可空字符串）
/// </summary>
internal class AiInvestmentManager
{
    public int PersonId { get; set; }
    public int FundId { get; set; }
    public string Name { get; set; } = "";
    public string? Profile { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
}
