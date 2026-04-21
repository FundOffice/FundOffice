using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExcelDataReader;
using FMO.Logging;
using FMO.TPL;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;

namespace FMO.TemplateManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}


public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
    {
        using var db = DbHelper.Base();
        Templates = [.. db.GetCollection<TemplateInfo>().FindAll()];
    }

    [ObservableProperty]
    public partial ObservableCollection<TemplateInfo> Templates { get; set; }

    [ObservableProperty]
    public partial TemplateInfo? SelectedTemplate { get; set; }


    [ObservableProperty]
    public partial IEnumerable<string>? TplFiles { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefault))]
    public partial string? SelectedFileName { get; set; }


    public bool IsDefault => SelectedFileName == "default";


    [ObservableProperty]
    public partial DataTable? Sample { get; set; }



    [ObservableProperty]
    public partial bool ShowCustomFileName { get; set; }


    [ObservableProperty]
    public partial string CustomFileName { get; set; }


    [RelayCommand]
    public void AddTemplate(DragEventArgs args)
    {
        if (args.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;


        foreach (var f in paths)
        {
            if (!f.ToLower().EndsWith(".zip"))
                continue;

            ParseTpl(f);
        }
    }


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
    public void Customize()
    {
        try
        {
            string target = Path.Combine(@$"files\tpl\{SelectedTemplate.Id}", $"{CustomFileName}.xlsx");
            File.Copy(Path.Combine(@$"files\tpl\{SelectedTemplate.Id}", $"{SelectedFileName}.xlsx"), target);
            TplFiles = TplFiles is null ? [target] : [.. TplFiles, target];
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch { }
    }


    partial void OnSelectedTemplateChanged(TemplateInfo? value)
    {
        if (value is null)
        {
            TplFiles = null;
            return;
        }

        TplFiles = new DirectoryInfo(@$"files\tpl\{value.Id}\").GetFiles("*.xlsx").Select(x => x.Name[0..^5]);

        OnSelectedFileNameChanged(TplFiles?.FirstOrDefault());
    }

    partial void OnSelectedFileNameChanged(string? value)
    {
        if (value is null || SelectedTemplate is null)
        {
            Sample = null;
            return;
        }

        try
        {
            using var fs = new FileStream(Path.Combine(@$"files\tpl\{SelectedTemplate.Id}", $"{value}.xlsx"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(fs);
            Sample = reader.AsDataSet().Tables[0];
        }
        catch { }
    }







    private void ParseTpl(string f)
    {
        try
        {
            using var fs = new FileStream(f, FileMode.Open);
            using ZipArchive archive = new ZipArchive(fs);

            var entry = archive.GetEntry("def.json");
            if (entry is null)
                return;

            using var es = entry.Open();
            var entity = JsonSerializer.Deserialize<TemplateInfo>(es);

            if (entity is null)
            {
                LogEx.Error($"模板 {f}，def 无法解析");
                return;
            }


            var gid = entity!.Id;
            var di = Directory.CreateDirectory(@$"files\tpl\{gid}");
            archive.ExtractToDirectory(di.FullName, true);

            using var db = DbHelper.Base();
            db.GetCollection<TemplateInfo>().Upsert(entity);

            foreach (var item in Templates.Where(x => x.Id == entity.Id).ToArray())
                Templates.Remove(item);

            Templates.Add(entity);

            HandyControl.Controls.Growl.Info($"模板 {entity.Name}    更新完成");
        }
        catch (Exception e)
        {
            LogEx.Error($"模板导入出错 {f}， {e.StackTrace}");
            HandyControl.Controls.Growl.Info($"模板导入出错");
        }
    }

    //private void ParseTpl(string f)
    //{
    //    using var fs = new FileStream(f, FileMode.Open);
    //    using ZipArchive archive = new ZipArchive(fs);

    //    var entry = archive.GetEntry("tpl.dll");
    //    if (entry is null)
    //        return;

    //    // 解析
    //    AssemblyLoadContext context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);

    //    using var ms = new MemoryStream();
    //    using var es = entry.Open();
    //    es.CopyTo(ms);
    //    ms.Seek(0, SeekOrigin.Begin);

    //    var assembly = context.LoadFromStream(ms);
    //    var gen = assembly.DefinedTypes.FirstOrDefault(x => x.IsAssignableTo(typeof(IExporter)));
    //    if (gen is null) return;

    //    var obj = Activator.CreateInstance(gen) as IExporter;

    //    var gid = obj!.Id;
    //    var di = Directory.CreateDirectory(@$"files\tpl\{gid}");
    //    archive.ExtractToDirectory(di.FullName, true);

    //    using var db = DbHelper.Base();
    //    TemplateInfo entity = new(gid, obj.Name, obj.Description, gen.FullName!, obj.Suit, obj.Meta, Path.GetRelativePath(Directory.GetCurrentDirectory(), di.FullName));
    //    db.GetCollection<TemplateInfo>().Upsert(entity);

    //    foreach (var item in Templates.Where(x => x.Id == entity.Id).ToArray())
    //        Templates.Remove(item);

    //    Templates.Add(entity);
    //    context.Unload();

    //    HandyControl.Controls.Growl.Info($"{entity.Name}    更新完成");
    //}
}
