using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Vetting.Data;
using Vetting.Models.Entities;

namespace Vetting.ViewModel;

public partial class CustomQuestionsViewModel : ObservableObject
{
    public ObservableCollection<QuestionItem> Questions { get; } = [];
    public string WindowTitle { get; }

    public CustomQuestionsViewModel() { WindowTitle = "自定义问题"; }

    public CustomQuestionsViewModel(string fileHash, string providerId, string fileName)
    {
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
