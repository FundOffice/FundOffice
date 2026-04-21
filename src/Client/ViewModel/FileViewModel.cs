using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Utilities;
using Microsoft.Win32;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;

namespace FMO;




//public partial class FileViewModel : ObservableObject
//{
//    public required string Id { get; set; }

//    /// <summary>
//    /// 标识
//    /// </summary>
//    [ObservableProperty]
//    public partial string? Label { get; set; }

//    [ObservableProperty]
//    public partial FileInfo? File { get; set; }


//    /// <summary>
//    /// 文件类型筛选
//    /// </summary>
//    public string? Filter { get; set; }

//    /// <summary>
//    /// 文件变更后，更新到实体
//    /// </summary>
//    public Action<FileViewModel>? SaveFunc { get; set; }

//    /// <summary>
//    /// 保存到本地
//    /// </summary>
//    public Func<FileViewModel, string?>? StoreFunc { get; set; }

//    /// <summary>
//    /// 清除文件
//    /// </summary>
//    public Action<FileViewModel>? ClearFunc { get; set; }

//    [RelayCommand]
//    public void View()
//    {
//        if (File?.Exists ?? false)
//            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(File.FullName) { UseShellExecute = true }); } catch { }
//    }

//    [RelayCommand]
//    public void Change()
//    {
//        var fd = new OpenFileDialog();
//        fd.Filter = Filter;
//        if (fd.ShowDialog() != true)
//            return;


//        var fi = new FileInfo(fd.FileName);
//        if (fi is not null)
//            SetFile(fi);
//    }

//    protected virtual void OnChanged(FileInfo fi)
//    {

//    }
//    protected virtual void OnClear()
//    {
//    }

//    [RelayCommand]
//    public void Clear()
//    {
//        if (ClearFunc is not null)
//            ClearFunc(this);

//        File = null;
//        OnClear();
//    }

//    [RelayCommand]
//    public void Print()
//    {
//        if (File is null || !File.Exists) return;


//        PrintDialog printDialog = new PrintDialog();
//        if (printDialog.ShowDialog() == true)
//        {
//            // 获取默认打印机名称
//            string printerName = printDialog.PrintQueue.Name;

//            // 使用系统默认的PDF阅读器打印PDF文档
//            System.Diagnostics.Process process = new System.Diagnostics.Process();
//            process.StartInfo.FileName = File.FullName;
//            process.StartInfo.Verb = "print";
//            process.Start();

//            // 等待打印任务完成
//            process.WaitForExit();
//        }
//    }


//    [RelayCommand]
//    public void SaveAs()
//    {
//        if (File is null || !File.Exists) return;

//        try
//        {
//            var d = new SaveFileDialog();
//            d.FileName = File.Name;
//            if (d.ShowDialog() == true)
//                System.IO.File.Copy(File.FullName, d.FileName);
//        }
//        catch (Exception ex)
//        {
//            Log.Error($"文件另存为失败: {ex.Message}");
//        }
//    }


//    public void OnDrop(string s)
//    {
//        var fi = new FileInfo(s);
//        if (!fi.Exists) return;

//        SetFile(fi);
//    }

//    private void SetFile(FileInfo fi)
//    {
//        File = fi;

//        if (fi is not null)
//        {
//            if (fi.Exists)
//            {
//                if (StoreFunc is not null && StoreFunc(this) is string s)
//                    File = new FileInfo(s);

//                if (SaveFunc is not null)
//                    SaveFunc(this);
//            }
//            OnChanged(fi);
//        }
//    }
//}



public partial class MultipleFileViewModel<TEntity> : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<FileInfo> Files { get; private set; } = new();


    public Func<TEntity>? GetEntity { get; set; }

    public Func<TEntity, List<FileStorageInfo>>? GetProperty { get; set; }



    public void Init(TEntity entity)
    {
        if (GetProperty is null) return;

        var prop = GetProperty(entity);


        if (Files is not null)
            Files.CollectionChanged -= Files_CollectionChanged;

        Files = new(prop.Select(x => new FileInfo(x.Path!)));

    }

    private void Files_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (GetEntity is null) return;
        if(GetProperty is null) return;

        TEntity entity = GetEntity();

        var p = GetProperty(entity);

        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                foreach (FileInfo fi in e.NewItems!)
                {
                    if (fi is null || !fi.Exists) continue;

                    string hash = FileHelper.ComputeHash(fi)!;
                    p.Add(new FileStorageInfo(fi.FullName, hash, fi.LastWriteTime));
                } 
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                break;
            default:
                break;
        }
         


    }
}