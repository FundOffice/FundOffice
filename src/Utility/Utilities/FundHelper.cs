using FMO.Models;
using System.Collections.Concurrent;
using System.Data;

namespace FMO.Utilities;

public static class FundHelper
{

    private static ConcurrentDictionary<int, string> FundStorageMap { get; } = new();

    public static DirectoryInfo Folder(this Fund fund) => GetFolder(fund.Id);

    public static DirectoryInfo GetFolder(int fundId)
    {
        if (FundStorageMap.TryGetValue(fundId, out var folder))
            return new DirectoryInfo(folder);

        var dis = new DirectoryInfo(@"files\funds").GetDirectories();
        var di = dis.FirstOrDefault(x => x.Name.StartsWith($"{fundId}."));
        if (di is not null)
            FundStorageMap.AddOrUpdate(fundId, di.FullName, (a, b) => di.FullName);

        return new DirectoryInfo(FundStorageMap[fundId]);
    }

    public static string GetFolder(int fundId, string sub)
    {
        if (FundStorageMap.TryGetValue(fundId, out var folder))
            return Path.Combine(FundStorageMap[fundId], sub);

        var dis = new DirectoryInfo(@"files\funds").GetDirectories();
        var di = dis.FirstOrDefault(x => x.Name.StartsWith($"{fundId}."));
        if (di is not null)
            FundStorageMap.AddOrUpdate(fundId, di.FullName, (a, b) => di.FullName);

        return Path.Combine(FundStorageMap[fundId], sub);
    }


    public static void Map(Fund fund, string folder)
    {
        FundStorageMap.AddOrUpdate(fund.Id, folder, (a, b) => folder);
    }

    /// <summary>
    /// 初始化一个新的基金
    /// </summary>
    /// <param name="fund"></param>
    public static void InitNew(Fund fund)
    {
        var name = $"{fund.Code}.{fund.Name}";
        string folder = $"files\\funds\\{name}";
        Directory.CreateDirectory(folder);

        InitFundFlowAndFactor(fund);

        Map(fund, folder);
    }

    public static void InitFundFlowAndFactor(Fund fund)
    {
        using var db = DbHelper.Base();
        var flows = db.GetCollection<FundFlow>().Find(x => x.FundId == fund.Id).OrderBy(x => x.Id).ToList();
        if (!flows.Any(x => x is InitiateFlow))
        {
            var f = new InitiateFlow { FundId = fund.Id, ElementFiles = new() { Label = "基金要素" }, ContractFiles = new() { Label = "基金合同" }, CustomFiles = new() };
            flows.Insert(0, f);
            db.GetCollection<FundFlow>().Insert(f);
        }

        if (!flows.Any(x => x is ContractFinalizeFlow))
        {
            var f = new ContractFinalizeFlow { FundId = fund.Id, CustomFiles = new() };
            flows.Insert(1, f);
            db.GetCollection<FundFlow>().Insert(f);
            // 默认份额类型
            db.GetCollection<IFundFactor>().Upsert(new FundFactor<ShareClass[]>(FactorFields.ShareClasses, fund.Id, f.Id, [ShareClass.FromFlowSingleton(f.Id, fund.Name, fund.Code)]));
        }

        if (fund.Status >= FundStatus.Setup && !flows.Any(x => x is SetupFlow))
        {
            var f = new SetupFlow { FundId = fund.Id, Date = fund.SetupDate, CustomFiles = new() };
            flows.Insert(2, f);
            db.GetCollection<FundFlow>().Insert(f);
        }


        if (fund.Status >= FundStatus.Registration && !flows.Any(x => x is RegistrationFlow))
        {
            var f = new RegistrationFlow { FundId = fund.Id, Date = fund.AuditDate, CustomFiles = new() };
            flows.Add(f);
            db.GetCollection<FundFlow>().Insert(f);
        }

        if (fund.Status >= FundStatus.StartLiquidation && !flows.Any(x => x is LiquidationFlow))
        {
            var f = new LiquidationFlow { FundId = fund.Id, CustomFiles = new() };
            flows.Add(f);
            db.GetCollection<FundFlow>().Insert(f);
        }




    }



    public static (Fund?, string? Class) FindByName(this Fund[] funds, string name)
    {
        var fund = funds.FirstOrDefault(x => x.Name == name);
        if (fund is not null) return (fund, null);

        // 尝试通过名称包含来查找 xxA xxB等子份额
        var poss = funds.Where(x => name.StartsWith(x.Name)).ToArray();
        if (poss.Length == 1)
            return (poss[0], name[poss[0].Name.Length..]);


        return (null, null);
    }

}
