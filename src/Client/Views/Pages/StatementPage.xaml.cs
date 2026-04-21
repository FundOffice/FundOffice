using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using System.IO;
using System.Windows.Controls;

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


    [RelayCommand]
    public void GenerateReport()
    {
        var context = new ExporterWindowViewModel(ExportTypeFlag.MultiFundSummary);
        if (context.Templates.Length == 0)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "没有可用的模板或者模板已被删除"));
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
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "没有可用的模板或者模板已被删除"));
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
    public void OpenTemplateManager()
    {
        try
        {
            var di = new DirectoryInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName).Parent!;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.Combine(di.FullName, $"FMO.TemplateManager.exe"),
                WorkingDirectory = Directory.GetCurrentDirectory()
            });
        }
        catch (Exception e)
        {
            HandyControl.Controls.Growl.Warning($"无法启动应用，{e.Message}");
        }
    }

}