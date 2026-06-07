using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using MoT;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Utilities;

namespace FMO;

/// <summary>
/// StatementPage.xaml 的交互逻辑
/// </summary>
public partial class StatementPage : UserControl
{
    public StatementPage()
    {
        InitializeComponent();
    }
}


public partial class StatementPageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<FileTemplateViewModel> ExcelTemplates { get; set; } = [];

    public CollectionViewSource ExcelTemplateSource { get; } = new();

    public StatementPageViewModel()
    {

        ExcelTemplateSource.GroupDescriptions.Add(new PropertyGroupDescription("Class"));

    }

    [RelayCommand]
    public void GenerateReport()
    {
        var context = new ExporterWindowViewModel(ExportTypeFlag.MultiFundSummary);
        if (context.Templates.Length == 0)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "没有可用的模板或者模板已被删除"));
            return;
        }

        var wnd = new ExporterWindow
        {
            DataContext = context,
            Owner = App.Current.MainWindow
        };

        wnd.ShowDialog();
    }

    [RelayCommand]
    public void GenerateElementSheet()
    {
        var context = new ExporterWindowViewModel(ExportTypeFlag.MultiFundElementSheet);
        if (context.Templates.Length == 0)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(ToastLevel.Warning, "没有可用的模板或者模板已被删除"));
            return;
        }



        var wnd = new ExporterWindow
        {
            DataContext = context,
            Owner = App.Current.MainWindow
        };

        wnd.ShowDialog();
    }



    [RelayCommand]
    public void LoadTemplates()
    {
        try
        {
            using var db = DbHelper.Template();
            var metas = db.GetCollection<TemplateMeta>().FindAll().ToArray();

            ExcelTemplates = [.. metas.Select(x => new FileTemplateViewModel(x))];

            App.Current.Dispatcher.InvokeAsync(() => ExcelTemplateSource.Source = ExcelTemplates);
        }
        catch (Exception e)
        {
            Logg.Error(e);
            Toast.Error("加载Excel模板出错");
        }
    }

    [RelayCommand]
    public async Task ImportTpl(DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] path)
            return;

        _ = Task.Run(async () =>
            {
                foreach (var p in path)
                {
                    try
                    {
                        await ExcelTemplate.Import(p);
                    }
                    catch (Exception er)
                    {
                        Logg.Error(er);
                        Toast.Warning($"导入失败: {Path.GetFileNameWithoutExtension(p)}");
                    }
                }

                LoadTemplates();
            });
    }


}



public partial class FileTemplateViewModel(TemplateMeta meta) : ObservableObject
{

    public string Name => Meta.Name;

    public string Description => Meta.Description;

    public string? Class => Meta.Class;

    public TemplateMeta Meta { get; set; } = meta;

    [RelayCommand]
    public void OpenTemplate()
    {
        try
        {
            var t = ExcelTemplate.Load(Meta);

            SheetExportWindow wnd = new SheetExportWindow()
            {
                Owner = App.Current.MainWindow,
                DataContext = new SheetExportWindowViewModel(t)
            };
            wnd.ShowDialog();
        }
        catch (Exception e)
        {
            HandyControl.Controls.Growl.Error($"无法打开模板，{e.Message}");
        }

    }
}