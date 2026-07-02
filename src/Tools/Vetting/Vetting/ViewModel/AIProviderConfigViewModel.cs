using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Vetting.Copilot.Data;
using Vetting.Copilot.Models;
using Vetting.Entity;

namespace Vetting.ViewModel;

public partial class AIProviderConfigViewModel : ObservableObject
{
    public ObservableCollection<AIProviderItemViewModel> Providers { get; } = [];
    [ObservableProperty] public partial AIProviderItemViewModel? SelectedProvider { get; set; }

    public AIProviderConfigViewModel()
    {
        using var db = new VettingDbContext();
        foreach (var config in db.AIProviderConfigs.FindAll())
            Providers.Add(new AIProviderItemViewModel(config));
        SelectedProvider = Providers.FirstOrDefault();
    }

    [RelayCommand]
    private void DeleteProvider(AIProviderItemViewModel vm)
    {
        if (HandyControl.Controls.MessageBox.Show($"确认删除 \"{vm.Name}\"？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        using var db = new VettingDbContext();
        db.AIProviderConfigs.Delete(vm.Id);
        Providers.Remove(vm);
        if (SelectedProvider == vm) SelectedProvider = Providers.FirstOrDefault();

        WeakReferenceMessenger.Default.Send(new AIProviderChanged(vm.Id, ChangedType.Delete));
    }

    [RelayCommand]
    private void AddProvider()
    {
        AIProviderItemViewModel vm = new();
        Providers.Add(vm);
        SelectedProvider = vm;
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [RelayCommand]
    private void ExportConfig()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON|*.json",
            FileName = "AI配置.json"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            using var db = new VettingDbContext();
            var configs = db.AIProviderConfigs.FindAll().ToList();
            var json = JsonSerializer.Serialize(configs, _jsonOpts);
            File.WriteAllText(dialog.FileName, json);
            HandyControl.Controls.Growl.Success("导出成功");
        }
        catch (Exception ex)
        {
            HandyControl.Controls.Growl.Error($"导出失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void ImportConfig()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        if (HandyControl.Controls.MessageBox.Show("导入将覆盖现有配置，是否继续？", "确认导入",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var configs = JsonSerializer.Deserialize<List<AIProviderConfig>>(json, _jsonOpts);
            if (configs == null || configs.Count == 0)
            {
                HandyControl.Controls.Growl.Warning("配置文件为空");
                return;
            }

            using var db = new VettingDbContext();
            db.AIProviderConfigs.DeleteAll();

            Providers.Clear();
            foreach (var config in configs)
            {
                var newConfig = config with { Id = 0 };
                db.AIProviderConfigs.Upsert(newConfig);
                Providers.Add(new AIProviderItemViewModel(newConfig));
            }

            SelectedProvider = Providers.FirstOrDefault();
            WeakReferenceMessenger.Default.Send(new AIProviderChanged(0, ChangedType.Update));
            HandyControl.Controls.Growl.Success("导入成功");
        }
        catch (Exception ex)
        {
            HandyControl.Controls.Growl.Error($"导入失败：{ex.Message}");
        }
    }
}
