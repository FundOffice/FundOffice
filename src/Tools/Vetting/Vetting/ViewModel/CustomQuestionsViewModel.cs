using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models.Entities;
using Vetting.Copilot.Models.Info;
using Vetting.Copilot;

namespace Vetting.ViewModel;

public partial class CustomQuestionsViewModel : ObservableObject
{
    public ObservableCollection<QuestionItem> Questions { get; } = [];
    public string WindowTitle { get; }

    private readonly string _fileHash = "";
    private readonly string _providerId = "";

    public CustomQuestionsViewModel() { WindowTitle = "自定义问题"; }

    public CustomQuestionsViewModel(string fileHash, string providerId, string fileName)
    {
        _fileHash = fileHash;
        _providerId = providerId;
        WindowTitle = $"自定义问题 — {fileName}";

        using var db = new VettingDbContext();
        var qs = db.FileSpecialQuestions
            .Find(x => x.FileHash == fileHash && x.Provider == providerId)
            .OrderBy(x => x.Index)
            .ToArray();

        foreach (var q in qs)
        {
            var item = new QuestionItem(q);
            var answers = db.SpecialAnswers.Find(a => a.QuestionId == q.Id).ToArray();
            item.ManualAnswer = answers.FirstOrDefault(a => a.Identifier == "manual")?.Value;
            foreach (var a in answers.Where(a => a.Identifier != "manual"))
                item.Answers.Add(new AnswerItem { Identifier = a.Identifier, Value = a.Value });
            Questions.Add(item);
        }
    }

    [RelayCommand]
    private async Task AIAnswerAsync()
    {
        var sel = MainWindowViewModel.GlobalProviders.Where(p => p.IsSelected).ToArray();
        if (sel.Length == 0) { HandyControl.Controls.Growl.Warning("请先选择 AI 接口"); return; }

        var tasks = sel.Select(p =>
        {
            var answerer = new CustomQuestionAnswerer(CustomQuestionAnswerer.CreateProvider(p.Name, p.ProviderType, p.ApiKey, p.BaseUrl, p.Model));
            return answerer.AnswerAndSaveAsync(_fileHash, _providerId, p.Name);
        });
        var results = await Task.WhenAll(tasks);

        // 刷新 Answers
        using var db = new VettingDbContext();
        foreach (var item in Questions)
        {
            item.Answers.Clear();
            var answers = db.SpecialAnswers.Find(a => a.QuestionId == item.Id).ToArray();
            item.ManualAnswer = answers.FirstOrDefault(a => a.Identifier == "manual")?.Value;
            foreach (var a in answers.Where(a => a.Identifier != "manual"))
                item.Answers.Add(new AnswerItem { Identifier = a.Identifier, Value = a.Value });
        }
        HandyControl.Controls.Growl.Success($"AI 回答完成，共 {results.Sum(r => r.AnsweredCount)} 条");
    }

    public partial class QuestionItem(FileSpecialQuestion q) : ObservableObject
    {
        public int Id { get; } = q.Id;
        public int Index { get; } = q.Index;
        public string Placeholder => $"{{{{a{Index}}}}}";
        public string Question { get; } = q.Question ?? "";
        public ObservableCollection<AnswerItem> Answers { get; } = [];

        [ObservableProperty]
        public partial string? ManualAnswer { get; set; }

        partial void OnManualAnswerChanged(string? value)
        {
            using var db = new VettingDbContext();
            var existing = db.SpecialAnswers.FindOne(a => a.QuestionId == Id && a.Identifier == "manual");
            if (existing != null)
            {
                existing.Value = value;
                db.SpecialAnswers.Update(existing);
            }
            else if (!string.IsNullOrEmpty(value))
            {
                db.SpecialAnswers.Insert(new SpecialAnswer { QuestionId = Id, Identifier = "manual", Value = value });
            }
        }
    }

    public class AnswerItem
    {
        public string Identifier { get; init; } = "";
        public string? Value { get; init; }
        public bool IsManual => Identifier == "manual";
    }
}
