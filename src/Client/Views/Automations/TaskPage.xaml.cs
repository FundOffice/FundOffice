using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Schedule;
using FMO.Utilities;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Documents;

namespace FMO;

/// <summary>
/// TaskPage.xaml 的交互逻辑
/// </summary>
public partial class TaskPage : UserControl
{
    public TaskPage()
    {
        InitializeComponent();
    }
}


public class MissionViewAndViewModel
{
    public required object View { get; set; }

    public required object ViewModel { get; set; }
}

public partial class TaskPageViewModel : ObservableObject, IRecipient<RemoveMissionMessage>
{

    // public ObservableCollection<AutomationViewModelBase> Tasks { get; } = new();
    public ObservableCollection<AutomationViewModelBase> Tasks { get; } = new();

    public MissionTemplate[]? Templates { get; set; }


    public TaskPageViewModel()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
        var ms = MissionSchedule.Missions;

        foreach (var m in ms)
        {
            var vm = MissionTemplateManager.MakeViewModel(m);
            if (vm is null)
            {
                // 后台任务
                if (m is FillFundDailyMission) continue;

                Log.Error($"无法加载任务{m.Id}，Type={m.GetType()}，找不到view model");
                //WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "无法加载任务，请查看log"));
                vm = new AutomationViewModelBase(m);
            }


            Tasks.Add(vm);
        }

        InitTaskTpl();
    }


    public void InitTaskTpl()
    {
        Templates = MissionTemplateManager.Templates.Select(x => x.Value).ToArray();

        //var mvm = AssemblyLoadContext.Default.Assemblies.SelectMany(x => x.DefinedTypes).Where(x => x.BaseType is not null && x.BaseType.IsGenericType && x.BaseType.GetGenericTypeDefinition() == typeof(MissionViewModel<>));

        //Templates = mvm.Select(x => (type: x, attr: x.GetCustomAttribute<MissionTitleAttribute>())).Where(x => x.attr is not null).Select(x => new TaskTemplate { Title = x.attr!.Title, ViewModel = x.type }).ToArray();


    }




    //[RelayCommand]
    //public void AddMailCache()
    //{
    //    Tasks.Add(new MailCacheViewModel(new()));
    //}

    [RelayCommand]
    public void AddTask(MissionTemplate template)
    { 
        var m = template.CreateMission();
        using var db = new MissionDatabase();
        db.GetCollection<Mission>().Insert(m);

        if (m is DailyFromMailMission || m is TAFromMailMission)
            if (!Tasks.Any(x => x.MissionType == typeof(MailCacheMission)))
                AddTask(MissionTemplateManager.CacheMailMissionTemplate);


        var vm = template.CreateViewModel(m);
        if (vm is null)
        {
            Log.Error($"无法加载任务{m.Id}，Type={m.GetType()}，找不到view model");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "无法加载任务，请查看log"));
            return;
        }

        Tasks.Add(vm);
        //if (template.ViewModel.BaseType?.GetGenericArguments() is Type[] types)
        //  try { Tasks.Add((Activator.CreateInstance(template.ViewModel, Activator.CreateInstance(types[0])) as AutomationViewModelBase)!); } catch { }
    }


    public void Receive(RemoveMissionMessage message)
    {
        Tasks.Remove(message.ViewModel);
        MissionSchedule.Unregister(message.ViewModel.Id);
    }



}

