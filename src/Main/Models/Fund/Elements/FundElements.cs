namespace FMO.Models;


public class FundElements
{
    public const string SingleShareKey = "单一份额";

    public int Id { get; set; }

    //public required int FundId { get; set; }


    /// <summary>
    /// 名称
    /// </summary>
    public Mutable<string> FullName { get; set; } = new Mutable<string>(nameof(FullName));

    /// <summary>
    /// 简称
    /// </summary>
    public Mutable<string> ShortName { get; set; } = new Mutable<string>(nameof(ShortName));

    public Mutable<SecurityFundType> SecurityFundType { get; set; } = new(nameof(SecurityFundType));

    /// <summary>
    /// 运作方式
    /// </summary> 
    public Mutable<DataExtra<FundMode>> FundModeInfo { get; set; } = new(nameof(ShortName));

    /// <summary>
    /// 封闭期
    /// </summary>
    public Mutable<SealingRule> SealingRule { get; set; } = new(nameof(SealingRule));

    /// <summary>
    /// 锁定期
    /// </summary>
    //public Mutable<SealingRule> LockingRule { get; set; }



    /// <summary>
    /// 风险等级
    /// </summary>
    public Mutable<RiskLevel> RiskLevel { get; set; } = new(nameof(RiskLevel));

    /// <summary>
    /// 存续期
    /// </summary>
    public Mutable<int> DurationInMonths { get; set; } = new(nameof(DurationInMonths));




    /// <summary>
    /// 结束日期
    /// </summary>
    public Mutable<DateOnly> ExpirationDate { get; set; } = new(nameof(ExpirationDate));


    /// <summary>
    /// 主募集账户
    /// </summary>
    public Mutable<BankAccount> CollectionAccount { get; set; } = new(nameof(CollectionAccount));


    /// <summary>
    /// 主托管账户
    /// </summary>
    public Mutable<BankAccount> CustodyAccount { get; set; } = new(nameof(CustodyAccount));


    /// <summary>
    /// 份额类别
    /// </summary>
    public Mutable<ShareClass[]> ShareClasses { get; set; } = new(nameof(ShareClasses));

    /// <summary>
    /// 止损线
    /// </summary>
    public Mutable<decimal> StopLine { get; set; } = new(nameof(StopLine));

    /// <summary>
    /// 预警线
    /// </summary>
    public Mutable<decimal> WarningLine { get; set; } = new(nameof(WarningLine));

    /// <summary>
    /// 开放日规则
    /// </summary>
    public Mutable<string> OpenDayInfo { get; set; } = new(nameof(OpenDayInfo));


    public Mutable<OpenRule> FundOpenRule { get; set; } = new(nameof(FundOpenRule));


    public Mutable<AgencyInfo> TrusteeInfo { get; set; } = new(nameof(TrusteeInfo));

    /// <summary>
    /// 托管费
    /// </summary> 
    public Mutable<FundFeeInfo> TrusteeFee { get; set; } = new(nameof(TrusteeFee));

    public Mutable<AgencyInfo> OutsourcingInfo { get; set; } = new(nameof(OutsourcingInfo));

    /// <summary>
    /// 外包费
    /// </summary>
    public Mutable<FundFeeInfo> OutsourcingFee { get; set; } = new(nameof(OutsourcingFee));


    public Mutable<FundInvestmentManager[]> InvestmentManagers { get; set; } = new(nameof(InvestmentManagers));


    public Mutable<string> InvestmentManager { get; set; } = new(nameof(InvestmentManager));

    /// <summary>
    /// 业绩比较基准
    /// </summary>
    public Mutable<ValueWithBoolean<string>> PerformanceBenchmarks { get; set; } = new(nameof(PerformanceBenchmarks));

    /// <summary>
    /// 投资目标
    /// </summary>
    public Mutable<string> InvestmentObjective { get; set; } = new(nameof(InvestmentObjective));

    /// <summary>
    /// 投资范围
    /// </summary>
    public Mutable<string> InvestmentScope { get; set; } = new(nameof(InvestmentScope));

    /// <summary>
    /// 投资策略
    /// </summary>
    public Mutable<string> InvestmentStrategy { get; set; } = new(nameof(InvestmentStrategy));


    public Mutable<TemporarilyOpenInfo> TemporarilyOpenInfo { get; set; } = new(nameof(TemporarilyOpenInfo));


    public Mutable<decimal> HugeRedemptionRatio { get; set; } = new(nameof(HugeRedemptionRatio));


    public Mutable<CoolingPeriodInfo> CoolingPeriod { get; set; } = new(nameof(CoolingPeriod));


    public Mutable<CallbackInfo> Callback { get; set; } = new(nameof(Callback));


    public PortionMutable<ValueWithEnum<SealingType, int>> LockingRule { get; set; } = new(nameof(LockingRule));



    /// <summary>
    /// 管理费
    /// </summary>
    public PortionMutable<FundFeeInfo> ManageFee { get; set; } = new(nameof(ManageFee));

    public Mutable<FeePayInfo> ManageFeePay { get; set; } = new(nameof(ManageFeePay));

    /// <summary>
    /// 认购费
    /// </summary>
    //public PortionMutable<FundFeeInfo> SubscriptionFee { get; set; } = new(nameof(SubscriptionFee));


    public PortionMutable<FundPurchaseRule> SubscriptionRule { get; set; } = new(nameof(SubscriptionRule));



    /// <summary>
    /// 申购费
    /// </summary>
    //public PortionMutable<FundFeeInfo> PurchaseFee { get; set; } = new(nameof(PurchaseFee));

    public PortionMutable<FundPurchaseRule> PurchasRule { get; set; } = new(nameof(PurchasRule));

    /// <summary>
    /// 赎回费
    /// </summary>
    public PortionMutable<RedemptionFeeInfo> RedemptionFee { get; set; } = new(nameof(RedemptionFee));

    /// <summary>
    /// 业绩报酬
    /// </summary>
    public PortionMutable<string> PerformanceFeeStatement { get; set; } = new(nameof(PerformanceFeeStatement));




    public static FundElements Create(int fundid, int firstFlow)
    {
        var e = new FundElements { Id = fundid, };
        e.ShareClasses.SetValue([new ShareClass { Id = -1, Name = FundElements.SingleShareKey }], 0);
        e.Callback.SetValue(new CallbackInfo(), firstFlow);
        return e;
    }


    public bool Init()
    {
        bool changed = false;

        if (RiskLevel is null)
        { changed = true; RiskLevel = new Mutable<RiskLevel>(nameof(RiskLevel)); }

        if (DurationInMonths is null)
        { changed = true; DurationInMonths = new Mutable<int>(nameof(DurationInMonths)); }

        if (ExpirationDate is null)
        { changed = true; ExpirationDate = new Mutable<DateOnly>(nameof(ExpirationDate)); }

        if (CollectionAccount is null)
        { changed = true; CollectionAccount = new Mutable<BankAccount>(nameof(CollectionAccount)); }

        if (CustodyAccount is null)
        { changed = true; CustodyAccount = new Mutable<BankAccount>(nameof(CustodyAccount)); }


        if (ShareClasses is null)
        { changed = true; ShareClasses = new Mutable<ShareClass[]>(nameof(ShareClasses)); }


        if (StopLine is null)
        { changed = true; StopLine = new Mutable<decimal>(nameof(StopLine)); }


        if (WarningLine is null)
        { changed = true; WarningLine = new Mutable<decimal>(nameof(WarningLine)); }

        if (FundModeInfo is null)
        { changed = true; FundModeInfo = new Mutable<DataExtra<FundMode>>(nameof(FundModeInfo)); }

        if (SealingRule is null)
        { changed = true; SealingRule = new Mutable<SealingRule>(nameof(SealingRule)); }


        if (LockingRule is null)
        { changed = true; LockingRule = new(nameof(LockingRule)); }

        if (OpenDayInfo is null)
        { changed = true; OpenDayInfo = new Mutable<string>(nameof(OpenDayInfo)); }




        if (TrusteeFee is null)
        { changed = true; TrusteeFee = new(nameof(TrusteeFee)); }


        if (OutsourcingFee is null)
        { changed = true; OutsourcingFee = new(nameof(OutsourcingFee)); }

        if (ManageFee is null)
        { changed = true; ManageFee = new(nameof(ManageFee)); }


        //if (SubscriptionFee is null)
        //{ changed = true; SubscriptionFee = new(nameof(SubscriptionFee)); }

        //if (PurchaseFee is null)
        //{ changed = true; PurchaseFee = new(nameof(PurchaseFee)); }

        if (RedemptionFee is null)
        { changed = true; RedemptionFee = new(nameof(RedemptionFee)); }

        if (InvestmentManagers is null)
        { changed = true; InvestmentManagers = new(nameof(InvestmentManagers)); }


        if (PerformanceBenchmarks is null)
        { changed = true; PerformanceBenchmarks = new(nameof(PerformanceBenchmarks)); }

        if (InvestmentObjective is null)
        { changed = true; InvestmentObjective = new(nameof(InvestmentObjective)); }


        if (InvestmentScope is null)
        { changed = true; InvestmentScope = new(nameof(InvestmentScope)); }

        if (InvestmentStrategy is null)
        { changed = true; InvestmentStrategy = new(nameof(InvestmentStrategy)); }

        return changed;
    }

    /// <summary>
    /// 获取所有使用过的基金名
    /// </summary>
    /// <returns></returns>
    public string[] GetAllNames()
    {
        return FullName.Changes.Values.ToArray();
    }


    /// <summary>
    /// 删除份额相关的要素 
    /// </summary>
    /// <param name="flowid"></param>
    /// <param name="share"></param>
    public void RemoveShareRelated(int flowid, int share)
    {
        if (share == -1) return;
        foreach (var p in GetType().GetProperties())
        {
            if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(PortionMutable<>))
            {
                var genericArg = p.PropertyType.GetGenericArguments()[0];
                var method = p.PropertyType.GetMethod(nameof(PortionMutable<object>.RemoveValue), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, new[] { typeof(int), typeof(int) });
                var obj = p.GetValue(this);
                method?.Invoke(obj, new object[] { share, flowid });
            }
        }
    }

    public void SwitchShareAsUnion(int flowid, int share)
    {
        if (share == -1) return;
        foreach (var p in GetType().GetProperties())
        {
            if (p.PropertyType.IsAssignableTo(typeof(IPortionMutable)) && p.GetValue(this) is IPortionMutable m)
                m.SwitchToSingle(share, flowid);
        }
    }


    //private void AddShareRelated(int flowId, string[] add)
    //{
    //    CopyFromDefault(ManageFee!, flowId, add);
    //    CopyFromDefault(LockingRule!, flowId, add);
    //    CopyFromDefault(SubscriptionFee!, flowId, add);
    //    CopyFromDefault(PurchaseFee!, flowId, add);
    //    CopyFromDefault(RedemptionFee!, flowId, add);
    //}



    //private void CopyFromDefault<T1, T2>(PortionMutable<ValueWithEnum<T1, T2>> mutable, int flowId, string[] add) where T1 : struct, Enum
    //{
    //    if (mutable!.GetValue(flowId) is var d && d.FlowId == flowId && d.Value?.FirstOrDefault().Value is ValueWithEnum<T1, T2> r)
    //        foreach (var item in add)
    //            d.Value[item] = r;
    //}

    //private void SetElementAsDefault<T>(PortionMutable<T> portion, int flowid) where T : notnull
    //{
    //    if (portion is null) return;

    //    (var id, var v) = portion!.GetValue(flowid);
    //    if (id != flowid) return;
    //    if (v is null || v.Count != 1) return;

    //    var sin = v.First();
    //    v[SingleShareKey] = sin.Value;
    //    v.Remove(sin.Key);
    //}


    //private void SetAsDefault(int flowid)
    //{
    //    foreach (var p in GetType().GetProperties())
    //    {
    //        if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(PortionMutable<>))
    //        {
    //            var genericArg = p.PropertyType.GetGenericArguments()[0];
    //            var method = typeof(FundElements).GetMethod(nameof(FundElements.SetElementAsDefault), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);//, new[] { typeof(PortionMutable<>), typeof(int) });
    //            var genericMethod = method!.MakeGenericMethod(genericArg);
    //            genericMethod.Invoke(this, new object[] { p.GetValue(this)!, flowid });
    //        }
    //    }
    //}



    //public void ShareClassChange(int flowId, string[] newshares, string[] add, string[] remove)
    //{
    //    // 从单一份额变成多份额，复制值
    //    if (ShareClasses is not null && ShareClasses.GetValue(flowId) is var iv && iv.FlowId == flowId && iv.Value?.Length == 1)
    //        AddShareRelated(flowId, add);

    //    foreach (var item in remove)
    //        RemoveShareRelated(flowId, item);

    //    if (ShareClasses is null)
    //        ShareClasses = new(nameof(FundElements.ShareClasses), newshares.Select(x => new ShareClass(x)).ToArray());
    //    else
    //        ShareClasses.SetValue(newshares.Select(x => new ShareClass(x)).ToArray(), flowId);


    //    if (newshares.Length == 1)
    //        SetAsDefault(flowId);
    //}

    public void ShareClassChange(int flowId, (int Id, string Name)[] add, (int Id, string Name)[] remove, (int Id, string Name)[] change)
    {
        var old = ShareClasses!.GetValue(flowId).Value?.ToList() ?? new();
        old.AddRange(add.Select(x => new ShareClass { Id = x.Id, Name = x.Name }));

        //删除份额类型
        foreach (var item in remove)
            RemoveShareRelated(flowId, item.Id);
        old.RemoveAll(x => remove.Any(y => x.Id == y.Id));

        //更名
        foreach (var item in change)
        {
            var v = old.FirstOrDefault(x => x.Id == item.Id);
            if (v is not null) v.Name = item.Name;
        }

        //如果只有一个，强制更名
        if (old.Count == 1) old[0].Name = SingleShareKey;

        ShareClasses!.SetValue(old.ToArray(), flowId);
    }

    public void ShareClassChange(int flowId, (int Id, string Name, string? Requirement)[] add, ShareClass[] remove, (int Id, string Name, string? Requirement)[] change)
    {
        var old = ShareClasses!.GetValue(flowId).Value?.ToList() ?? new();
        old.AddRange(add.Select(x => new ShareClass { Id = x.Id, Name = x.Name, Requirement = x.Requirement }));

        //删除份额类型
        foreach (var item in remove)
            RemoveShareRelated(flowId, item.Id);
        old.RemoveAll(x => remove.Any(y => x.Id == y.Id));

        //更名
        foreach (var item in change)
        {
            var v = old.FirstOrDefault(x => x.Id == item.Id);
            if (v is not null)
            {
                v.Name = item.Name;
                v.Requirement = item.Requirement;
            }
        }

        //如果只有一个，强制更名
        if (old.Count == 1)
        {
            old[0].Name = SingleShareKey;
            SwitchShareAsUnion(flowId, old[0].Id);
            old[0].Id = -1;
        }
        else if (old.Count > 1 && old.FirstOrDefault(x => x.Id == -1) is ShareClass sc)
            old.Remove(sc);

        ShareClasses!.SetValue(old.DistinctBy(x => x.Id).ToArray(), flowId);
    }
}
