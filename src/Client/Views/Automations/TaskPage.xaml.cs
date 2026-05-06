using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Schedule;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// TaskPage.xaml 的交互逻辑
/// </summary>
public partial class TaskPage : UserControl
{
    public TaskPage()
    {
        InitializeComponent();

        Loaded += (s, e) =>
        {
            if (DataContext is not TaskPageViewModel)
                DataContext = new TaskPageViewModel();
        };
    }
     
}


public class MissionViewAndViewModel
{
    public required object View { get; set; }

    public required object ViewModel { get; set; }
}

public partial class TaskPageViewModel : ObservableObject, IRecipient<RemoveMissionMessage>, IRecipient<Mission>
{

    // public ObservableCollection<AutomationViewModelBase> Tasks { get; } = new();
    public ObservableCollection<MissionViewModel> Tasks { get; } = new();

    public MissionTemplate[]? Templates { get; set; }


    public TaskPageViewModel()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
        var ms = MissionSchedule.Missions;
        Templates = MissionManager.Templates;

        Task.Run(async () =>
        {
            await Task.Delay(1);
            foreach (var m in ms)
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    var vm = MissionManager.GetViewModel(m);
                    Tasks.Add(vm);
                });
            }
        });
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
        MissionSchedule.Register(m);



    }


    public void Receive(RemoveMissionMessage message)
    {
        Tasks.Remove(message.ViewModel);
        MissionSchedule.Unregister(message.ViewModel.Id);
    }

    public void Receive(Mission m)
    {
        var vm = MissionManager.GetViewModel(m);
        Tasks.Add(vm);
    }
}

