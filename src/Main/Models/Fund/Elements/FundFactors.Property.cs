namespace FMO.Models;

public partial class FundFactors
{
    /// <summary>
    /// 份额类别
    /// </summary>
    public ShareClassFactorItem ShareClasses { get; private set; } = null!;


    // ==================== 基础信息 ====================

    /// <summary>
    /// 名称
    /// </summary>
    public SingletonFactorItem<string> FullName { get; private set; } = null!;

    /// <summary>
    /// 简称
    /// </summary>
    public SingletonFactorItem<string> ShortName { get; private set; } = null!;

    /// <summary>
    /// 证券基金类型
    /// </summary>
    public SingletonValueFactorItem<SecurityFundType> SecurityFundType { get; private set; } = null!;

    /// <summary>
    /// 运作方式
    /// </summary>
    public SingletonFactorItem<FundModeInfo> FundModeInfo { get; private set; } = null!;

    /// <summary>
    /// 封闭期
    /// </summary>
    public SingletonFactorItem<SealingRule> SealingRule { get; private set; } = null!;

    /// <summary>
    /// 风险等级
    /// </summary>
    public SingletonValueFactorItem<RiskLevel> RiskLevel { get; private set; } = null!;

    /// <summary>
    /// 存续期（月）
    /// </summary>
    public SingletonFactorItem<FundDuration> DurationInMonths { get; private set; } = null!;

    /// <summary>
    /// 结束日期
    /// </summary>
    public SingletonValueFactorItem<DateOnly> ExpirationDate { get; private set; } = null!;


    // ==================== 账户信息 ====================

    /// <summary>
    /// 主募集账户
    /// </summary>
    public SingletonFactorItem<BankAccount> CollectionAccount { get; private set; } = null!;

    /// <summary>
    /// 主托管账户
    /// </summary>
    public SingletonFactorItem<BankAccount> CustodyAccount { get; private set; } = null!;


    // ==================== 风控线 ====================

    /// <summary>
    /// 止损线
    /// </summary>
    public SingletonValueFactorItem<decimal> StopLine { get; private set; } = null!;

    /// <summary>
    /// 预警线
    /// </summary>
    public SingletonValueFactorItem<decimal> WarningLine { get; private set; } = null!;

    /// <summary>
    /// 巨额赎回比例
    /// </summary>
    public SingletonFactorItem<HugeRedemptionRule> HugeRedemption { get; private set; } = null!;


    // ==================== 开放/赎回规则 ====================

    /// <summary>
    /// 开放日规则（文本描述）
    /// </summary>
    public SingletonFactorItem<string> OpenDayInfo { get; private set; } = null!;

    /// <summary>
    /// 开放日规则（结构化）
    /// </summary>
    [FactField("OpenRule")]
    public SingletonFactorItem<OpenRule> FundOpenRule { get; private set; } = null!;

    /// <summary>
    /// 临时开放信息
    /// </summary>
    public SingletonFactorItem<TemporarilyOpenInfo> TemporarilyOpenInfo { get; private set; } = null!;

    /// <summary>
    /// 冷静期信息
    /// </summary>
    public SingletonFactorItem<CoolingPeriodInfo> CoolingPeriod { get; private set; } = null!;

    /// <summary>
    /// 回访信息
    /// </summary>
    public SingletonFactorItem<CallbackInfo> Callback { get; private set; } = null!;


    // ==================== 机构信息 ====================

    /// <summary>
    /// 托管机构信息
    /// </summary>
    public SingletonFactorItem<AgencyInfo> TrusteeInfo { get; private set; } = null!;

    /// <summary>
    /// 外包机构信息
    /// </summary>
    public SingletonFactorItem<AgencyInfo> OutsourcingInfo { get; private set; } = null!;


    // ==================== 费用信息（单份额） ====================

    /// <summary>
    /// 托管费
    /// </summary>
    public SingletonFactorItem<FundFeeInfo> TrusteeFee { get; private set; } = null!;

    /// <summary>
    /// 外包费
    /// </summary>
    public SingletonFactorItem<FundFeeInfo> OutsourcingFee { get; private set; } = null!;

    /// <summary>
    /// 管理费支付方式
    /// </summary>
    public SingletonFactorItem<FeePayInfo> ManageFeePay { get; private set; } = null!;


    // ==================== 投资经理/策略 ====================

    /// <summary>
    /// 基金经理列表
    /// </summary>
    public SingletonFactorItem<FundInvestmentManager[]> InvestmentManagers { get; private set; } = null!;

    /// <summary>
    /// 基金经理（字符串描述）
    /// </summary>
    public SingletonFactorItem<string> InvestmentManager { get; private set; } = null!;

    /// <summary>
    /// 业绩比较基准
    /// </summary>
    public SingletonFactorItem<PerformanceBenchmark> PerformanceBenchmark { get; private set; } = null!;

    /// <summary>
    /// 投资目标
    /// </summary>
    public SingletonFactorItem<string> InvestmentObjective { get; private set; } = null!;

    /// <summary>
    /// 投资范围
    /// </summary>
    public SingletonFactorItem<string> InvestmentScope { get; private set; } = null!;

    /// <summary>
    /// 投资策略
    /// </summary>
    public SingletonFactorItem<string> InvestmentStrategy { get; private set; } = null!;

    // ==================== 份额相关规则（多份额） ====================

    /// <summary>
    /// 锁定期
    /// </summary>
    public FactorItem<SealingRule> LockingRule { get; private set; } = null!;

    /// <summary>
    /// 管理费（按份额）
    /// </summary>
    public FactorItem<FundFeeInfo> ManageFee { get; private set; } = null!;

    /// <summary>
    /// 认购规则
    /// </summary>
    public FactorItem<FundPurchaseRule> SubscriptionRule { get; private set; } = null!;

    /// <summary>
    /// 申购规则
    /// </summary>
    public FactorItem<FundPurchaseRule> PurchasRule { get; private set; } = null!;

    /// <summary>
    /// 赎回费
    /// </summary>
    public FactorItem<RedemptionFeeInfo> RedemptionFee { get; private set; } = null!;

    /// <summary>
    /// 业绩报酬说明
    /// </summary>
    public FactorItem<string> PerformanceFeeStatement { get; private set; } = null!;
}