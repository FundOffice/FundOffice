using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FMO.Disclosure;

/// <summary>
/// ConfigureDisclosureWorkflowWindow.xaml 的交互逻辑
/// </summary>
public partial class ConfigureDisclosureWorkflowWindow : Window
{
    public ConfigureDisclosureWorkflowWindow()
    {
        InitializeComponent();
    }

    private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ConfigureDisclosureWorkflowWindowViewModel vm)
        {
            // 1. 拿到模板
            var template = (DataTemplate)FindResource("FlowItemTemplate");

            // 循环生成所有列
            for (int i = 0; i < vm.Types.Length; i++)
            {

                // 2. 动态给 Border 设置 DataContext = Workflows[i]
                var dt = new DataTemplate();

                // 用 XamlWriter/XamlReader 复制模板内容太麻烦，换个思路：
                // 直接让 ContentControl 用资源里的模板
                dt.VisualTree = new FrameworkElementFactory(typeof(ContentControl));
                dt.VisualTree.SetBinding(ContentControl.ContentProperty, new Binding($"Workflows[{i}]"));
                dt.VisualTree.SetResourceReference(ContentControl.ContentTemplateProperty, "FlowItemTemplate");


                // 4. 创建列
                var col = new DataGridTemplateColumn
                {
                    Header = EnumDescriptionTypeConverter.GetEnumDescription(vm.Types[i]),
                    Width = 120,
                    CellTemplate = dt
                };

                grid.Columns.Add(col);
            }
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is null)
            DataContext = new ConfigureDisclosureWorkflowWindowViewModel();
    }
}

public partial class ConfigureDisclosureWorkflowWindowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial IDisclosureChannel[] Channels { get; set; }

    [ObservableProperty]
    public partial ChannelConfigViewModel[] ChannelConfigs { get; set; }

    [ObservableProperty]
    public partial DisclosureType[] Types { get; set; }

    public ObservableCollection<WorkflowRow> Workflows { get; } = [];


    public ConfigureDisclosureWorkflowWindowViewModel()
    {
        Channels = DisclosureChannelGalley.GetRegisteredChannels().ToArray();

        // 检查是否有对应的配置界面
        using var db = DbHelper.Base();
        var configs = db.GetCollection<DisclosureChannelConfig>().FindAll().ToArray().ToDictionary(x => x.ChannelCode);

        List<ChannelConfigViewModel> channelConfigs = new();
        foreach (var c in Channels)
        {
            if (configs.TryGetValue(c.Code, out var config))
                channelConfigs.Add(ChannelConfigFactory.CreateConfig(config)!);
            else channelConfigs.Add(ChannelConfigFactory.CreateConfig(c.Code)!);
        }

        ChannelConfigs = channelConfigs.ToArray();
        var cm = channelConfigs.ToDictionary(x => x.ChannelCode);



        Types = Enum.GetValues<DisclosureType>().Except([DisclosureType.Temporary, DisclosureType.ManagerLevel]).ToArray();

        var dd = db.GetCollection<DisclosureWorkflow>().FindAll().ToArray();
        var data = dd.ToDictionary(x => x.Id);


        foreach (var c in Channels)
        {
            var list = new List<DisclosureWorkflow?>();
            foreach (var t in Types)
            {
                if (c.IsSupported(t) == false)
                {
                    list.Add(null);
                    continue;
                }
                var key = c.Code + t;
                if (data.ContainsKey(key))
                {
                    list.Add(data[key]);
                }
                else
                {
                    list.Add(new DisclosureWorkflow { Channel = c.Code, Type = t });
                }
            }

            Workflows.Add(new WorkflowRow { Head = c, Config = cm[c.Code], Workflows = list.Select(x => new DisclosureWorkflowViewModel(x)).ToArray() });
        }
    }

}


public partial class WorkflowRow : ObservableObject
{

    public required IDisclosureChannel Head { get; set; }


    public required ChannelConfigViewModel Config { get; set; }


    public DisclosureWorkflowViewModel[] Workflows { get; set; } = [];


    [RelayCommand]
    public void SetChannelConfig()
    {
        var win = new ConfigureChannelWindow
        {
            Owner = Application.Current.Windows[^2],
            DataContext = new { Config = Config }
        };
        win.ShowDialog();
    }

}



public partial class DisclosureWorkflowViewModel : ObservableObject
{

    public bool IsSupported { get; }


    public DisclosureWorkflowViewModel(DisclosureWorkflow? workflow)
    {
        IsSupported = workflow is not null;
        if (workflow is not null)
        {
            IsEnabled = workflow.IsEnabled;
            Type = workflow.Type;
            ForAllFunds = workflow.ForAllFunds;
            TargetFunds = workflow.TargetFunds;
            Channel = workflow.Channel;
            Config = workflow.Config;
        }
    }



    public string Id => Channel + Type;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    // 信批类型 
    public DisclosureType Type { get; init; }

    /// <summary>
    /// 管理人维度，如果为true，则适用于管理人层面；
    /// 如果为false，则适用于基金层面，需要指定TargetFunds
    /// </summary>
    public bool IsManagerLevel => Type > DisclosureType.ManagerLevel;

    /// <summary>
    /// 适用全部产品
    /// IsManagerLevel为true时，无效
    /// </summary>
    [ObservableProperty]
    public partial bool ForAllFunds { get; set; }

    /// <summary>
    /// 适用的基金ID列表，仅当ForAllFunds为false时有效
    /// IsManagerLevel为true时，无效
    /// </summary>
    [ObservableProperty]
    public partial int[] TargetFunds { get; set; } = [];


    public string Channel { get; init; } = "";

    [ObservableProperty]
    public partial IWorkConfig? Config { get; set; }


    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // 核心：生成 WorkflowRow 持久化对象
        var obj = new DisclosureWorkflow
        {
            IsEnabled = this.IsEnabled,
            Type = this.Type,
            ForAllFunds = this.ForAllFunds,
            TargetFunds = this.TargetFunds ?? [], // 空值防护
            Channel = this.Channel,
            Config = this.Config,
        };

        // 持久化到数据库
        using var db = DbHelper.Base();
        db.GetCollection<DisclosureWorkflow>().Upsert(obj);
    }
}