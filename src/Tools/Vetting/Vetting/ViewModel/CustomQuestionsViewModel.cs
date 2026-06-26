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

    public CustomQuestionsViewModel() { WindowTitle = "自定义问题"; }

    /// <summary>
    /// 文件级构造：合并所有 provider 的问题，按 question 文本去重显示
    /// </summary>
    public CustomQuestionsViewModel(string fileHash, string fileName)
    {
        _fileHash = fileHash;
        WindowTitle = $"自定义问题 — {fileName}";
        LoadMerged();
    }

    private void LoadMerged()
    {
        using var db = new VettingDbContext();
        var allQs = db.FileSpecialQuestions.Find(x => x.FileHash == _fileHash).ToArray();
        // 按 Question 文本去重，每组取 Index 最小的为主，收集所有 questionId
        var grouped = allQs
            .Where(q => !string.IsNullOrWhiteSpace(q.Question))
            .GroupBy(q => q.Question!)
            .OrderBy(g => g.Min(x => x.Index));

        Questions.Clear();
        foreach (var g in grouped)
        {
            var primary = g.OrderBy(x => x.Index).First();
            var allIds = g.Select(x => x.Id).ToArray();
            var item = new QuestionItem(primary, allIds);

            var answers = db.SpecialAnswers.Query().ToEnumerable().Where(a => allIds.Contains(a.QuestionId)).ToArray();
            item.ManualAnswer = answers.FirstOrDefault(a => a.Identifier == "manual")?.Value;
            foreach (var a in answers.Where(a => a.Identifier != "manual"))
                item.Answers.Add(new AnswerItem { Identifier = a.Identifier, Value = a.Value });
            Questions.Add(item);
        }
    }
 

    public partial class QuestionItem : ObservableObject
    {
        public int Id { get; }
        public int[] AllIds { get; }
        public int Index { get; }
        public string Placeholder => $"{{{{a{Index}}}}}";
        public string Question { get; }
        public ObservableCollection<AnswerItem> Answers { get; } = [];

        [ObservableProperty]
        public partial string? ManualAnswer { get; set; }

        public QuestionItem(FileSpecialQuestion primary, int[] allIds)
        {
            Id = primary.Id;
            AllIds = allIds;
            Index = primary.Index;
            Question = primary.Question ?? "";
        }

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
