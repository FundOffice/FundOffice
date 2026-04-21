using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using LiteDB;
using Microsoft.Win32;
using Serilog;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;

namespace FMO;



public class TempFile : IDisposable
{
    public string FilePath { get; }

    public TempFile() => FilePath = Path.GetTempFileName();


    public void Dispose()
    {
        try { File.Delete(FilePath); } catch (Exception e) { Log.Error(e, $"删除临时文件【{FilePath}】失败"); }
    }
}



/// <summary>
/// 基类
/// </summary>
public partial class FlowViewModel : ObservableObject//, IFileSetter
{

    public int FundId { get; set; }

    public int FlowId { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// 流程日期
    /// </summary>
    [ObservableProperty]
    public partial DateTime? Date { get; set; }


    /// <summary>
    /// 自定义文件
    /// </summary>
    //[ObservableProperty]
    //public partial ObservableCollection<SimpleFileBase>? CustomFiles { get; set; }

    //public MultiDualFileViewModel CustomFiles { get;  }


    public string CustomFileFolder { get; set; } = "Files";

    /// <summary>
    /// 只读模式
    /// </summary>
    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;

    [ObservableProperty]
    public partial bool IsSettingDate { get; set; }

    /// <summary>
    /// 初始化
    /// </summary>
    protected bool Initialized { get; set; }


    [SetsRequiredMembers]
    public FlowViewModel(FundFlow flow)
    {
        FlowId = flow.Id;

        FundId = flow.FundId;

        Name = flow.Name;

        Date = flow.Date?.ToDateTime(default) ?? null;

        //CustomFiles = new(flow.CustomFiles);
        //CustomFiles.FileChanged += f => SaveFileChanged(new { CustomFiles = f });

        //CustomFiles = flow.CustomFiles is not null ? new(flow.CustomFiles.Select(x => new SimpleFileBase { Label = x.Title, File = x.Path is null ? null : new FileInfo(x.Path) })) : new();
        //foreach (var item in CustomFiles)
        //    item.PropertyChanged += CustomFile_PropertyChanged;

        //CustomFiles.CollectionChanged += CustomFiles_CollectionChanged;

        IsReadOnly = flow.Finished;

    }


    partial void OnIsReadOnlyChanged(bool oldValue, bool newValue)
    {
        using var db = DbHelper.Base();
        var flow = db.GetCollection<FundFlow>().FindById(FlowId);
        flow.Finished = newValue;
        db.GetCollection<FundFlow>().Update(flow);
    }

    partial void OnDateChanged(DateTime? oldValue, DateTime? newValue)
    {
        if (newValue is null || !Initialized) return;

        using var db = DbHelper.Base();
        var flow = db.GetCollection<FundFlow>().FindById(FlowId);
        flow!.Date = DateOnly.FromDateTime(newValue.Value);
        db.GetCollection<FundFlow>().Update(flow);
    }


    [RelayCommand]
    public void AddCustomFile()
    {
        // 如果存在未设置的，则不增加
        //var last = CustomFiles.Files.LastOrDefault();
        //if (last is not null && (string.IsNullOrWhiteSpace(last.Label) || last.Label == "未命名") && last.File is null)
        //    return;

        //SimpleFileBase item = new();
        //item.PropertyChanged += CustomFile_PropertyChanged;
        //CustomFiles.Add(item);
    }

    //[RelayCommand]
    //public void DeleteFile(SimpleFileBase file)
    //{
    //    CustomFiles?.Remove(file);
    //}

    protected void SaveFileChanged<T>(T value)
    {
        using var db = DbHelper.Base();
        db.GetCollection<FundFlow>().UpdateMany(BsonMapper.Global.ToDocument(value).ToString(), $"_id={FlowId}");
    }





    //[RelayCommand]
    //public void ChooseFile(SimpleFile file)
    //{
    //    var fd = new OpenFileDialog();
    //    fd.Filter = file.Filter;
    //    if (fd.ShowDialog() != true)
    //        return;

    //    SetFile(file, fd.FileName);
    //}


    //public void SetFile(ISimpleFile? file, string path)
    //{
    //    if (file is SimpleFile ff)
    //    {
    //        FileStorageInfo? tar = ff.Build(path);
    //        if (tar?.Path is not null)
    //            ff.File = new FileInfo(tar.Path);

    //        using var db = DbHelper.Base();
    //        var flow = db.GetCollection<FundFlow>().FindById(FlowId);
    //        if (flow is not null)
    //        {
    //            ff.SetProperty(flow, tar);
    //            db.GetCollection<FundFlow>().Update(flow);
    //        }
    //    }
    //}




    //[RelayCommand]
    //public void Clear(SimpleFile file)
    //{
    //    if (file is null) return;

    //    var r = HandyControl.Controls.MessageBox.Show("是否删除文件", "提示", MessageBoxButton.YesNoCancel);
    //    if (r == MessageBoxResult.Cancel) return;

    //    if (r == MessageBoxResult.Yes)
    //    {
    //        try
    //        {
    //            file.File?.Delete();
    //        }
    //        catch (Exception e)
    //        {
    //            HandyControl.Controls.Growl.Warning("文件已打开，无法删除，请先关闭文件");
    //            return;
    //        }
    //    }


    //    using var db = DbHelper.Base();
    //    var flow = db.GetCollection<FundFlow>().FindById(FlowId);

    //    if (flow is not null)
    //    {
    //        file.SetProperty(flow, null);
    //        db.GetCollection<FundFlow>().Update(flow);
    //        file.File = null;
    //    }
    //}

    [RelayCommand]
    public void UpdateFileDate()
    {
        UpdateFileDateOverride();
    }

    protected virtual void UpdateFileDateOverride()
    {
    }
}



