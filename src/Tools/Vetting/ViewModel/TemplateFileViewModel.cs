using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniSoftware;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Vetting.Data;
using Vetting.Models.Entities;
using Vetting.Services;

namespace Vetting.ViewModel;

public partial class TemplateFileViewModel : ObservableObject
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    public ObservableCollection<string> Output { get; } = [];

    [SetsRequiredMembers]
    public TemplateFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;
    }

    [RelayCommand]
    private void OpenFile() => Process.Start(new ProcessStartInfo(AbsolutePath) { UseShellExecute = true });

    [RelayCommand]
    private void ViewCustomQuestions()
    {
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if(!m.Success)
        {
            HandyControl.Controls.Growl.Warning("文件名不合法");
            return;
        }

        var safeName = m.Groups[1].Value;
        var providerId = m.Groups[2].Value;
        var ext = m.Groups[3].Value;
        var srcPath = Path.Combine("files", "vetting", VettingId, $"{safeName}{ext}");

        if (!File.Exists(srcPath))
        {
            HandyControl.Controls.Growl.Warning("找不到原始尽调文件");
            return;
        }
        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();

        var vm = new CustomQuestionsViewModel(fileHash, providerId, FileName);
        if (vm.Questions.Count == 0)
        {
            HandyControl.Controls.Growl.Warning("没有找到自定义问题");
            return;
        }

        var win = new Vetting.View.CustomQuestionsWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        win.Show();
    }

    [RelayCommand]
    private async Task AIAnswerCustomQuestionsAsync()
    {
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if (!m.Success) { HandyControl.Controls.Growl.Warning("文件名不合法"); return; }
        var providerId = m.Groups[2].Value;
        var srcPath = Path.Combine("files", "vetting", VettingId, $"{m.Groups[1].Value}{m.Groups[3].Value}");
        if (!File.Exists(srcPath)) { HandyControl.Controls.Growl.Warning("找不到原始尽调文件"); return; }
        var fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();

        var sel = MainWindowViewModel.GlobalProviders.Where(p => p.IsSelected).ToArray();
        if (sel.Length == 0) { HandyControl.Controls.Growl.Warning("请先选择 AI 接口"); return; }

        Output.Clear();
        IsExpanded = true;
        var tasks = sel.Select(p => CustomQuestionAnswerService.AnswerAsync(
            fileHash, providerId,
            CustomQuestionAnswerService.CreateProvider(p), p.Name,
            output: line => Output.Add(line)));
        var counts = await Task.WhenAll(tasks);
        Output.Add($"AI 回答完成，共 {counts.Sum()} 条");
    }

    [RelayCommand]
    private async Task FillTemplateAsync()
    {
        Output.Clear();
        IsExpanded = true;

        var tplPath = AbsolutePath;
        if (!File.Exists(tplPath)) { Output.Add("模板文件不存在"); return; }

        // 解析文件名获取 fileHash + providerId
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if (!m.Success) { Output.Add("文件名格式无法解析"); return; }
        var srcPath = Path.Combine("files", "vetting", VettingId, $"{m.Groups[1].Value}{m.Groups[3].Value}");
        var fileHash = File.Exists(srcPath)
            ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant()
            : "";
        var providerId = m.Groups[2].Value;

        var obj = await Task.Run(() => BuildFillObject(fileHash, providerId));

        var safeName = Path.GetFileNameWithoutExtension(FileName);
        var outDir = Path.Combine("files", "vetting", VettingId, "final");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, $"{safeName}_filled.docx");

        try
        {
            MiniWord.SaveAsByTemplate(outPath, tplPath, obj);
            Output.Add($"已生成: {outPath}");
        }
        catch (Exception ex)
        {
            Output.Add($"填充失败: {ex.Message}");
        }
    }

    private static Dictionary<string, object> BuildFillObject(string fileHash, string providerId)
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

        // 散装问题答案
        Dictionary<string, object> scatter = new();
        if (!string.IsNullOrEmpty(fileHash))
        {
            var questions = db.FileSpecialQuestions.Find(q => q.FileHash == fileHash && q.Provider == providerId).ToArray();
            var answers = db.SpecialAnswers.Query().ToEnumerable().Where(a => questions.Any(q => q.Id == a.QuestionId)).ToArray();
            foreach (var q in questions)
            {
                var best = answers.Where(a => a.QuestionId == q.Id)
                    .OrderByDescending(a => a.Identifier == "manual" ? 1 : 0)
                    .FirstOrDefault();
                scatter[$"a{q.Index}"] = (object)(best?.Value ?? "");
            }
        }

        // 人员按角色分组
        Staff[] Filter(StaffRole role) => allStaff.Where(s => s.Role.HasFlag(role)).ToArray();

        var obj = new Dictionary<string, object>();
        // 唯一项：扁平化为 manager_Name 格式
        FlattenInto(obj, "manager", manager);
        FlattenInto(obj, "credit", credit);
        FlattenInto(obj, "invest", invest);
        FlattenInto(obj, "risk", risk);
        // 列表项：保持数组（MiniWord 支持 list.property）
        obj["shareholder"] = allShareholders.Select(ToDict).ToArray();
        obj["actualcontroller"] = allShareholders.Where(s => s.IsActualController).Select(s => ToDict(new { s.Name, Penetration = s.Ratio, Intro = s.Intro })).ToArray();
        obj["department"] = allDepts.Select(d => {
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

        foreach (var kv in scatter) obj[kv.Key] = kv.Value;
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
