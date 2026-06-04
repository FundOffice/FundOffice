namespace FMO.Models;




public partial class FundElements
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

    /// <summary>
    /// 开放日
    /// </summary>
    [FactorField("OpenRule")] public Mutable<OpenRule> FundOpenRule { get; set; } = new(nameof(FundOpenRule));

    /// <summary>
    /// 托管机构
    /// </summary>
    public Mutable<AgencyInfo> TrusteeInfo { get; set; } = new(nameof(TrusteeInfo));

    /// <summary>
    /// 托管费
    /// </summary> 
    public Mutable<FundFeeInfo> TrusteeFee { get; set; } = new(nameof(TrusteeFee));

    /// <summary>
    /// 外包机构
    /// </summary>
    public Mutable<AgencyInfo> OutsourcingInfo { get; set; } = new(nameof(OutsourcingInfo));

    /// <summary>
    /// 外包费
    /// </summary>
    public Mutable<FundFeeInfo> OutsourcingFee { get; set; } = new(nameof(OutsourcingFee));

    /// <summary>
    /// 基金经理
    /// </summary>
    public Mutable<FundInvestmentManager[]> InvestmentManagers { get; set; } = new(nameof(InvestmentManagers));


    public Mutable<string> InvestmentManager { get; set; } = new(nameof(InvestmentManager));

    /// <summary>
    /// 业绩比较基准
    /// </summary>
    public Mutable<PerformanceBenchmark> PerformanceBenchmark { get; set; } = new(nameof(PerformanceBenchmark));

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

    /// <summary>
    /// 临时开放
    /// </summary>
    public Mutable<TemporarilyOpenInfo> TemporarilyOpenInfo { get; set; } = new(nameof(TemporarilyOpenInfo));

    /// <summary>
    /// 巨额赎回
    /// </summary>
    public Mutable<decimal> HugeRedemptionRatio { get; set; } = new(nameof(HugeRedemptionRatio));

    /// <summary>
    /// 冷静期
    /// </summary>
    public Mutable<CoolingPeriodInfo> CoolingPeriod { get; set; } = new(nameof(CoolingPeriod));

    /// <summary>
    /// 回访
    /// </summary>
    public Mutable<CallbackInfo> Callback { get; set; } = new(nameof(Callback));

    /// <summary>
    /// 锁定期
    /// </summary>
    public PortionMutable<SealingRule> LockingRule { get; set; } = new(nameof(LockingRule));



    /// <summary>
    /// 管理费
    /// </summary>
    public PortionMutable<FundFeeInfo> ManageFee { get; set; } = new(nameof(ManageFee));

    public Mutable<FeePayInfo> ManageFeePay { get; set; } = new(nameof(ManageFeePay));

    /// <summary>
    /// 认购规则
    /// </summary> 
    public PortionMutable<FundPurchaseRule> SubscriptionRule { get; set; } = new(nameof(SubscriptionRule));



    /// <summary>
    /// 申购规则
    /// </summary> 
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


        if (PerformanceBenchmark is null)
        { changed = true; PerformanceBenchmark = new(nameof(PerformanceBenchmark)); }

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


public partial class FundElements
{


    public IFundFactor[] ToFactors()
    {


        var Factors = new List<IFundFactor>();
        var fundId = this.Id;

        if (this.FullName is Mutable<string> m_FullName && m_FullName.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_FullName.Changes)
            {
                Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.FullName, Data = value });
            }
        }
        if (this.ShortName is Mutable<string> m_ShortName && m_ShortName.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_ShortName.Changes)
            {
                Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.ShortName, Data = value });
            }
        }
        if (this.SecurityFundType is Mutable<global::FMO.Models.SecurityFundType> m_SecurityFundType && m_SecurityFundType.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_SecurityFundType.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.SecurityFundType> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.SecurityFundType, Data = value });
            }
        }
        if (this.FundModeInfo is Mutable<global::FMO.Models.DataExtra<global::FMO.Models.FundMode>> m_FundModeInfo && m_FundModeInfo.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_FundModeInfo.Changes)
            {
                Factors.Add(new FundFactor<FundModeInfo> { FundId = Id, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.FundModeInfo, Data = new FundModeInfo { Mode = value.Data ?? default, Other = value.Extra } });
            }
        }
        if (this.SealingRule is Mutable<global::FMO.Models.SealingRule> m_SealingRule && m_SealingRule.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_SealingRule.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.SealingRule> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.SealingRule, Data = value });
            }
        }
        if (this.RiskLevel is Mutable<global::FMO.Models.RiskLevel> m_RiskLevel && m_RiskLevel.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_RiskLevel.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.RiskLevel> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.RiskLevel, Data = value });
            }
        }
        if (this.DurationInMonths is Mutable<int> m_DurationInMonths && m_DurationInMonths.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_DurationInMonths.Changes)
            {
                Factors.Add(new FundFactor<int> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.DurationInMonths, Data = value });
            }
        }
        if (this.ExpirationDate is Mutable<global::System.DateOnly> m_ExpirationDate && m_ExpirationDate.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_ExpirationDate.Changes)
            {
                Factors.Add(new FundFactor<global::System.DateOnly> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.ExpirationDate, Data = value });
            }
        }
        if (this.CollectionAccount is Mutable<global::FMO.Models.BankAccount> m_CollectionAccount && m_CollectionAccount.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_CollectionAccount.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.BankAccount> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.CollectionAccount, Data = value });
            }
        }
        if (this.CustodyAccount is Mutable<global::FMO.Models.BankAccount> m_CustodyAccount && m_CustodyAccount.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_CustodyAccount.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.BankAccount> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.CustodyAccount, Data = value });
            }
        }
        if (this.ShareClasses is Mutable<global::FMO.Models.ShareClass[]> m_ShareClasses && m_ShareClasses.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_ShareClasses.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.ShareClass[]> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.ShareClasses, Data = value });
            }
        }
        if (this.StopLine is Mutable<decimal> m_StopLine && m_StopLine.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_StopLine.Changes)
            {
                Factors.Add(new FundFactor<decimal> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.StopLine, Data = value });
            }
        }
        if (this.WarningLine is Mutable<decimal> m_WarningLine && m_WarningLine.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_WarningLine.Changes)
            {
                Factors.Add(new FundFactor<decimal> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.WarningLine, Data = value });
            }
        }
        if (this.OpenDayInfo is Mutable<string> m_OpenDayInfo && m_OpenDayInfo.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_OpenDayInfo.Changes)
            {
                Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.OpenDayInfo, Data = value });
            }
        }
        if (this.FundOpenRule is Mutable<global::FMO.Models.OpenRule> m_FundOpenRule && m_FundOpenRule.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_FundOpenRule.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.OpenRule> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.FundOpenRule, Data = value });
            }
        }
        if (this.TrusteeInfo is Mutable<global::FMO.Models.AgencyInfo> m_TrusteeInfo && m_TrusteeInfo.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_TrusteeInfo.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.AgencyInfo> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.TrusteeInfo, Data = value });
            }
        }
        void MergeFundFeeToAgencyInfo(AgencyInfo agencyInfo, FundFeeInfo feeInfo)
        {
            if (agencyInfo == null || feeInfo == null) return;

            agencyInfo.FeeType = feeInfo.Type;
            agencyInfo.HasFee = feeInfo.HasFee;
            agencyInfo.Fee = feeInfo.Fee;
            agencyInfo.HasGuaranteedFee = feeInfo.HasGuaranteedFee;
            agencyInfo.GuaranteedFee = feeInfo.GuaranteedFee;
            agencyInfo.Other = feeInfo.Other;
        }


        if (this.TrusteeFee is Mutable<global::FMO.Models.FundFeeInfo> m_TrusteeFee && m_TrusteeFee.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_TrusteeFee.Changes)
            {
                // 🔑 核心：查找 同FlowId、同托管信息 的已有因子
                var oldFactor = Factors.FirstOrDefault(x =>
                    x.FundId == Id &&
                    x.FlowId == flowId &&
                    x.ShareId == ShareClass.Singleton &&
                    x.FactorId == FactorFields.TrusteeInfo) as FundFactor<AgencyInfo>;

                if (oldFactor != null)
                {
                    // ✅ 找到：直接将 Fee 合并到已有的 AgencyInfo 中（不新增）
                    MergeFundFeeToAgencyInfo(oldFactor.Data, value);
                }
                else
                {
                    // ❌ 没找到：新建 AgencyInfo，合并 Fee 后添加
                    var mergedAgency = new AgencyInfo();
                    MergeFundFeeToAgencyInfo(mergedAgency, value);

                    Factors.Add(new FundFactor<AgencyInfo>
                    {
                        FundId = Id,
                        FlowId = flowId,
                        ShareId = ShareClass.Singleton,
                        FactorId = FactorFields.TrusteeInfo,
                        Data = mergedAgency
                    });
                }
            }
        }

       

        if (this.OutsourcingInfo is Mutable<global::FMO.Models.AgencyInfo> m_OutsourcingInfo && m_OutsourcingInfo.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_OutsourcingInfo.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.AgencyInfo> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.OutsourcingInfo, Data = value });
            }
        }
        if (this.OutsourcingFee is Mutable<global::FMO.Models.FundFeeInfo> m_OutsourcingFee && m_OutsourcingFee.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_OutsourcingFee.Changes)
            {
                // 🔑 核心：查找 同FlowId、同托管信息 的已有因子
                var oldFactor = Factors.FirstOrDefault(x =>
                    x.FundId == Id &&
                    x.FlowId == flowId &&
                    x.ShareId == ShareClass.Singleton &&
                    x.FactorId == FactorFields.OutsourcingInfo) as FundFactor<AgencyInfo>;

                if (oldFactor != null)
                {
                    // ✅ 找到：直接将 Fee 合并到已有的 AgencyInfo 中（不新增）
                    MergeFundFeeToAgencyInfo(oldFactor.Data, value);
                }
                else
                {
                    // ❌ 没找到：新建 AgencyInfo，合并 Fee 后添加
                    var mergedAgency = new AgencyInfo();
                    MergeFundFeeToAgencyInfo(mergedAgency, value);

                    Factors.Add(new FundFactor<AgencyInfo>
                    {
                        FundId = Id,
                        FlowId = flowId,
                        ShareId = ShareClass.Singleton,
                        FactorId = FactorFields.OutsourcingInfo,
                        Data = mergedAgency
                    });
                }
            }
        }
        if (this.InvestmentManagers is Mutable<global::FMO.Models.FundInvestmentManager[]> m_InvestmentManagers && m_InvestmentManagers.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_InvestmentManagers.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.FundInvestmentManager[]> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.InvestmentManagers, Data = value });
            }
        }
        if (this.InvestmentManager is Mutable<string> m_InvestmentManager && m_InvestmentManager.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_InvestmentManager.Changes)
            {
                Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.InvestmentManager, Data = value });
            }
        }
        if (this.PerformanceBenchmark is Mutable<global::FMO.Models.PerformanceBenchmark> m_PerformanceBenchmark && m_PerformanceBenchmark.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_PerformanceBenchmark.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.PerformanceBenchmark> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.PerformanceBenchmark, Data = value });
            }
        }
        if (this.InvestmentObjective is Mutable<string> m_InvestmentObjective && m_InvestmentObjective.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_InvestmentObjective.Changes)
            {
                Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.InvestmentObjective, Data = value });
            }
        }
        if (this.InvestmentScope is Mutable<string> m_InvestmentScope && m_InvestmentScope.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_InvestmentScope.Changes)
            {
                Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.InvestmentScope, Data = value });
            }
        }
        if (this.InvestmentStrategy is Mutable<string> m_InvestmentStrategy && m_InvestmentStrategy.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_InvestmentStrategy.Changes)
            {
                Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.InvestmentStrategy, Data = value });
            }
        }
        if (this.TemporarilyOpenInfo is Mutable<global::FMO.Models.TemporarilyOpenInfo> m_TemporarilyOpenInfo && m_TemporarilyOpenInfo.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_TemporarilyOpenInfo.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.TemporarilyOpenInfo> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.TemporarilyOpenInfo, Data = value });
            }
        }
        if (this.HugeRedemptionRatio is Mutable<decimal> m_HugeRedemptionRatio && m_HugeRedemptionRatio.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_HugeRedemptionRatio.Changes)
            {
                Factors.Add(new FundFactor<HugeRedemptionRule>
                {
                    FundId = Id,
                    FlowId = flowId,
                    ShareId = ShareClass.Singleton,
                    FactorId = FactorFields.HugeRedemption,
                    Data = new HugeRedemptionRule { Has = true, Ratio = value }
                });
            }
        }
        if (this.CoolingPeriod is Mutable<global::FMO.Models.CoolingPeriodInfo> m_CoolingPeriod && m_CoolingPeriod.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_CoolingPeriod.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.CoolingPeriodInfo> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.CoolingPeriod, Data = value });
            }
        }
        if (this.Callback is Mutable<global::FMO.Models.CallbackInfo> m_Callback && m_Callback.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_Callback.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.CallbackInfo> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.Callback, Data = value });
            }
        }
        if (this.ManageFeePay is Mutable<global::FMO.Models.FeePayInfo> m_ManageFeePay && m_ManageFeePay.Changes.Count > 0)
        {
            foreach (var (flowId, value) in m_ManageFeePay.Changes)
            {
                Factors.Add(new FundFactor<global::FMO.Models.FeePayInfo> { FundId = fundId, FlowId = flowId, ShareId = ShareClass.Singleton, FactorId = FactorFields.ManageFeePay, Data = value });
            }
        }

        if (this.LockingRule is PortionMutable<global::FMO.Models.SealingRule> pm_LockingRule && pm_LockingRule.Changes.Count > 0)
        {
            foreach (var (flowId, shareDict) in pm_LockingRule.Changes)
            {
                foreach (var (shareId, value) in shareDict)
                {
                    Factors.Add(new FundFactor<global::FMO.Models.SealingRule> { FundId = fundId, FlowId = flowId, ShareId = shareId, FactorId = FactorFields.LockingRule, Data = value });
                }
            }
        }
        if (this.ManageFee is PortionMutable<global::FMO.Models.FundFeeInfo> pm_ManageFee && pm_ManageFee.Changes.Count > 0)
        {
            foreach (var (flowId, shareDict) in pm_ManageFee.Changes)
            {
                foreach (var (shareId, value) in shareDict)
                {
                    Factors.Add(new FundFactor<global::FMO.Models.FundFeeInfo> { FundId = fundId, FlowId = flowId, ShareId = shareId, FactorId = FactorFields.ManageFee, Data = value });
                }
            }
        }
        if (this.SubscriptionRule is PortionMutable<global::FMO.Models.FundPurchaseRule> pm_SubscriptionRule && pm_SubscriptionRule.Changes.Count > 0)
        {
            foreach (var (flowId, shareDict) in pm_SubscriptionRule.Changes)
            {
                foreach (var (shareId, value) in shareDict)
                {
                    Factors.Add(new FundFactor<global::FMO.Models.FundPurchaseRule> { FundId = fundId, FlowId = flowId, ShareId = shareId, FactorId = FactorFields.SubscriptionRule, Data = value });
                }
            }
        }
        if (this.PurchasRule is PortionMutable<global::FMO.Models.FundPurchaseRule> pm_PurchasRule && pm_PurchasRule.Changes.Count > 0)
        {
            foreach (var (flowId, shareDict) in pm_PurchasRule.Changes)
            {
                foreach (var (shareId, value) in shareDict)
                {
                    Factors.Add(new FundFactor<global::FMO.Models.FundPurchaseRule> { FundId = fundId, FlowId = flowId, ShareId = shareId, FactorId = FactorFields.PurchasRule, Data = value });
                }
            }
        }
        if (this.RedemptionFee is PortionMutable<global::FMO.Models.RedemptionFeeInfo> pm_RedemptionFee && pm_RedemptionFee.Changes.Count > 0)
        {
            foreach (var (flowId, shareDict) in pm_RedemptionFee.Changes)
            {
                foreach (var (shareId, value) in shareDict)
                {
                    Factors.Add(new FundFactor<global::FMO.Models.RedemptionFeeInfo> { FundId = fundId, FlowId = flowId, ShareId = shareId, FactorId = FactorFields.RedemptionFee, Data = value });
                }
            }
        }
        if (this.PerformanceFeeStatement is PortionMutable<string> pm_PerformanceFeeStatement && pm_PerformanceFeeStatement.Changes.Count > 0)
        {
            foreach (var (flowId, shareDict) in pm_PerformanceFeeStatement.Changes)
            {
                foreach (var (shareId, value) in shareDict)
                {
                    Factors.Add(new FundFactor<string> { FundId = fundId, FlowId = flowId, ShareId = shareId, FactorId = FactorFields.PerformanceFeeStatement, Data = value });
                }
            }
        }

        return Factors.ToArray();
    }


    public static FundElements From(IFundFactor[] Factors)
    {
        if (Factors == null || Factors.Length == 0)
            return new FundElements();

        var fundId = Factors[0].FundId;
        foreach (var f in Factors)
        {
            if (f.FundId != fundId)
                throw new ArgumentException($"All Factors must belong to the same fund. Expected FundId: {fundId}, but found: {f.FundId}");
        }

        var elements = new FundElements { Id = fundId };
        var FactorGroups = Factors.GroupBy(f => f.FactorId).ToDictionary(g => g.Key, g => g.ToList());

        if (FactorGroups.TryGetValue(FactorFields.FullName, out var g_FullName))
        {
            var target = elements.FullName;
            target.Changes.Clear();
            foreach (var f in g_FullName)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.FullName': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.FullName'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.ShortName, out var g_ShortName))
        {
            var target = elements.ShortName;
            target.Changes.Clear();
            foreach (var f in g_ShortName)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.ShortName': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.ShortName'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.SecurityFundType, out var g_SecurityFundType))
        {
            var target = elements.SecurityFundType;
            target.Changes.Clear();
            foreach (var f in g_SecurityFundType)
            {
                if (f is not FundFactor<global::FMO.Models.SecurityFundType> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.SecurityFundType': expected FundFactor<global::FMO.Models.SecurityFundType>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");

                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.FundModeInfo, out var g_FundModeInfo))
        {
            var target = elements.FundModeInfo;
            target.Changes.Clear();
            foreach (var f in g_FundModeInfo)
            {
                if (f is not FundFactor<global::FMO.Models.DataExtra<global::FMO.Models.FundMode>> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.FundModeInfo': expected FundFactor<global::FMO.Models.DataExtra<global::FMO.Models.FundMode>>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.FundModeInfo'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.SealingRule, out var g_SealingRule))
        {
            var target = elements.SealingRule;
            target.Changes.Clear();
            foreach (var f in g_SealingRule)
            {
                if (f is not FundFactor<global::FMO.Models.SealingRule> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.SealingRule': expected FundFactor<global::FMO.Models.SealingRule>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.SealingRule'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.RiskLevel, out var g_RiskLevel))
        {
            var target = elements.RiskLevel;
            target.Changes.Clear();
            foreach (var f in g_RiskLevel)
            {
                if (f is not FundFactor<global::FMO.Models.RiskLevel> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.RiskLevel': expected FundFactor<global::FMO.Models.RiskLevel>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");

                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.DurationInMonths, out var g_DurationInMonths))
        {
            var target = elements.DurationInMonths;
            target.Changes.Clear();
            foreach (var f in g_DurationInMonths)
            {
                if (f is not FundFactor<int> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.DurationInMonths': expected FundFactor<int>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");

                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.ExpirationDate, out var g_ExpirationDate))
        {
            var target = elements.ExpirationDate;
            target.Changes.Clear();
            foreach (var f in g_ExpirationDate)
            {
                if (f is not FundFactor<global::System.DateOnly> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.ExpirationDate': expected FundFactor<global::System.DateOnly>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");

                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.CollectionAccount, out var g_CollectionAccount))
        {
            var target = elements.CollectionAccount;
            target.Changes.Clear();
            foreach (var f in g_CollectionAccount)
            {
                if (f is not FundFactor<global::FMO.Models.BankAccount> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.CollectionAccount': expected FundFactor<global::FMO.Models.BankAccount>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.CollectionAccount'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.CustodyAccount, out var g_CustodyAccount))
        {
            var target = elements.CustodyAccount;
            target.Changes.Clear();
            foreach (var f in g_CustodyAccount)
            {
                if (f is not FundFactor<global::FMO.Models.BankAccount> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.CustodyAccount': expected FundFactor<global::FMO.Models.BankAccount>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.CustodyAccount'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.ShareClasses, out var g_ShareClasses))
        {
            var target = elements.ShareClasses;
            target.Changes.Clear();
            foreach (var f in g_ShareClasses)
            {
                if (f is not FundFactor<global::FMO.Models.ShareClass[]> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.ShareClasses': expected FundFactor<global::FMO.Models.ShareClass[]>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.ShareClasses'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.StopLine, out var g_StopLine))
        {
            var target = elements.StopLine;
            target.Changes.Clear();
            foreach (var f in g_StopLine)
            {
                if (f is not FundFactor<decimal> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.StopLine': expected FundFactor<decimal>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");

                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.WarningLine, out var g_WarningLine))
        {
            var target = elements.WarningLine;
            target.Changes.Clear();
            foreach (var f in g_WarningLine)
            {
                if (f is not FundFactor<decimal> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.WarningLine': expected FundFactor<decimal>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");

                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.OpenDayInfo, out var g_OpenDayInfo))
        {
            var target = elements.OpenDayInfo;
            target.Changes.Clear();
            foreach (var f in g_OpenDayInfo)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.OpenDayInfo': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.OpenDayInfo'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.FundOpenRule, out var g_FundOpenRule))
        {
            var target = elements.FundOpenRule;
            target.Changes.Clear();
            foreach (var f in g_FundOpenRule)
            {
                if (f is not FundFactor<OpenRule> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.FundOpenRule': expected FundFactor<global::FMO.OpenRule>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.FundOpenRule'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.TrusteeInfo, out var g_TrusteeInfo))
        {
            var target = elements.TrusteeInfo;
            target.Changes.Clear();
            foreach (var f in g_TrusteeInfo)
            {
                if (f is not FundFactor<global::FMO.Models.AgencyInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.TrusteeInfo': expected FundFactor<global::FMO.Models.AgencyInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.TrusteeInfo'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.TrusteeFee, out var g_TrusteeFee))
        {
            var target = elements.TrusteeFee;
            target.Changes.Clear();
            foreach (var f in g_TrusteeFee)
            {
                if (f is not FundFactor<global::FMO.Models.FundFeeInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.TrusteeFee': expected FundFactor<global::FMO.Models.FundFeeInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.TrusteeFee'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.OutsourcingInfo, out var g_OutsourcingInfo))
        {
            var target = elements.OutsourcingInfo;
            target.Changes.Clear();
            foreach (var f in g_OutsourcingInfo)
            {
                if (f is not FundFactor<global::FMO.Models.AgencyInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.OutsourcingInfo': expected FundFactor<global::FMO.Models.AgencyInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.OutsourcingInfo'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.OutsourcingFee, out var g_OutsourcingFee))
        {
            var target = elements.OutsourcingFee;
            target.Changes.Clear();
            foreach (var f in g_OutsourcingFee)
            {
                if (f is not FundFactor<global::FMO.Models.FundFeeInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.OutsourcingFee': expected FundFactor<global::FMO.Models.FundFeeInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.OutsourcingFee'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.InvestmentManagers, out var g_InvestmentManagers))
        {
            var target = elements.InvestmentManagers;
            target.Changes.Clear();
            foreach (var f in g_InvestmentManagers)
            {
                if (f is not FundFactor<global::FMO.Models.FundInvestmentManager[]> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.InvestmentManagers': expected FundFactor<global::FMO.Models.FundInvestmentManager[]>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.InvestmentManagers'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.InvestmentManager, out var g_InvestmentManager))
        {
            var target = elements.InvestmentManager;
            target.Changes.Clear();
            foreach (var f in g_InvestmentManager)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.InvestmentManager': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.InvestmentManager'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.PerformanceBenchmark, out var g_PerformanceBenchmark))
        {
            var target = elements.PerformanceBenchmark;
            target.Changes.Clear();
            foreach (var f in g_PerformanceBenchmark)
            {
                if (f is not FundFactor<global::FMO.Models.PerformanceBenchmark> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.PerformanceBenchmark': expected FundFactor<global::FMO.Models.PerformanceBenchmark>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.PerformanceBenchmark'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.InvestmentObjective, out var g_InvestmentObjective))
        {
            var target = elements.InvestmentObjective;
            target.Changes.Clear();
            foreach (var f in g_InvestmentObjective)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.InvestmentObjective': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.InvestmentObjective'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.InvestmentScope, out var g_InvestmentScope))
        {
            var target = elements.InvestmentScope;
            target.Changes.Clear();
            foreach (var f in g_InvestmentScope)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.InvestmentScope': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.InvestmentScope'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.InvestmentStrategy, out var g_InvestmentStrategy))
        {
            var target = elements.InvestmentStrategy;
            target.Changes.Clear();
            foreach (var f in g_InvestmentStrategy)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.InvestmentStrategy': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.InvestmentStrategy'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.TemporarilyOpenInfo, out var g_TemporarilyOpenInfo))
        {
            var target = elements.TemporarilyOpenInfo;
            target.Changes.Clear();
            foreach (var f in g_TemporarilyOpenInfo)
            {
                if (f is not FundFactor<global::FMO.Models.TemporarilyOpenInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.TemporarilyOpenInfo': expected FundFactor<global::FMO.Models.TemporarilyOpenInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.TemporarilyOpenInfo'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.HugeRedemption, out var g_HugeRedemptionRatio))
        {
            var target = elements.HugeRedemptionRatio;
            target.Changes.Clear();
            foreach (var f in g_HugeRedemptionRatio)
            {
                if (f is not FundFactor<decimal> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.HugeRedemption': expected FundFactor<decimal>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");

                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.CoolingPeriod, out var g_CoolingPeriod))
        {
            var target = elements.CoolingPeriod;
            target.Changes.Clear();
            foreach (var f in g_CoolingPeriod)
            {
                if (f is not FundFactor<global::FMO.Models.CoolingPeriodInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.CoolingPeriod': expected FundFactor<global::FMO.Models.CoolingPeriodInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.CoolingPeriod'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.Callback, out var g_Callback))
        {
            var target = elements.Callback;
            target.Changes.Clear();
            foreach (var f in g_Callback)
            {
                if (f is not FundFactor<global::FMO.Models.CallbackInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.Callback': expected FundFactor<global::FMO.Models.CallbackInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.Callback'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.ManageFeePay, out var g_ManageFeePay))
        {
            var target = elements.ManageFeePay;
            target.Changes.Clear();
            foreach (var f in g_ManageFeePay)
            {
                if (f is not FundFactor<global::FMO.Models.FeePayInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.ManageFeePay': expected FundFactor<global::FMO.Models.FeePayInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.ManageFeePay'. FundId: {f.FundId}, FlowId: {f.FlowId}");
                target.SetValue(ff.Data!, f.FlowId);
            }
        }

        if (FactorGroups.TryGetValue(FactorFields.LockingRule, out var pg_LockingRule))
        {
            var target = elements.LockingRule;
            target.Changes.Clear();
            foreach (var f in pg_LockingRule)
            {
                if (f is not FundFactor<global::FMO.Models.SealingRule> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.LockingRule': expected FundFactor<global::FMO.Models.SealingRule>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.LockingRule'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                target.SetValue(f.ShareId, ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.ManageFee, out var pg_ManageFee))
        {
            var target = elements.ManageFee;
            target.Changes.Clear();
            foreach (var f in pg_ManageFee)
            {
                if (f is not FundFactor<global::FMO.Models.FundFeeInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.ManageFee': expected FundFactor<global::FMO.Models.FundFeeInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.ManageFee'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                target.SetValue(f.ShareId, ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.SubscriptionRule, out var pg_SubscriptionRule))
        {
            var target = elements.SubscriptionRule;
            target.Changes.Clear();
            foreach (var f in pg_SubscriptionRule)
            {
                if (f is not FundFactor<global::FMO.Models.FundPurchaseRule> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.SubscriptionRule': expected FundFactor<global::FMO.Models.FundPurchaseRule>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.SubscriptionRule'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                target.SetValue(f.ShareId, ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.PurchasRule, out var pg_PurchasRule))
        {
            var target = elements.PurchasRule;
            target.Changes.Clear();
            foreach (var f in pg_PurchasRule)
            {
                if (f is not FundFactor<global::FMO.Models.FundPurchaseRule> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.PurchasRule': expected FundFactor<global::FMO.Models.FundPurchaseRule>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.PurchasRule'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                target.SetValue(f.ShareId, ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.RedemptionFee, out var pg_RedemptionFee))
        {
            var target = elements.RedemptionFee;
            target.Changes.Clear();
            foreach (var f in pg_RedemptionFee)
            {
                if (f is not FundFactor<global::FMO.Models.RedemptionFeeInfo> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.RedemptionFee': expected FundFactor<global::FMO.Models.RedemptionFeeInfo>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.RedemptionFee'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                target.SetValue(f.ShareId, ff.Data!, f.FlowId);
            }
        }
        if (FactorGroups.TryGetValue(FactorFields.PerformanceFeeStatement, out var pg_PerformanceFeeStatement))
        {
            var target = elements.PerformanceFeeStatement;
            target.Changes.Clear();
            foreach (var f in pg_PerformanceFeeStatement)
            {
                if (f is not FundFactor<string> ff)
                    throw new InvalidOperationException($"Factor type mismatch for FactorId 'FactorFields.PerformanceFeeStatement': expected FundFactor<string>, but got '{f.GetType().Name}'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                if (ff.Data == null)
                    throw new InvalidOperationException($"Factor data is null for FactorId 'FactorFields.PerformanceFeeStatement'. FundId: {f.FundId}, FlowId: {f.FlowId}, ShareId: {f.ShareId}");
                target.SetValue(f.ShareId, ff.Data!, f.FlowId);
            }
        }
        return elements;
    }
}