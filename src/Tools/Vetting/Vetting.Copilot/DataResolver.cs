using System.Text.Json;
using System.Text.RegularExpressions;
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
    public static DataResolver Load(string fileName, string providerId, int[]? recommendIds = null, bool excludeInferred = false)
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
                .Select(s => StaffToDict(s, allDepts)).ToArray(),
            ["researcher"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.投研) && !s.HasLeft)
                .Select(s => StaffToDict(s, allDepts)).ToArray(),
            ["riskctrl"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.风控) && !s.HasLeft)
                .Select(s => StaffToDict(s, allDepts)).ToArray(),
            ["pm"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.投资经理) && !s.HasLeft)
                .Select(s => StaffToDict(s, allDepts)).ToArray(),
            ["contact"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.联系人) && !s.HasLeft)
                .Select(s => StaffToDict(s, allDepts)).ToArray(),
            ["compliance"] = allStaff.Where(s => s.Role.HasFlag(StaffRole.合规) && !s.HasLeft)
                .Select(s => StaffToDict(s, allDepts)).ToArray(),
            ["departedstaff"] = allStaff.Where(s => s.HasLeft)
                .Select(s => StaffToDict(s, allDepts)).ToArray(),
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
        // 优先级：QA 表（manual 最高） > SpecialAnswers（per-file）
        var answersByQuestion = new Dictionary<string, string>();

        // 1) 先加载 SpecialAnswers（per-file），作为基础层
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
                {
                    var value = best?.Value ?? "";
                    // 精确模式下排除 AI 推断的回答
                    if (excludeInferred && value.StartsWith("（ai）"))
                        value = "";
                    answersByQuestion[q.Question] = value;
                }
            }
        }

        // 2) QA 表手动答案覆盖（question 完全一致时优先级最高）
        foreach (var qa in db.QA.FindAll().ToArray())
        {
            if (!string.IsNullOrEmpty(qa.Question) && !string.IsNullOrEmpty(qa.Answer))
                answersByQuestion[qa.Question] = qa.Answer;
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

    private static Dictionary<string, string> StaffToDict(Staff s, Department[] allDepts)
    {
        var dict = ObjectToDictViaResolve(s, new[]
        {
            "Name", "Title", "Duty", "Education", "Profile", "IdNumber", "Years",
            "Age", "BirthDate", "JoinDate", "LeaveDate", "LeaveReason", "HasPartTimeJob",
            "Specialty", "ResearchFocus",
            "MobilePhone", "Telephone", "Email"
        });
        // Department 通过 DepartmentId 查找，而非 Staff.Department 字符串
        dict["Department"] = s.DepartmentId.HasValue
            ? allDepts.FirstOrDefault(d => d.Id == s.DepartmentId.Value)?.Name ?? ""
            : "";
        return dict;
    }

    private static int[] LoadGlobalRecommendIds(VettingDbContext db)
    {
        var rec = db.TemplateRecommends.FindOne(r => r.FileName == "__global__");
        if (rec?.FundIds == null) return [];
        return rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }

    // entity#id 占位符查找映射：entity name → (db, id) => IResolve?
    private static readonly Dictionary<string, Func<VettingDbContext, int, IResolve?>> EntityByIdResolvers = new()
    {
        ["fund"] = (db, id) => db.FundInfos.FindById(id),
        ["staff"] = (db, id) => db.Staffs.FindById(id),
        ["executive"] = (db, id) => db.Staffs.FindById(id),
        ["researcher"] = (db, id) => db.Staffs.FindById(id),
        ["riskctrl"] = (db, id) => db.Staffs.FindById(id),
        ["pm"] = (db, id) => db.Staffs.FindById(id),
        ["contact"] = (db, id) => db.Staffs.FindById(id),
        ["compliance"] = (db, id) => db.Staffs.FindById(id),
        ["shareholder"] = (db, id) => db.Shareholders.FindById(id),
        ["actualcontroller"] = (db, id) => db.Shareholders.FindById(id),
        ["department"] = (db, id) => db.Departments.FindById(id),
        ["strategy"] = (db, id) => db.Strategies.FindById(id),
        ["award"] = (db, id) => db.Awards.FindById(id),
        ["financialstatement"] = (db, id) => db.FinancialStatements.FindById(id),
        ["drawdownrecord"] = (db, id) => db.DrawdownRecords.FindById(id),
        ["aum"] = (db, id) => db.AUMs.FindById(id),
        ["productline"] = (db, id) => db.ProductLines.FindById(id),
    };

    private static readonly Regex PlaceholderRegex = new(@"\{\{(.+?)\}\}", RegexOptions.Compiled);

    /// <summary>
    /// 解析文本中的 {{...}} 占位符。
    /// 支持：{{entity.property}}、{{entity#id.property}}、{{entity.property:format}}
    /// </summary>
    public string ResolvePlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{")) return text;

        return PlaceholderRegex.Replace(text, match =>
        {
            var inner = match.Groups[1].Value;
            try
            {
                return inner.Contains('#') ? ResolveById(inner) : ResolveByScalar(inner);
            }
            catch
            {
                return match.Value; // 解析失败保留原样
            }
        });
    }

    private string ResolveByScalar(string inner)
    {
        var (property, format) = SplitFormat(inner);
        var dot = property.IndexOf('.');
        if (dot <= 0) return $"{{{{{inner}}}}}";

        var entity = property[..dot];
        var prop = property[(dot + 1)..];

        if (!_scalars.TryGetValue(entity, out var obj)) return $"{{{{{inner}}}}}";
        var value = obj.Resolve(prop);
        return ResolveHelper.ToString(value, format);
    }

    private string ResolveById(string inner)
    {
        var (property, format) = SplitFormat(inner);
        var hashIdx = property.IndexOf('#');
        var dotIdx = property.IndexOf('.', hashIdx + 1);
        if (hashIdx < 0 || dotIdx < 0) return $"{{{{{inner}}}}}";

        var entity = property[..hashIdx];
        var idStr = property[(hashIdx + 1)..dotIdx];
        var prop = property[(dotIdx + 1)..];

        if (!int.TryParse(idStr, out var id)) return $"{{{{{inner}}}}}";

        IResolve? obj = null;

        // 优先从 _recommendFunds 查找（与 FindFundById 一致）
        if (entity == "fund")
            obj = _recommendFunds.Values.FirstOrDefault(f => f.Id == id);

        // fallback: LiteDB 按 Id 查找
        obj ??= ResolveFromDb(entity, id);
        if (obj == null) return $"{{{{{inner}}}}}";

        var value = obj.Resolve(prop);
        return ResolveHelper.ToString(value, format);
    }

    private static IResolve? ResolveFromDb(string entity, int id)
    {
        if (!EntityByIdResolvers.TryGetValue(entity, out var resolver)) return null;
        using var db = new VettingDbContext();
        return resolver(db, id);
    }

    private static (string property, string? format) SplitFormat(string inner)
    {
        var colonIdx = inner.LastIndexOf(':');
        if (colonIdx > 0 && colonIdx < inner.Length - 1)
            return (inner[..colonIdx], inner[(colonIdx + 1)..]);
        return (inner, null);
    }

    // ═══ 图片占位符处理 ═══

    private static readonly Regex ImagePlaceholderRegex = new(@"\[img#(\d+)\]", RegexOptions.Compiled);

    /// <summary>检查文本是否包含图片占位符</summary>
    public static bool HasImagePlaceholders(string text)
    {
        return !string.IsNullOrEmpty(text) && ImagePlaceholderRegex.IsMatch(text);
    }

    /// <summary>提取文本中所有图片 ID</summary>
    public static List<int> ExtractImageIds(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("[img#")) return [];

        var ids = new List<int>();
        foreach (Match match in ImagePlaceholderRegex.Matches(text))
        {
            if (int.TryParse(match.Groups[1].Value, out var id))
                ids.Add(id);
        }
        return ids;
    }

    /// <summary>获取图片元数据</summary>
    public static PhotoMap? GetPhotoById(int id)
    {
        using var db = new VettingDbContext();
        return db.PhotoMaps.FindById(id);
    }

    /// <summary>获取图片流</summary>
    public static Stream? GetPhotoStream(string fileId)
    {
        using var db = new VettingDbContext();
        return db.GetPhotoStream(fileId);
    }

    /// <summary>移除文本中的图片占位符（用于文本填充时）</summary>
    public static string RemoveImagePlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("[img#")) return text;
        return ImagePlaceholderRegex.Replace(text, "");
    }
}
