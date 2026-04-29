using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace FMO.Disclosure;

/// <summary>
/// ConfigureDisclosureWorkflowWindow.xaml 的交互逻辑
/// </summary>
public partial class ConfigureDisclosureWorkflowWindow : Window
{
    public ConfigureDisclosureWorkflowWindow()
    {
        InitializeComponent();

        GenerateColumns();

        //Task.Run(() =>
        //{
        //    var data = new ConfigureDisclosureWorkflowWindowViewModel();

        //    Dispatcher.InvokeAsync(() => DataContext = data);
        //});
    }

    private void GenerateColumns()
    {
        var template = (DataTemplate)FindResource("FlowItemTemplate");

        // 循环生成所有列
        for (int i = 0; i < DisclosureService.DisclosureTypes.Length; i++)
        {
            var type = DisclosureService.DisclosureTypes[i];
            if (type == DisclosureType.QuarterlyUpdate)
            {
                Style style = new(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));
                grid.Columns.Add(new DataGridTextColumn { Header = "季度更新", ElementStyle = style, Binding = new Binding() { Source = "无需配置" } });
                continue; // 季度更新通道特殊，不展示在这里
            }
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
                Header = EnumDescriptionTypeConverter.GetEnumDescription(type),
                Width = 120,
                CellTemplate = dt
            };

            grid.Columns.Add(col);
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoading))]
    public partial double LoadProgress { get; set; } = 0.1;


    public bool ShowLoading => LoadProgress <= 99;

    public ConfigureDisclosureWorkflowWindowViewModel()
    {
        Channels = DisclosureService.GetRegisteredChannels().ToArray();//.Where(x => x.Code != DisclosureChannelCode.QuarterlyUpdate).ToArray();

        // 检查是否有对应的配置界面
        using var db = DbHelper.Base();
        var configs = db.GetCollection<DisclosureChannelConfig>().FindAll().ToArray().ToDictionary(x => x.ChannelCode);

        List<ChannelConfigViewModel> channelConfigs = new();
        foreach (var c in Channels)
        {
            var vm = DisclosureChannelManager.CreateViewModel(c.Code);
            if (vm is null) continue;

            if (configs.TryGetValue(c.Code, out var config))
                vm.UpdateFrom(config);

            channelConfigs.Add(vm);
        }

        ChannelConfigs = channelConfigs.ToArray();
        var cm = channelConfigs.ToDictionary(x => x.ChannelCode);

        var funds = db.GetCollection<Fund>().Query().Select(x => new { Name = x.Name, Code = x.Code!, Id = x.Id, }).ToArray();


        Types = DisclosureService.DisclosureTypes;//.Except([DisclosureType.Temporary, DisclosureType.ManagerLevel, DisclosureType.QuarterlyUpdate]).ToArray();

        var dd = DisclosureService.GetWorkflows();

        Task.Run(() =>
        {
            foreach (var c in Channels)
            {
                var rowd = from a in Types
                           join b in dd.Where(x => x.Channel == c.Code) on a equals b.Type
                           into instanceGroup
                           from instance in instanceGroup.DefaultIfEmpty()
                           select instance;


                WorkflowRow item = new()
                {
                    Head = c,
                    Config = cm[c.Code],
                    Workflows = rowd.Select(x => new DisclosureWorkflowViewModel(x, funds.Select(x => new DisclosureWorkflowViewModel.FundSelectInfo
                    {
                        Code = x.Code,
                        Name = x.Name,
                        Id = x.Id
                    }).ToArray())).ToArray()
                };
                Application.Current.Dispatcher.InvokeAsync(() => Workflows.Add(item));
                LoadProgress += 100.0 / Channels.Length;
            }
        });
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


    public DisclosureWorkflowViewModel(DisclosureWorkflow? workflow, FundSelectInfo[] funds)
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
            RequireConfigWork = DisclosureService.GetChannel(Channel)?.RequireConfigWork(Type) ?? false;
        }

        Funds = funds;

        foreach (var item in Funds)
        {
            if (TargetFunds.Contains(item.Id))
                item.IsSelected = true;
        }
    }


    public FundSelectInfo[] Funds { get; }


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


    [ObservableProperty]
    public partial bool ShowWorkConfigPop { get; set; }

    public string Channel { get; init; } = "";

    [ObservableProperty]
    public partial IWorkConfig? Config { get; set; }


    public bool RequireConfigWork { get; init; }


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

        DisclosureService.UpdateWorkflow(obj);
    }


    [RelayCommand]
    public void ChooseFund()
    {
        var w = new ChooseFundWindow { Owner = Application.Current.Windows[^2] };
        w.DataContext = new ChooseFundWindowViewModel(Funds);
        if (w.ShowDialog() == true)
        {
            TargetFunds = Funds.Where(x => x.IsSelected).Select(x => x.Id).ToArray();
        }
    }


 

    partial void OnShowWorkConfigPopChanged(bool value)
    {
        if (Config is null)
            Config = DisclosureService.GetChannel(Channel)!.DefaultWorkConfig(Type);

        if(!value)
            OnPropertyChanged(nameof(Config));
    }

    public class FundSelectInfo
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public required string Code { get; set; }

        public bool IsSelected { get; set; }
    }
}