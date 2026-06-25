using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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

    // TODO: 查看自定义问题
    [RelayCommand]
    private void ViewCustomQuestions()
    {
    }

    // TODO: AI回答自定义问题
    [RelayCommand]
    private async Task AIAnswerCustomQuestionsAsync()
    {
        await Task.CompletedTask;
    }

    // TODO: 填充模板
    [RelayCommand]
    private async Task FillTemplateAsync()
    {
        await Task.CompletedTask;
    }
}
