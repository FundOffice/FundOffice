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

        using var db = DbHelper.Base();
        db.GetCollection<Fund>().Insert(fund);
        InitiateFlow flow = new() { FundId = fund.Id, ElementFiles = new MultiFile { Label = "基金要素" }, ContractFiles = new MultiFile { Label = "基金合同" }, CustomFiles = new() };
        db.GetCollection<FundFlow>().Insert(flow);
        db.GetCollection<FundElements>().Insert(FundElements.Create(fund.Id, flow.Id));


        Map(fund, folder);
    }

    /// <summary>
    /// 生成每日份额表
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    public static (IList<DateOnly> Dates, IList<int> InvestorIds, IList<string> Names, decimal[,] Data) GenerateShareSheet(int fundId, DateOnly begin, DateOnly end)
    {
        using var db = DbHelper.Base();

        IEnumerable<TransferRecord> uncheck = db.GetCollection<TransferRecord>().Find(x => x.FundId == 0);
        if (uncheck.Count() > 0)
        {
            var funds = db.GetCollection<Fund>().FindAll().Select(x => new { x.Id, x.Code, x.Name }).ToArray();
            foreach (var item in uncheck)
            {
                item.FundId = (funds.FirstOrDefault(x => x.Code == item.FundCode) ?? funds.FirstOrDefault(x => x.Name == item.FundName))!.Id;
            }
            db.GetCollection<TransferRecord>().Update(uncheck);
        }

        // 计算份额表，排除已全部赎回的
        var data = db.GetCollection<TransferRecord>().Find(x => x.FundId == fundId).OrderBy(x => x.ConfirmedDate).ToList();
        data = data.GroupBy(x => x.InvestorId).Where(x => x.Max(y => y.ConfirmedDate) >= begin || x.Sum(y => y.ShareChange()) > 0).SelectMany(x => x).ToList();

        /// 生成行、列头
        List<DateOnly> dates = new List<DateOnly>();
        var idname = data.Select(x => (x.InvestorId, x.InvestorName)).DistinctBy(x => x.InvestorId);
        var ids = idname.Select(x => x.InvestorId).ToList();
        var names = idname.Select(x => x.InvestorName).ToList();

        var date = begin;
        while (date <= end)
        {
            dates.Add(date);
            date = date.AddDays(1);
        }

        var array = new decimal[dates.Count, ids.Count];

        Dictionary<DateOnly, Dictionary<int, decimal>> result = new();


        for (int i = 0; i < dates.Count; i++)
        {
            foreach (var d in data)
            {
                if (d.ConfirmedDate >= dates[i]) continue;

                var cid = ids.IndexOf(d.InvestorId);
                array[i, cid] += d.ShareChange();
            }
        }


        return (dates, ids, names, array);
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
