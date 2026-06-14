using FMO.Models;

namespace FMO.AI;

/// <summary>
/// 将 AiParsedFundInfo（DTO）转换为 FundFactor[]
/// AI 直接返回正确类型，无需字符串解析
/// </summary>
public static class AiParsedFundInfoConverter
{
    private const int DefaultFundId = 0;
    private const int DefaultFlowId = 1;

    /// <summary>
    /// 从 DTO 提取所有 FundFactor
    /// </summary>
    public static IFundFactor[] ToFactors(AiParsedFundInfo dto)
    {
        var factors = new List<IFundFactor>();

        // ===== 份额类别（必须先添加，FactorItem 依赖它）=====
        var shareClasses = BuildShareClasses(dto.ShareClasses?.Value);
        factors.Add(MakeSingleton<ShareClass[]>(FactorFields.ShareClasses, shareClasses));

        // ===== 全局单值属性 =====
        AddIfNotNull(factors, dto.FullName?.Value, FactorFields.FullName);
        AddIfNotNull(factors, dto.ShortName?.Value, FactorFields.ShortName);

        if (dto.SecurityFundType?.Value is { } sft)
            factors.Add(MakeSingleton<SecurityFundType>(FactorFields.SecurityFundType, sft));

        if (dto.FundModeInfo?.Value is { } fmi)
            factors.Add(MakeSingleton<FundModeInfo>(FactorFields.FundModeInfo, fmi));

        if (dto.SealingRule?.Value is { } sr)
            factors.Add(MakeSingleton<SealingRule>(FactorFields.SealingRule, sr));

        if (dto.RiskLevel?.Value is { } rl)
            factors.Add(MakeSingleton<RiskLevel>(FactorFields.RiskLevel, rl));

        if (dto.DurationInMonths?.Value is { } dur)
            factors.Add(MakeSingleton<FundDuration>(FactorFields.DurationInMonths, dur));

        if (dto.ExpirationDate?.Value != null
            && DateOnly.TryParse(dto.ExpirationDate.Value, null, out var expDate))
            factors.Add(MakeSingleton<DateOnly>(FactorFields.ExpirationDate, expDate));

        if (dto.StopLine?.Value is { } sl)
            factors.Add(MakeSingleton<decimal>(FactorFields.StopLine, sl));

        if (dto.WarningLine?.Value is { } wl)
            factors.Add(MakeSingleton<decimal>(FactorFields.WarningLine, wl));

        if (dto.HugeRedemptionRatio?.Value is { } hr)
            factors.Add(MakeSingleton<HugeRedemptionRule>(FactorFields.HugeRedemption, new HugeRedemptionRule { Has = true, Ratio = hr }));

        // ===== 银行账户 =====
        if (dto.CollectionAccount?.Value is { } ca)
            factors.Add(MakeSingleton<BankAccount>(FactorFields.CollectionAccount, ca));

        if (dto.CustodyAccount?.Value is { } cu)
            factors.Add(MakeSingleton<BankAccount>(FactorFields.CustodyAccount, cu));

        // ===== 机构（AgencyInfo 已含费用信息）=====
        if (dto.TrusteeInfo?.Value is { } ti)
            factors.Add(MakeSingleton<AgencyInfo>(FactorFields.TrusteeInfo, ti));

        if (dto.OutsourcingInfo?.Value is { } oi)
            factors.Add(MakeSingleton<AgencyInfo>(FactorFields.OutsourcingInfo, oi));

        if (dto.ManageFeePay?.Value is { } mfp)
            factors.Add(MakeSingleton<FeePayInfo>(FactorFields.ManageFeePay, mfp));

        // ===== 基金经理 =====
        if (dto.InvestmentManagers?.Value is { } ims)
        {
            var managers = ims.Select(m => new FundInvestmentManager
            {
                PersonId = m.PersonId,
                FundId = m.FundId,
                Name = m.Name,
                Profile = m.Profile,
                Start = m.Start != null ? DateOnly.Parse(m.Start) : default,
                End = m.End != null ? DateOnly.Parse(m.End) : default,
            }).ToArray();
            factors.Add(MakeSingleton<FundInvestmentManager[]>(FactorFields.InvestmentManagers, managers));
        }

        if (dto.InvestmentManager?.Value is { } im)
            AddIfNotNull(factors, im, FactorFields.InvestmentManager);

        // ===== 其它全局 =====
        if (dto.PerformanceBenchmark?.Value is { } pb)
            factors.Add(MakeSingleton<PerformanceBenchmark>(FactorFields.PerformanceBenchmark, pb));

        if (dto.InvestmentObjective?.Value is { } io)
            AddIfNotNull(factors, io, FactorFields.InvestmentObjective);

        if (dto.InvestmentScope?.Value is { } isc)
            AddIfNotNull(factors, isc, FactorFields.InvestmentScope);

        if (dto.InvestmentStrategy?.Value is { } ist)
            AddIfNotNull(factors, ist, FactorFields.InvestmentStrategy);

        if (dto.TemporarilyOpenInfo?.Value is { } toi)
            AddPortionFactors(factors, toi, FactorFields.TemporarilyOpenInfo, shareClasses);

        if (dto.CoolingPeriod?.Value is { } cp)
            factors.Add(MakeSingleton<CoolingPeriodInfo>(FactorFields.CoolingPeriod, cp));

        if (dto.Callback?.Value is { } cb)
            factors.Add(MakeSingleton<CallbackInfo>(FactorFields.Callback, cb));

        if (dto.FundOpenRule?.Value is { } or2)
            AddPortionFactors(factors, or2, FactorFields.FundOpenRule, shareClasses);

        if (dto.PerformanceFeeRule?.Value is { } pfr)
            factors.Add(MakeSingleton<PerformanceFeeRule>(FactorFields.PerformanceFeeRule, pfr));

        // ===== 份额相关（数组，压缩逻辑）=====
        AddPortionFactors(factors, dto.LockingRule?.Value, FactorFields.LockingRule, shareClasses);
        AddPortionFactors(factors, dto.ManageFee?.Value, FactorFields.ManageFee, shareClasses);
        AddPortionFactors(factors, dto.SubscriptionRule?.Value, FactorFields.SubscriptionRule, shareClasses);
        AddPortionFactors(factors, dto.PurchasRule?.Value, FactorFields.PurchasRule, shareClasses);
        AddPortionFactors(factors, dto.RedemptionFee?.Value, FactorFields.RedemptionFee, shareClasses);
        AddPortionStringFactors(factors, dto.PerformanceFeeStatement?.Value, FactorFields.PerformanceFeeStatement, shareClasses);
        AddPortionFactors(factors, dto.PerformanceFeeStandard?.Value, FactorFields.PerformanceFeeStandard, shareClasses);

        return factors.ToArray();
    }

    // ===== 辅助方法 =====

    private static void AddIfNotNull<T>(List<IFundFactor> factors, T? value, string factorId) where T : class
    {
        if (value != null)
            factors.Add(MakeSingleton<T>(factorId, value));
    }

    private static FundFactor<T> MakeSingleton<T>(string factorId, T data)
        => new(factorId, DefaultFundId, DefaultFlowId, ShareClass.Singleton, data);

    private static FundFactor<T> MakeFactor<T>(string factorId, int shareId, T data)
        => new(factorId, DefaultFundId, DefaultFlowId, shareId, data);

    private static ShareClass[] BuildShareClasses(AiShareClass[]? classes)
    {
        if (classes == null || classes.Length == 0)
            return [new ShareClass { Id = ShareClass.MakeId(DefaultFlowId, 0), Name = ShareClass.SingletonName, Requirement = null, FundName = "", Code = "" }];

        // AI 返回单一份额占位时，转换为 Singleton
        if (classes.Length == 1 && (classes[0].Name == ShareClass.SingletonName || string.IsNullOrWhiteSpace(classes[0].Name)))
            return [new ShareClass { Id = ShareClass.MakeId(DefaultFlowId, 0), Name = ShareClass.SingletonName, Requirement = null, FundName = "", Code = "" }];

        return classes.Select((c, i) => new ShareClass
        {
            Id = ShareClass.MakeId(DefaultFlowId, i),
            Name = c.Name,
            Requirement = c.Requirement,
            FundName = "",
            Code = "",
            Inherit = i > 0 ? ShareClass.MakeId(DefaultFlowId, i - 1) : ShareClass.Singleton
        }).ToArray();
    }

    /// <summary>
    /// 份额相关数组要素（压缩逻辑：全相同用 Singleton，否则按份额）
    /// </summary>
    private static void AddPortionFactors<T>(
        List<IFundFactor> factors,
        T[]? values,
        string factorId,
        ShareClass[] shareClasses) where T : class
    {
        if (values == null || values.Length == 0) return;

        if (values.Length == 1)
        {
            factors.Add(MakeFactor(factorId, ShareClass.Singleton, values[0]));
            return;
        }

        // 检查所有值是否相同
        var first = values[0];
        var allSame = values.All(v => EqualityComparer<T>.Default.Equals(v, first));
        if (allSame)
        {
            factors.Add(MakeFactor(factorId, ShareClass.Singleton, first));
            return;
        }

        // 不同值，按份额添加
        for (int i = 0; i < values.Length && i < shareClasses.Length; i++)
            factors.Add(MakeFactor(factorId, shareClasses[i].Id, values[i]));
    }

    /// <summary>
    /// 份额相关字符串数组要素
    /// </summary>
    private static void AddPortionStringFactors(
        List<IFundFactor> factors,
        string[]? values,
        string factorId,
        ShareClass[] shareClasses)
    {
        if (values == null || values.Length == 0) return;

        if (values.Length == 1)
        {
            factors.Add(MakeFactor(factorId, ShareClass.Singleton, values[0]));
            return;
        }

        var allSame = values.All(v => v == values[0]);
        if (allSame)
        {
            factors.Add(MakeFactor(factorId, ShareClass.Singleton, values[0]));
            return;
        }

        for (int i = 0; i < values.Length && i < shareClasses.Length; i++)
            factors.Add(MakeFactor(factorId, shareClasses[i].Id, values[i]));
    }
}
