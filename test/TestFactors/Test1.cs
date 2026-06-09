using FMO.Models;
using FMO.Utilities;
using Initial;

namespace TestFactors;

[TestClass]
public sealed class Test1
{
    [TestInitialize]
    public void TestInit()
    {
        DataInject.SetAsDebug();
    }

    [TestMethod]
    public void TestReadonlyFundInfoPair()
    {
        using var db = DbHelper.Base();

        var fund = db.GetCollection<Fund>().Query().Select(x => x.Id).ToArray();
        foreach (var f in fund)
        {
            var flows = db.GetCollection<FundFlow>().Query().Where(x => x.FundId == f).ToEnumerable().OfType<ContractFlow>().Select(x => x.Id).ToArray();

            foreach (var fl in flows)
            {
                var factors = db.GetCollection<IFundFactor>().Query().Where(x => x.FundId == f && x.FlowId <= fl).ToArray();

                var ff = new FundFactors(1, factors);
                var ri = new ReadonlyFundInfo();
                ri.FillBy(factors);

                // ── Singleton 属性 ──
                Assert.AreEqual(ff.FullName.Current, ri.FullName, "FullName");
                Assert.AreEqual(ff.ShortName.Current, ri.ShortName, "ShortName");
                Assert.AreEqual(ff.SecurityFundType.Current, ri.SecurityFundType, "SecurityFundType");
                Assert.AreEqual(ff.FundModeInfo.Current, ri.FundModeInfo, "FundModeInfo");
                Assert.AreEqual(ff.SealingRule.Current, ri.SealingRule, "SealingRule");
                Assert.AreEqual(ff.RiskLevel.Current, ri.RiskLevel, "RiskLevel");
                Assert.AreEqual(ff.DurationInMonths.Current, ri.DurationInMonths, "DurationInMonths");
                Assert.AreEqual(ff.ExpirationDate.Current, ri.ExpirationDate, "ExpirationDate");
                Assert.AreEqual(ff.StructureInfo.Current, ri.StructureInfo, "StructureInfo");
                Assert.AreEqual(ff.CollectionAccount.Current, ri.CollectionAccount, "CollectionAccount");
                Assert.AreEqual(ff.CustodyAccount.Current, ri.CustodyAccount, "CustodyAccount");
                Assert.AreEqual(ff.StopLine.Current, ri.StopLine, "StopLine");
                Assert.AreEqual(ff.WarningLine.Current, ri.WarningLine, "WarningLine");
                Assert.AreEqual(ff.HugeRedemption.Current, ri.HugeRedemption, "HugeRedemption");
                Assert.AreEqual(ff.CoolingPeriod.Current, ri.CoolingPeriod, "CoolingPeriod");
                Assert.AreEqual(ff.Callback.Current, ri.Callback, "Callback");
                Assert.AreEqual(ff.TrusteeInfo.Current, ri.TrusteeInfo, "TrusteeInfo");
                Assert.AreEqual(ff.OutsourcingInfo.Current, ri.OutsourcingInfo, "OutsourcingInfo");
                Assert.AreEqual(ff.TrusteeFee.Current, ri.TrusteeFee, "TrusteeFee");
                Assert.AreEqual(ff.OutsourcingFee.Current, ri.OutsourcingFee, "OutsourcingFee");
                Assert.AreEqual(ff.ManageFeePay.Current, ri.ManageFeePay, "ManageFeePay");
                Assert.AreEqual(ff.InvestmentManagers.Current, ri.InvestmentManagers, "InvestmentManagers");
                Assert.AreEqual(ff.InvestmentManager.Current, ri.InvestmentManager, "InvestmentManager");
                Assert.AreEqual(ff.PerformanceBenchmark.Current, ri.PerformanceBenchmark, "PerformanceBenchmark");
                Assert.AreEqual(ff.InvestmentObjective.Current, ri.InvestmentObjective, "InvestmentObjective");
                Assert.AreEqual(ff.InvestmentScope.Current, ri.InvestmentScope, "InvestmentScope");
                Assert.AreEqual(ff.InvestmentStrategy.Current, ri.InvestmentStrategy, "InvestmentStrategy");

                // ── FactorItem 属性 ──
                CollectionAssert.AreEqual(ff.TemporarilyOpenInfo.Current, ri.TemporarilyOpenInfo, "TemporarilyOpenInfo");
                // FundOpenRule: FactorItem<OpenRule[]> vs OpenRule?
                // FundOpenRule: OpenRule[] 没有重写 Equals，用引用比较会失败，改为逐元素深度比较
                Assert.AreEqual(ff.FundOpenRule.Current.Length, ri.FundOpenRule?.Length ?? 0, "FundOpenRule.Length");
                for (int j = 0; j < ff.FundOpenRule.Current.Length; j++)
                {
                    var ffArr = ff.FundOpenRule.Current[j];
                    var riArr = ri.FundOpenRule![j];
                    Assert.AreEqual(ffArr?.Length ?? 0, riArr?.Length ?? 0, $"FundOpenRule[{j}].Length");
                    if (ffArr != null && riArr != null)
                        for (int k = 0; k < ffArr.Length; k++)
                        {
                            Assert.AreEqual(ffArr[k].Type, riArr[k].Type, $"FundOpenRule[{j}][{k}].Type");
                            Assert.AreEqual(ffArr[k].AllowBuy, riArr[k].AllowBuy, $"FundOpenRule[{j}][{k}].AllowBuy");
                            Assert.AreEqual(ffArr[k].AllowSell, riArr[k].AllowSell, $"FundOpenRule[{j}][{k}].AllowSell");
                        }
                }
                CollectionAssert.AreEqual(ff.LockingRule.Current, ri.LockingRule, "LockingRule");
                CollectionAssert.AreEqual(ff.SubscriptionRule.Current, ri.SubscriptionRule, "SubscriptionRule");
                CollectionAssert.AreEqual(ff.PurchasRule.Current, ri.PurchasRule, "PurchasRule");
                CollectionAssert.AreEqual(ff.ManageFee.Current, ri.ManageFee, "ManageFee");
                CollectionAssert.AreEqual(ff.RedemptionFee.Current, ri.RedemptionFee, "RedemptionFee");
                CollectionAssert.AreEqual(ff.PerformanceFeeStatement.Current, ri.PerformanceFeeStatement, "PerformanceFeeStatement");
            }
        }

        
    }
}