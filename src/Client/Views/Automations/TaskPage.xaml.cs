using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Schedule;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

 

    private void ListBox_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var listBox = (ListBox)sender;
        if (listBox.SelectedItem == null) return;

        // 沿可视化树向上查找，判断点击是否落在 ListBoxItem 上
        var source = e.OriginalSource as DependencyObject;
        bool isOnItem = false;
        while (source != null)
        {
            if (source is ListBoxItem)
            {
                isOnItem = true;
                break;
            }
            source = VisualTreeHelper.GetParent(source);
        }

        // 点击空白区域（背景/间隙/滚动条外）则取消选中
        if (!isOnItem)
        {
            listBox.SelectedItem = null;
        }
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

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Task.Run(async () =>
        {
            await Task.Delay(1);
            foreach (var m in ms)
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    sw.Restart();
                    var vm = MissionManager.GetViewModel(m);
                    sw.Stop(); System.Diagnostics.Debug.WriteLine($"{m.GetType()}, {sw.ElapsedMilliseconds}");
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

