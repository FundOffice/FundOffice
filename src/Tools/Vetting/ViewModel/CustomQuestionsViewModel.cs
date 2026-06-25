using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Vetting.Data;
using Vetting.Models.Entities;

namespace Vetting.ViewModel;

public partial class CustomQuestionsViewModel : ObservableObject
{
    public ObservableCollection<QuestionItem> Questions { get; } = [];
    
    public string WindowTitle { get; } 

    private readonly string _fileHash = null!;
    private readonly string _providerId = null!;


    public CustomQuestionsViewModel() { WindowTitle = $"自定义问题"; }


    public CustomQuestionsViewModel(string fileHash, string providerId, string fileName)
    {
        _fileHash = fileHash;
        _providerId = providerId;
        WindowTitle = $"自定义问题 — {fileName}";

        using var db = new VettingDbContext();
        foreach (var q in db.FileSpecialQuestions
            .Find(x => x.FileHash == fileHash && x.Provider == providerId)
            .OrderBy(x => x.Index))
        {
            Questions.Add(new QuestionItem(this, q));
        }
    }

    public void Save(QuestionItem item)
    {
        using var db = new VettingDbContext();
        var entity = db.FileSpecialQuestions
            .FindOne(x => x.FileHash == _fileHash && x.Provider == _providerId && x.Index == item.Index);
        if (entity != null)
        {
            entity.Answer = item.Answer;
            db.FileSpecialQuestions.Update(entity);
        }
    }

    public partial class QuestionItem(CustomQuestionsViewModel owner, FileSpecialQuestion q) : ObservableObject
    {
        public int Index { get; } = q.Index;
        public string Placeholder => $"{{{{a{Index}}}}}";
        public string Question { get; } = q.Question ?? "";

        [ObservableProperty]
        public partial string? Answer { get; set; } = q.Answer;

        partial void OnAnswerChanged(string? value) => owner.Save(this);
    }
}
