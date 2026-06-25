using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
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
}
