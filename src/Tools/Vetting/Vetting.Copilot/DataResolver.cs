using System.Text.Json;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;

namespace Vetting.Copilot;

/// <summary>
/// 根据 entity.property 解析实际值（纯数据容器，不依赖数据库，不使用反射）
/// </summary>
public class DataResolver
{
    private readonly Dictionary<string, IResolve> _scalars;
    private readonly Dictionary<string, Dictionary<string, string>[]> _lists;
    private readonly Dictionary<int, FundInfo> _recommendFunds;
    private readonly Dictionary<string, string> _answersByQuestion;
    private readonly string? _fileName;
    private readonly Dictionary<string, int> _fundBindings; // RangeKey → FundId

    public DataResolver(
        Dictionary<string, IResolve> scalars,
        Dictionary<string, Dictionary<string, string>[]> lists,
        Dictionary<int, FundInfo> recommendFunds,
        Dictionary<string, string> answersByQuestion,
        string? fileName,
        Dictionary<string, int> fundBindings)
    {
        _scalars = scalars;
        _lists = lists;
        _recommendFunds = recommendFunds;
        _fileName = fileName;
        _answersByQuestion = answersByQuestion;
        _fundBindings = fundBindings;
    }

    /// <summary>
    /// 从 LiteDB 加载所有数据并构造 DataResolver
    /// </summary>
    public static DataResolver Load(string fileName, string providerId, int[]? recommendIds = null)
    {
        using var db = new VettingDbContext();

        var manager = db.Managers.FindById(1) ?? new Manager();
        var credit = db.CreditStandings.FindById(1) ?? new CreditStanding();
        var invest = db.InvestmentInfos.FindById(1) ?? new InvestmentInfo();
        var risk = db.RiskControls.FindById(1) ?? new RiskControl();

        var allStaff = db.Staffs.FindAll().ToArray();
        var allShareholders = db.Shareholders.FindAll().ToArray();
        var allDepts = db.Departments.FindAll().ToArray();
        var allStrategies = db.Strategies.FindAll().ToArray();
        var allFunds = db.FundInfos.FindAll().ToArray();
        var allAwards = db.Awards.FindAll().ToArray();

        var scalars = new Dictionary<string, IResolve>
        {
            ["manager"] = manager,
            ["credit"] = credit,
            ["invest"] = invest,
            ["risk"] = risk,
        };

        var lists = new Dictionary<string, Dictionary<string, string>[]>
        {
            ["shareholder"] = allShareholders.Select(s => ObjectToDictViaResolve(s, new[]
                { "Name", "Ratio", "Intro", "Nature", "PaidInAmount", "IdentityBrief", "CompanyRole", "IsCoreResearch", "CompanyPosition" })).ToArray(),
            ["actualcontroller"] = allShareholders
                .Where(s => s.IsActualController)
                .Select(s => new Dictionary<string, string>
                {
                    ["Name"] = ResolveHelper.ToString(s.Name),
                    ["Penetration"] = ResolveHelper.ToString(s.Ratio),
                    ["Intro"] = ResolveHelper.ToString(s.Intro),
                }).ToArray(),
            ["department"] = allDepts.Select(d =>
            {
                var dict = ObjectToDictViaResolve(d, new[] { "Name", "MainFunction", "Head" });
                dict["StaffCount"] = allStaff.Count(s => s.DepartmentId == d.Id).ToString();
                return dict;
            }).ToArray(),
            ["strategy"] = allStrategies.Select(s => ObjectToDictViaResolve(s, new[]
                { "Name", "Manager", "Scale", "Type" })).ToArray(),
            ["fund"] = allFunds.Select(f => ObjectToDictViaResolve(f, new[]
                { "Name", "Code", "Duration", "Type", "MinSubscription", "Frequency", "Custodian",
                  "RiskLevel", "BuySellFee", "MgmtFee", "CustodyFee", "Scope", "Restriction",
                  "WarningStoploss", "PerformanceFee", "Dividend", "Other", "EstablishmentDate",
                  "LockupPeriod", "OpeningDay", "FilingOrRegistration", "StrategyType", "NavDate",
                  "Scale", "IssueScale", "CurrentScale", "UnitNav", "CumulativeNav", "AnnualReturn",
                  "MaxDrawdown", "Volatility", "Sharpe", "Calmar", "CumulativeReturn",
                  "Return6M", "Return1Y", "Return1M" })).ToArray(),
            ["award"] = allAwards.Select(a => ObjectToDictViaResolve(a, new[]
                { "Time", "Entity", "Name", "Evaluator" })).ToArray(),
            ["executive"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.高管) && !s.HasLeft)
                .Select(s => StaffToDict(s)).ToArray(),
            ["researcher"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.投研) && !s.HasLeft)
                .Select(s => StaffToDict(s)).ToArray(),
            ["riskctrl"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.风控) && !s.HasLeft)
                .Select(s => StaffToDict(s)).ToArray(),
            ["pm"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.投资经理) && !s.HasLeft)
                .Select(s => StaffToDict(s)).ToArray(),
            ["contact"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.联系人) && !s.HasLeft)
                .Select(s => StaffToDict(s)).ToArray(),
            ["compliance"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.合规) && !s.HasLeft)
                .Select(s => StaffToDict(s)).ToArray(),
            ["departedstaff"] = allStaff.Where(s => s.HasLeft)
                .Select(s => StaffToDict(s)).ToArray(),
        };

        var fsList = db.FinancialStatements.FindAll().OrderByDescending(f => f.Year).ToArray();
        lists["financialstatement"] = fsList.Select(f => ObjectToDictViaResolve(f, new[]
            { "Year", "TotalAssets", "TotalLiabilities", "OwnersEquity", "Revenue", "Cost", "NetProfit" })).ToArray();

        var drList = db.DrawdownRecords.FindAll().OrderByDescending(d => d.Date).ToArray();
        lists["drawdownrecord"] = drList.Select(d => ObjectToDictViaResolve(d, new[]
            { "ProductName", "Date", "Amplitude", "Reason", "Countermeasures", "RecoveryDays" })).ToArray();

        var aumList = db.AUMs.FindAll().OrderByDescending(a => a.Year).ToArray();
        lists["aum"] = aumList.Select(a => ObjectToDictViaResolve(a, new[] { "Year", "Scale" })).ToArray();

        // staffcount: derive from Staff JoinDate/LeaveDate
        var staffCountByYear = new Dictionary<int, int>();
        foreach (var s in allStaff)
        {
            if (s.JoinDate == null) continue;
            var startYear = s.JoinDate.Value.Year;
            var endYear = s.LeaveDate?.Year ?? 9999;
            for (int y = startYear; y <= Math.Min(endYear, DateTime.Now.Year); y++)
                staffCountByYear[y] = staffCountByYear.GetValueOrDefault(y) + 1;
        }
        lists["staffcount"] = staffCountByYear
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new Dictionary<string, string> { ["Year"] = kv.Key.ToString(), ["Count"] = kv.Value.ToString() })
            .ToArray();

        // productline
        var productLineList = db.ProductLines.FindAll().OrderByDescending(p => p.Id).ToArray();
        lists["productline"] = productLineList.Select(p => ObjectToDictViaResolve(p, new[]
            { "Name", "StrategyType", "SpecificStrategy", "RepresentProduct", "Manager", "FundCount", "Scale", "TradingScale", "Capacity" })).ToArray();

        recommendIds ??= LoadGlobalRecommendIds(db);
        var recommendFunds = new Dictionary<int, FundInfo>();
        for (int i = 0; i < recommendIds.Length; i++)
        {
            var fund = allFunds.FirstOrDefault(f => f.Id == recommendIds[i]);
            if (fund != null)
                recommendFunds[i] = fund;
        }
        // 加载表格绑定（从 FundBinding 表，按 FileName 过滤）
        var fundBindings = new Dictionary<string, int>();
        foreach (var b in db.FundBindings.Find(b => b.FileName == fileName))
            fundBindings[b.Range ?? ""] = b.FundId;

        // 加载自定义问题答案

        var answersByQuestion = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(fileName))
        {
            var questions = db.FileSpecialQuestions.Find(q => q.FileName == fileName && q.Provider == providerId).ToArray();
            var answers = db.SpecialAnswers.Query().ToEnumerable()
                .Where(a => questions.Any(q => q.Id == a.QuestionId)).ToArray();
            foreach (var q in questions)
            {
                var best = answers.Where(a => a.QuestionId == q.Id)
                    .OrderByDescending(a => a.Identifier == "manual" ? 1 : 0)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(q.Question))
                    answersByQuestion[q.Question] = best?.Value ?? "";
            }
        }

        return new DataResolver(scalars, lists, recommendFunds, answersByQuestion, fileName, fundBindings);
    }

    /// <summary>

    /// 为 Type b 解析属性，查找优先级：FundBinding → file 级推荐 → global 推荐
    /// </summary>
    public string ResolveRecommendForFund(int fundIndex, Range range, string property, string? header = null, string? format = null)
    {
        // 1. 优先查找 FundBinding
        var key = range.ToKey();
        if (_fundBindings.TryGetValue(key, out var boundFundId))
        {
            var fund = FindFundById(boundFundId);
            if (fund != null)
            {
                var value = fund.Resolve(property);
                return ResolveHelper.ToString(value, format);
            }
        }

        // 2. Fallback: 按 fundIndex 从推荐列表取
        return ResolveRecommend(fundIndex, property, format);
    }

    /// <summary>
    /// 按 FundId 在推荐列表和数据库中查找基金
    /// </summary>
    private FundInfo? FindFundById(int fundId)
    {
        foreach (var f in _recommendFunds.Values)
            if (f.Id == fundId) return f;

        using var db = new VettingDbContext();
        return db.FundInfos.FindById(fundId);
    }

    /// 解析单值实体属性（Type a / Type z with entity）
    /// </summary>
    public string Resolve(string entity, string property, string? format = null)
    {
        if (!_scalars.TryGetValue(entity, out var obj)) return "";
        var value = obj.Resolve(property);
        return ResolveHelper.ToString(value, format);
    }

    /// <summary>
    /// 解析推荐产品属性（Type b，按 fundIndex）
    /// </summary>
    public string ResolveRecommend(int fundIndex, string property, string? format = null)
    {
        if (!_recommendFunds.TryGetValue(fundIndex, out var fund)) return "";
        var value = fund.Resolve(property);
        return ResolveHelper.ToString(value, format);
    }

    /// <summary>
    /// 获取散装问题答案（Type z without entity）— 按问题文本匹配
    /// </summary>
    public string GetAnswerByQuestion(string question)
    {
        return _answersByQuestion.TryGetValue(question, out var answer) ? answer : "";
    }

    /// <summary>
    /// 获取列表实体（Type c/d/e）
    /// </summary>
    public Dictionary<string, string>[] GetList(string entity)
    {
        return _lists.TryGetValue(entity, out var list) ? list : [];
    }

    /// <summary>
    /// 通过 IResolve 将对象转为字典
    /// </summary>
    private static Dictionary<string, string> ObjectToDictViaResolve(IResolve src, string[] properties)
    {
        var dict = new Dictionary<string, string>();
        foreach (var prop in properties)
            dict[prop] = ResolveHelper.ToString(src.Resolve(prop));
        return dict;
    }

    private static Dictionary<string, string> StaffToDict(Staff s)
    {
        return ObjectToDictViaResolve(s, new[]
        {
            "Name", "Title", "Duty", "Department", "Education", "Profile", "IdNumber", "Years",
            "Age", "BirthDate", "JoinDate", "LeaveDate", "LeaveReason", "HasPartTimeJob",
            "Specialty", "ResearchFocus",
            "MobilePhone", "Telephone", "Email"
        });
    }

    private static int[] LoadGlobalRecommendIds(VettingDbContext db)
    {
        var rec = db.TemplateRecommends.FindOne(r => r.FileName == "__global__");
        if (rec?.FundIds == null) return [];
        return rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }
}
