using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniSoftware;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;
using Vetting.Copilot;

namespace Vetting.ViewModel;

public partial class TemplateFileViewModel : ObservableObject
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";
    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool IsRecommendOpen { get; set; }
    public ObservableCollection<string> Output { get; } = [];
    public ObservableCollection<QuestionAnswerTaskViewModel> AIStatuses { get; } = [];

    // 推荐产品
    public ObservableCollection<FundInfoVM> AvailableFunds { get; } = [];
    public ObservableCollection<FundInfoVM> RecommendedFunds { get; } = [];
    [ObservableProperty] public partial FundInfoVM? SelectedAvailable { get; set; }
    [ObservableProperty] public partial FundInfoVM? SelectedRecommended { get; set; }

    private string _fileHash = "";
    private string _providerId = "";

    [SetsRequiredMembers]
    public TemplateFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;

        // 解析文件名获取 fileHash + providerId
        var m = Regex.Match(FileName, @"(.+)_by\[(.+)\](.*)");
        if (m.Success)
        {
            var srcPath = Path.Combine("files", "vetting", VettingId, $"{m.Groups[1].Value}{m.Groups[3].Value}");
            if (File.Exists(srcPath))
                _fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(srcPath))).ToLowerInvariant();
            _providerId = m.Groups[2].Value;
        }

        // 加载所有产品
        using var db = new VettingDbContext();
        foreach (var f in db.FundInfos.FindAll())
            AvailableFunds.Add(new FundInfoVM(f));

        // 加载推荐产品
        var rec = db.TemplateRecommends.FindOne(r => r.FileHash == _fileHash && r.ProviderId == _providerId);
        if (rec?.FundIds != null)
        {
            var ids = rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            foreach (var id in ids)
            {
                var fund = AvailableFunds.FirstOrDefault(f => f.Entity.Id == id);
                if (fund != null) RecommendedFunds.Add(fund);
            }
        }
    }

    public void SaveRecommend()
    {
        using var db = new VettingDbContext();
        var existing = db.TemplateRecommends.FindOne(r => r.FileHash == _fileHash && r.ProviderId == _providerId);
        var ids = string.Join(",", RecommendedFunds.Select(f => f.Entity.Id));
        if (existing != null)
        {
            existing.FundIds = ids;
            db.TemplateRecommends.Update(existing);
        }
        else if (RecommendedFunds.Count > 0)
        {
            db.TemplateRecommends.Insert(new TemplateRecommend { FileHash = _fileHash, ProviderId = _providerId, FundIds = ids });
        }
    }

    [RelayCommand]
    private void OpenFile() => Process.Start(new ProcessStartInfo(AbsolutePath) { UseShellExecute = true });

    [RelayCommand]
    private void AddRecommend()
    {
        if (SelectedAvailable == null || RecommendedFunds.Contains(SelectedAvailable)) return;
        RecommendedFunds.Add(SelectedAvailable);
        SaveRecommend();
    }

    [RelayCommand]
    private void RemoveRecommend()
    {
        if (SelectedRecommended == null) return;
        RecommendedFunds.Remove(SelectedRecommended);
        SaveRecommend();
    }

    [RelayCommand]
    private void MoveUp()
    {
        var idx = SelectedRecommended != null ? RecommendedFunds.IndexOf(SelectedRecommended) : -1;
        if (idx <= 0) return;
        RecommendedFunds.Move(idx, idx - 1);
        SaveRecommend();
    }

    [RelayCommand]
    private void MoveDown()
    {
        var idx = SelectedRecommended != null ? RecommendedFunds.IndexOf(SelectedRecommended) : -1;
        if (idx < 0 || idx >= RecommendedFunds.Count - 1) return;
        RecommendedFunds.Move(idx, idx + 1);
        SaveRecommend();
    }

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

        AIStatuses.Clear();
        var tasks = sel.Select(p =>
        {
            var provider = CustomQuestionAnswerer.CreateProvider(p.Name, p.ProviderType, p.ApiKey, p.BaseUrl, p.Model);
            var vm = new QuestionAnswerTaskViewModel(provider, p.Name, fileHash, providerId);
            AIStatuses.Add(vm);
            return vm.RunAsync(line => Output.Add(line));
        });

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

        var recommendIds = RecommendedFunds.Count > 0
            ? RecommendedFunds.Select(f => f.Entity.Id).ToArray()
            : LoadGlobalRecommendIds();
        var obj = await Task.Run(() => BuildFillObject(fileHash, providerId, recommendIds));

        var safeName = Path.GetFileNameWithoutExtension(FileName);
        var outDir = Path.Combine("files", "vetting", VettingId, "final");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, $"{safeName}_filled.docx");

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"vetting_{Guid.NewGuid():N}.docx");
            try
            {
                FileRetry.Run(() => MiniWord.SaveAsByTemplate(tempPath, tplPath, obj), "生成临时模板", onRetry: m => Output.Add(m));
                FileRetry.Run(() => MiniWord.SaveAsByTemplate(outPath, tempPath, obj), "生成最终文件", onRetry: m => Output.Add(m));
                Output.Add($"已生成: {outPath}");
            }
            finally { try { File.Delete(tempPath); } catch { } }
        }
        catch (Exception ex)
        {
            Output.Add($"填充失败: {ex.Message}");
        }
    }

    private static int[] LoadGlobalRecommendIds()
    {
        using var db = new VettingDbContext();
        var rec = db.TemplateRecommends.FindOne(r => r.FileHash == "__global__");
        if (rec?.FundIds == null) return [];
        return rec.FundIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }

    private static Dictionary<string, object> BuildFillObject(string fileHash, string providerId, int[] recommendIds)
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

        // 年份分组列表（编号 N=1 最近，N=2 去年，N=3 前年）
        var fsList = db.FinancialStatements.FindAll().OrderByDescending(f => f.Year).ToArray();
        for (int i = 0; i < fsList.Length; i++) FlattenInto(obj, $"financialstatement{i + 1}", fsList[i]);
        obj["financialstatement"] = fsList.Select(ToDict).ToArray();

        var drList = db.DrawdownRecords.FindAll().OrderByDescending(d => d.Date).ToArray();
        for (int i = 0; i < drList.Length; i++) FlattenInto(obj, $"drawdownrecord{i + 1}", drList[i]);
        obj["drawdownrecord"] = drList.Select(ToDict).ToArray();

        var aumList = db.AUMs.FindAll().OrderByDescending(a => a.Year).ToArray();
        for (int i = 0; i < aumList.Length; i++) FlattenInto(obj, $"aum{i + 1}", aumList[i]);
        obj["aum"] = aumList.Select(ToDict).ToArray();

        // 散装问题答案
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
