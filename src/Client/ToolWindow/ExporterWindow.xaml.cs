using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ExcelDataReader;
using FMO.Logging;
using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using Microsoft.Win32;
using Serilog;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// ExporterWindow.xaml 的交互逻辑
/// </summary>
public partial class ExporterWindow : Window
{
    public ExporterWindow()
    {
        InitializeComponent();
    }
}

public partial class ExporterWindowViewModel : ObservableObject
{
    public ExporterWindowViewModel(IEnumerable<TemplateInfo> templates, object? parameter = null)
    {
        Templates = [.. templates];
        if (Templates.Length == 1)
            SelectedTemplate = Templates[0];

        Parameter = parameter;
    }


    public ExporterWindowViewModel(ExportTypeFlag flag)
    {
        using var db = DbHelper.Base();
        // 排除不存在的模板
        Templates = db.GetCollection<TemplateInfo>().FindAll().Where(x => (x.Suit & flag) != 0).Where(x => Directory.Exists(@$"files\tpl\{x.Id}")).ToArray();//Find(x => ((int)x.Suit & (int)flag) != 0).ToArray();

        if (Templates.Length == 0)
            return;


        if (Templates.Length == 1)
            SelectedTemplate = Templates[0];

        ChooseMode = true;
    }


    public TemplateInfo[] Templates { get; set; }

    public bool ChooseMode { get; }

    [ObservableProperty]
    public partial bool ChooseFund { get; set; }


    [ObservableProperty]
    public partial SelectionMode ChooseFundMode { get; set; }

    [ObservableProperty]
    public partial FundSelect[]? Funds { get; set; }


    [ObservableProperty]
    public partial TemplateInfo? SelectedTemplate { get; set; }


    [ObservableProperty]
    public partial IEnumerable<string>? TplFiles { get; set; }

    [ObservableProperty]
    public partial string? SelectedFileName { get; set; }


    [ObservableProperty]
    public partial DataTable? Sample { get; set; }

    public bool IsMultiTpl => Templates.Length > 1;

    [ObservableProperty]
    public partial bool ShowCustomFileName { get; set; }

    public object? Parameter { get; }

    [RelayCommand]
    public void Modify()
    {
        if (SelectedTemplate is null) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(@$"files\tpl\{SelectedTemplate.Id}", $"{SelectedFileName}.xlsx"),
                UseShellExecute = true
            });
        }
        catch { }
    }


    [RelayCommand]
    public void Export(Window window)
    {
        if (SelectedTemplate is null || SelectedFileName is null)
            return;

        var filePath = Path.Combine(@$"files\tpl\{SelectedTemplate.Id}", $"{SelectedFileName}.xlsx");
        if (!File.Exists(filePath))
            return;

        var param = Parameter;
        if (ChooseMode && param is null)
        {
            param = BuildParameter();
        }

        AssemblyLoadContext context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
        try
        {
            string assemblyPath = Path.GetFullPath(Path.Combine(@$"files\tpl\{SelectedTemplate.Id}", SelectedTemplate.Entry));
            using var fs = new FileStream(assemblyPath, FileMode.Open);
            var assembly = context.LoadFromStream(fs);
            var type = assembly.GetType(SelectedTemplate.Type);
            if (type is null)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "导出失败，模板异常。"));
                return;
            }

            var obj = Activator.CreateInstance(type) as IExporter;

            var data = obj?.Generate(param);
            if (data?.Data is null)
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "导出失败，未能成功生成数据。"));
                return;
            }

            var fd = new SaveFileDialog
            {
                FileName = data.FileName,
                Filter = data.Filter,
                Title = $"导出 {SelectedTemplate.Name} 数据"
            };
            if (fd.ShowDialog() == true)
                try { Tpl.Generate(fd.FileName!, filePath, data.Data); } catch { }
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "导出失败，请查看Log"));
        }
        finally
        {
            context.Unload();
        }
        window.Close();
    }



    [RelayCommand]
    public void SelectAllFunds() => Funds?.ToList().ForEach(x => x.Select(true));



    [RelayCommand]
    public void UnselectAllFunds() => Funds?.ToList().ForEach(x => x.Select(false));


    [RelayCommand]
    public void ReverseSelectFunds() => Funds?.ToList().ForEach(x => x.Select(!x.IsSelected));






    private object? BuildParameter()
    {
        Dictionary<string, object?> dic = new();
        if (ChooseFund)
        {
            int[]? value = Funds?.Where(x => x.IsSelected)?.Select(x => x.Id)?.ToArray();
            if (value is not null)
                dic.Add(nameof(Fund), value);
        }


        return dic.Count == 1 ? dic.First().Value : dic;
    }

    partial void OnSelectedTemplateChanged(TemplateInfo? value)
    {
        if (value is null)
        {
            TplFiles = null;
            return;
        }

        try
        {
            TplFiles = new DirectoryInfo(@$"files\tpl\{value.Id}").GetFiles("*.xlsx").Select(x => x.Name[0..^5]);
            SelectedFileName = TplFiles.FirstOrDefault();

            var meta = value.Meta?.FirstOrDefault(x => x.Type == nameof(Fund));
            ChooseFund = meta is not null;

            if (ChooseFund && Funds is null)
            {
                using var db = DbHelper.Base();
                Funds = db.GetCollection<FundSelect>(nameof(Fund)).FindAll().ToArray();
                ChooseFundMode = meta!.Multiple ? SelectionMode.Multiple : SelectionMode.Single;
            }
        }
        catch (DirectoryNotFoundException e)
        {
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "模板丢失，请重新导入模板"));
        }
        catch (Exception e)
        {
            Log.Error(e, $"打开模板失败，{value.Name} {value.Id}");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "打开模板失败，请查看log"));
        }
    }

    partial void OnSelectedFileNameChanged(string? value)
    {
        if (value is null || SelectedTemplate is null)
        {
            Sample = null;
            return;
        }

        using var fs = new FileStream(Path.Combine($@"files\tpl\{SelectedTemplate.Id}", $"{value}.xlsx"), FileMode.Open);
        using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(fs);
        Sample = reader.AsDataSet().Tables[0];
    }






    public class FundSelect : Fund, INotifyPropertyChanged
    {
        public bool IsSelected { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Select(bool sel = true)
        {
            IsSelected = sel;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

}