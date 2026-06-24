namespace FMO.AI;

/// <summary>
/// AI 解析中间层（internal），外部不感知
/// 所有复杂类型以简单字符串形式表示，由 Converter 转换为 FundFactor[]
/// </summary>
internal class AiParsedFundInfo
{
    // ===== ReadonlyFundInfo 自有字段 =====
    public string? ManagerProfile { get; set; }
    public string? AuditDate { get; set; }

    // ===== 全局属性（非份额相关）=====

    /// <summary>基金全称</summary>
    public string? FullName { get; set; }

    /// <summary>基金简称</summary>
    public string? ShortName { get; set; }

    /// <summary>证券基金类型：固定收益类/权益类/期货和衍生品类/混合类</summary>
    public string? SecurityFundType { get; set; }

    /// <summary>运作方式：开放式/封闭式/其它</summary>
    public string? FundMode { get; set; }

    /// <summary>封闭期："X个月" 或 "无" 或 其它描述</summary>
    public string? SealingRule { get; set; }

    /// <summary>风险等级：R1/R2/R3/R4/R5</summary>
    public string? RiskLevel { get; set; }

    /// <summary>是否永续产品</summary>
    public bool? DurationInfinity { get; set; }

    /// <summary>存续期月数（永续时填null）</summary>
    public int? DurationMonths { get; set; }

    /// <summary>结束日期 yyyy-MM-dd</summary>
    public string? ExpirationDate { get; set; }

    /// <summary>止损线（如 0.7）</summary>
    public decimal? StopLine { get; set; }

    /// <summary>预警线（如 0.8）</summary>
    public decimal? WarningLine { get; set; }

    /// <summary>开放日规则描述</summary>
    public string? OpenRule { get; set; }

    /// <summary>临时开放信息描述</summary>
    public string? TemporarilyOpenInfo { get; set; }

    /// <summary>巨额赎回比例描述（如 "10%"）</summary>
    public string? HugeRedemption { get; set; }

    /// <summary>主募集账户："户名：xxx\n账号：xxx\n开户行：xxx"</summary>
    public string? CollectionAccount { get; set; }

    /// <summary>主托管账户："户名：xxx\n账号：xxx\n开户行：xxx"</summary>
    public string? CustodyAccount { get; set; }

    /// <summary>托管机构名称</summary>
    public string? TrusteeName { get; set; }

    /// <summary>托管费："X%/年" 或 "固定X元/年" 或 "无"</summary>
    public string? TrusteeFee { get; set; }

    /// <summary>外包机构名称</summary>
    public string? OutsourcingName { get; set; }

    /// <summary>外包费："X%/年" 或 "固定X元/年" 或 "无"</summary>
    public string? OutsourcingFee { get; set; }

    /// <summary>管理费支付方式："按月支付"/"按季支付"/"其它描述"</summary>
    public string? ManageFeePay { get; set; }

    /// <summary>基金经理（字符串描述）</summary>
    public string? InvestmentManager { get; set; }

    /// <summary>业绩比较基准</summary>
    public string? PerformanceBenchmark { get; set; }

    /// <summary>投资目标</summary>
    public string? InvestmentObjective { get; set; }

    /// <summary>投资范围</summary>
    public string? InvestmentScope { get; set; }

    /// <summary>投资策略</summary>
    public string? InvestmentStrategy { get; set; }

    /// <summary>冷静期："24小时" 或 其它描述</summary>
    public string? CoolingPeriod { get; set; }

    /// <summary>回访："需要" 或 "不适用"</summary>
    public string? Callback { get; set; }

    // ===== 份额相关（string[]，压缩逻辑）=====

    /// <summary>份额名称列表（如 ["A类", "B类"]）</summary>
    public string[]? ShareClassNames { get; set; }

    /// <summary>锁定期规则（按份额，压缩后可能为单元素）</summary>
    public string[]? LockingRule { get; set; }

    /// <summary>管理费（按份额）："X%/年" 或 "固定X元/年" 或 "无"</summary>
    public string[]? ManageFee { get; set; }

    /// <summary>认购规则（按份额）</summary>
    public string[]? SubscriptionRule { get; set; }

    /// <summary>申购规则（按份额）</summary>
    public string[]? PurchaseRule { get; set; }

    /// <summary>赎回费（按份额）</summary>
    public string[]? RedemptionFee { get; set; }

    /// <summary>业绩报酬说明（按份额）</summary>
    public string[]? PerformanceFeeStatement { get; set; }
}
