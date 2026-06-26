using MiniSoftware;
using System.Text.RegularExpressions;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;

namespace Vetting.Copilot;

/// <summary>
/// 模板填充结果
/// </summary>
public record TemplateFillResult
{
    public bool Success { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Logs { get; init; } = [];
}

/// <summary>
/// 模板填充器 — 从数据库加载实体数据，填充 MiniWord 模板生成最终文件
/// </summary>
public class TemplateFiller
{
    /// <summary>
    /// 填充模板生成最终文件
    /// </summary>
    public TemplateFillResult Fill(
        string templatePath,
        string outputPath,
        string fileHash,
        string providerId,
        int[]? recommendIds = null,
        Action<string>? progress = null)
    {
        var logs = new List<string>();
        try
        {
            if (!File.Exists(templatePath))
                return new TemplateFillResult { Success = false, ErrorMessage = "模板文件不存在", Logs = logs };

            recommendIds ??= LoadGlobalRecommendIds();
            var obj = BuildFillObject(fileHash, providerId, recommendIds);

            var tempPath = Path.Combine(Path.GetTempPath(), $"vetting_{Guid.NewGuid():N}.docx");
            try
            {
                var outDir = Path.GetDirectoryName(outputPath)!;
                Directory.CreateDirectory(outDir);

                FileRetry.Run(() => MiniWord.SaveAsByTemplate(tempPath, templatePath, obj), "生成临时模板", onRetry: m => { logs.Add(m); progress?.Invoke(m); });
                FileRetry.Run(() => MiniWord.SaveAsByTemplate(outputPath, tempPath, obj), "生成最终文件", onRetry: m => { logs.Add(m); progress?.Invoke(m); });

                var logMsg = $"已生成: {outputPath}";
                logs.Add(logMsg);
                progress?.Invoke(logMsg);

                return new TemplateFillResult { Success = true, OutputPath = outputPath, Logs = logs };
            }
            finally { try { File.Delete(tempPath); } catch { } }
        }
        catch (Exception ex)
        {
            logs.Add($"填充失败: {ex.Message}");
            return new TemplateFillResult { Success = false, ErrorMessage = ex.Message, Logs = logs };
        }
    }

    /// <summary>
    /// 构建 MiniWord 填充对象（不写文件，可用于测试/预览）
    /// </summary>
    public Dictionary<string, object> BuildFillObjectForTest(
        string fileHash, string providerId, int[]? recommendIds = null)
    {
        recommendIds ??= LoadGlobalRecommendIds();
        return BuildFillObject(fileHash, providerId, recommendIds);
    }

    private static int[] LoadGlobalRecommendIds()
    {
        using var db = new VettingDbContext();
        var rec = db.TemplateRecommends.FindOne(r => r.FileHash == "__global__");
        if (rec?.FundIds == null) return [];
        return rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }

    internal static Dictionary<string, object> BuildFillObject(string fileHash, string providerId, int[] recommendIds)
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

        Staff[] Filter(StaffRole role) => allStaff.Where(s => s.Role.HasFlag(role)).ToArray();

        var obj = new Dictionary<string, object>();
        FlattenInto(obj, "manager", manager);
        FlattenInto(obj, "credit", credit);
        FlattenInto(obj, "invest", invest);
        FlattenInto(obj, "risk", risk);
        obj["recommendCount"] = recommendIds.Length;
        for (int i = 0; i < recommendIds.Length; i++)
        {
            var fund = allFunds.FirstOrDefault(f => f.Id == recommendIds[i]);
            if (fund != null) FlattenInto(obj, $"recommend{i + 1}", fund);
        }
        obj["shareholder"] = allShareholders.Select(ToDict).ToArray();
        obj["actualcontroller"] = allShareholders.Where(s => s.IsActualController).Select(s => ToDict(new { s.Name, Penetration = s.Ratio, Intro = s.Intro })).ToArray();
        obj["department"] = allDepts.Select(d =>
        {
            var dict = ToDict(d);
            dict["StaffCount"] = allStaff.Count(s => s.DepartmentId == d.Id).ToString();
            return dict;
        }).ToArray();
        obj["strategy"] = allStrategies.Select(ToDict).ToArray();
        obj["product"] = allFunds.Select(ToDict).ToArray();
        obj["award"] = allAwards.Select(ToDict).ToArray();
        obj["executive"] = Filter(StaffRole.高管).Select(ToDict).ToArray();
        obj["researcher"] = Filter(StaffRole.投研).Select(ToDict).ToArray();
        obj["riskctrl"] = Filter(StaffRole.风控).Select(ToDict).ToArray();
        obj["pm"] = Filter(StaffRole.投资经理).Select(ToDict).ToArray();
        obj["contact"] = Filter(StaffRole.联系人).Select(ToDict).ToArray();
        obj["compliance"] = Filter(StaffRole.合规).Select(ToDict).ToArray();

        var fsList = db.FinancialStatements.FindAll().OrderByDescending(f => f.Year).ToArray();
        for (int i = 0; i < fsList.Length; i++) FlattenInto(obj, $"financialstatement{i + 1}", fsList[i]);
        obj["financialstatement"] = fsList.Select(ToDict).ToArray();

        var drList = db.DrawdownRecords.FindAll().OrderByDescending(d => d.Date).ToArray();
        for (int i = 0; i < drList.Length; i++) FlattenInto(obj, $"drawdownrecord{i + 1}", drList[i]);
        obj["drawdownrecord"] = drList.Select(ToDict).ToArray();

        var aumList = db.AUMs.FindAll().OrderByDescending(a => a.Year).ToArray();
        for (int i = 0; i < aumList.Length; i++) FlattenInto(obj, $"aum{i + 1}", aumList[i]);
        obj["aum"] = aumList.Select(ToDict).ToArray();

        if (!string.IsNullOrEmpty(fileHash))
        {
            var questions = db.FileSpecialQuestions.Find(q => q.FileHash == fileHash && q.Provider == providerId).ToArray();
            var answers = db.SpecialAnswers.Query().ToEnumerable().Where(a => questions.Any(q => q.Id == a.QuestionId)).ToArray();
            foreach (var q in questions)
            {
                var best = answers.Where(a => a.QuestionId == q.Id)
                    .OrderByDescending(a => a.Identifier == "manual" ? 1 : 0)
                    .FirstOrDefault();
                obj[$"a{q.Index}"] = (object)(best?.Value ?? "");
            }
        }
        return obj;
    }

    private static Dictionary<string, object> ToDict(object src)
    {
        var dict = new Dictionary<string, object>();
        foreach (var p in src.GetType().GetProperties())
        {
            var val = p.GetValue(src);
            if (val is null) { dict[p.Name] = ""; continue; }
            if (val is DateTime dt) { dict[p.Name] = dt.ToString("yyyy-MM-dd"); continue; }
            if (val is Enum) { dict[p.Name] = val.ToString()!; continue; }
            if (val is int age && p.Name == "Age" && src is Staff) { dict[p.Name] = age.ToString(); continue; }
            dict[p.Name] = val;
        }
        return dict;
    }

    private static void FlattenInto(Dictionary<string, object> target, string prefix, object src)
    {
        foreach (var p in src.GetType().GetProperties())
        {
            var val = p.GetValue(src);
            if (val is null) { target[$"{prefix}_{p.Name}"] = ""; continue; }
            if (val is DateTime dt) { target[$"{prefix}_{p.Name}"] = dt.ToString("yyyy-MM-dd"); continue; }
            if (val is Enum) { target[$"{prefix}_{p.Name}"] = val.ToString()!; continue; }
            target[$"{prefix}_{p.Name}"] = val;
        }
    }
}
