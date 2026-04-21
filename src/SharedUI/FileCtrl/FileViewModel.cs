using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace FMO.Shared;






public class FileExistsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool show = false;
        switch (value)
        {
            case string s:
                show = File.Exists(s);
                break;
            case FileInfo fi:
                show = fi.Exists;
                break;
            //case FileStorageInfo fsi:
            //    show = fsi.Exists;
            //    break;
            case FileMeta f:
                show = f.Exists;
                break;

            case SimpleFile f:
                show = f.File?.Exists ?? false;
                break;
            default:
                show = false;
                break;
        }

        if (parameter switch { bool r => r, string s => s == "true", _ => false })
            return !show ? Visibility.Visible : Visibility.Collapsed;

        return show ? Visibility.Visible : Visibility.Collapsed;


    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}



public partial class ReadOnlyFileMetaViewModel : ObservableObject
{

    /// <summary>
    /// "Word Documents|*.doc|Office Files|*.doc;*.xls;*.ppt"
    /// </summary>

    public Guid Guid { get; } = Guid.NewGuid();

    public string? Id => Meta?.Id;

    public string? Name => Meta?.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Exists))]
    [NotifyPropertyChangedFor(nameof(Deleted))]
    [NotifyPropertyChangedFor(nameof(CanSet))]
    [NotifyPropertyChangedFor(nameof(Id))]
    [NotifyPropertyChangedFor(nameof(Name))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial FileMeta? Meta { get; set; }

    public string? DisplayName => GetShort(Name);

    public bool Exists => Meta?.Exists ?? false;

    public bool CanSet => Meta is null || !Meta.Exists;

    public bool Deleted => Meta is not null && !string.IsNullOrWhiteSpace(Meta.Id) && !Meta.Exists;


    private string? GetShort(string? name, int cnt = 20)
    {
        int a = cnt / 3, b = cnt * 2 / 3;
        return name switch { string s => s.Length > a + b ? s[..a] + " ...... " + s[^b..] : s, _ => name };
    }




    [RelayCommand]
    public void View()
    {
        if (string.IsNullOrWhiteSpace(Id)) return;

        try
        {
            Directory.CreateDirectory(@$"temp\{Id}");
            string tmp = @$"temp\{Id}\{Name}";

            if (!File.Exists(tmp))
                FileMeta.CreateHardLink(@$"files\hardlink\{Id}", tmp);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tmp) { UseShellExecute = true });
        }
        catch (Exception e) { LogEx.Error(e); WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "无法打开文件")); }
    }


    [RelayCommand]
    public void Copy()
    {
        if (!Exists) return;

        try
        {
            Directory.CreateDirectory(@$"temp\{Id}");
            string tmp = @$"temp\{Id}\{Name}";
            if (!File.Exists(tmp))
                FileMeta.CreateHardLink(@$"files\hardlink\{Id}", tmp);

            tmp = Path.GetFullPath(tmp);
            var obj = new DataObject(DataFormats.FileDrop, new string[] { tmp });
            obj.SetText(tmp);
            Clipboard.SetDataObject(obj);
        }
        catch (Exception e) { LogEx.Error(e); WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "无法复制文件")); }
    }


    [RelayCommand]
    public void SaveAs()
    {
        if (!Exists) return;

        try
        {
            var d = new SaveFileDialog();
            d.FileName = Name!;
            d.DefaultExt = Path.GetExtension(Name!);
            d.AddExtension = true;
            if (d.ShowDialog() == true)
                File.Copy(@$"files\hardlink\{Id}", d.FileName);
        }
        catch (Exception e) { LogEx.Error(e); WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "文件另存为失败")); }
    }

    
}



public partial class FileMetaViewModel : ReadOnlyFileMetaViewModel
{
    public FileMetaViewModel(FileMeta? meta) => Meta = meta;

    public FileMetaViewModel() { }

    public string? Filter { get; set; }


    /// <summary>
    /// Extension -> SpecificFileName
    /// </summary>
    public Func<string?, string>? SpecificFileName { get; set; }


    public string? SaveFolder { get; set; }

    public delegate void MetaChangedHandler(FileMetaViewModel sender);
    public event MetaChangedHandler? MetaChanged;



    [RelayCommand(CanExecute = nameof(CanSet))]
    public void Choose()
    {
        var fd = new OpenFileDialog();
        fd.Filter = Filter;
        if (fd.ShowDialog() != true) return;

        var newf = new FileInfo(fd.FileName);
        var desire = SpecificFileName is null ? newf.Name : SpecificFileName(newf.Extension);

        Meta = FileMeta.Create(newf, desire);
        OnMetaChanged();
    }


    internal void SetFile(string path)
    {
        var newf = new FileInfo(path);
        var desire = SpecificFileName is null ? newf.Name : SpecificFileName(newf.Extension);

        Meta = FileMeta.Create(newf, desire);
        OnMetaChanged();
    }

    [RelayCommand]
    public void Delete()
    {
        if (HandyControl.Controls.MessageBox.Show($"是否删除 {Name}", "提示", MessageBoxButton.YesNo) == MessageBoxResult.No)
            return;

        if (!string.IsNullOrWhiteSpace(Id))
            File.Delete($@"files\hardlink\{Id}");
        Meta = null;
        OnMetaChanged();
    }

    [RelayCommand]
    public void Rename()
    {
        if (Meta is null) return;

        var wnd = new RenameWindow();
        RenameWindowViewModel vm = new();
        wnd.DataContext = vm;

        var ext = Path.GetExtension(Name);
        vm.NewName = SpecificFileName is null ? Name : SpecificFileName(ext);

        wnd.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        wnd.Owner = Application.Current.MainWindow;
        if (true == wnd.ShowDialog())
        {
            var name = vm.NewName;
            if (!Path.HasExtension(name))
                name += ext;

            Meta = Meta with { Name = name };
            OnMetaChanged();
        }
    }

    protected virtual void OnMetaChanged()
    {
        ChooseCommand.NotifyCanExecuteChanged();
        MetaChanged?.Invoke(this);
    }
}


public partial class SimpleFileViewModel : FileMetaViewModel
{
    public SimpleFileViewModel(SimpleFile? file = null)
    {
        Meta = file?.File;
        Label = file?.Label;
    }

    public string? Label { get; set; }


    public delegate void FileChangedHandler(SimpleFile? File);
    public event FileChangedHandler? FileChanged;


    protected override void OnMetaChanged()
    {
        base.OnMetaChanged();
        FileChanged?.Invoke(new SimpleFile { Label = Label, File = Meta });
    }

}


public class DualFileMetaViewModel
{
    public Guid Guid { get; } = Guid.NewGuid();

    public FileMetaViewModel Normal { get; } = new();

    public FileMetaViewModel Another { get; } = new();

    public string? Filter { get; set; }


    /// <summary>
    /// Extension -> SpecificFileName
    /// </summary>
    public Func<string?, string>? SpecificFileName { get; set; }


    public string? SaveFolder { get; set; }


    public delegate void MetaChangedHandler(DualFileMetaViewModel sender);
    public event MetaChangedHandler? MetaChanged;

    public DualFileMetaViewModel(DualFileMeta? sealedFile)
    {
        Normal.Meta = sealedFile?.Normal;
        Another.Meta = sealedFile?.Another;

        Normal.MetaChanged += OnMetaChanged;
        Another.MetaChanged += OnMetaChanged;
    }

    public DualFileMetaViewModel()
    {
        Normal.MetaChanged += OnMetaChanged;
        Another.MetaChanged += OnMetaChanged;
    }

    protected virtual void OnMetaChanged(FileMetaViewModel sender) => MetaChanged?.Invoke(this);

}

public partial class DualFileViewModel : DualFileMetaViewModel
{
    public DualFileViewModel()
    {
    }

    public DualFileViewModel(DualFile? sealedFile)
    {
        Label = sealedFile?.Label;
        Normal.Meta = sealedFile?.File;
        Another.Meta = sealedFile?.Another;
    }

    public string? Label { get; set; }

    public delegate void FileChangedHandler(DualFile? File);
    public event FileChangedHandler? FileChanged;


    protected override void OnMetaChanged(FileMetaViewModel sender)
    {
        base.OnMetaChanged(sender);
        FileChanged?.Invoke(new DualFile
        {
            Label = Label,
            File = Normal.Meta,
            Another = Another.Meta
        });
    }



}



public partial class MultiFileViewModel : ObservableObject
{
    public MultiFileViewModel()
    {
    }

    public MultiFileViewModel(MultiFile? multiSealed)
    {
        Label = multiSealed?.Label;
        if (multiSealed?.Files is not null)
            Files = [.. multiSealed.Files.Where(x => x is not null).Select(x => new FileMetaViewModel { Meta = x })];
        else Files = [];

        foreach (var file in Files)
            file.MetaChanged += ItemChanged; ;
    }

    public Guid Guid { get; } = Guid.NewGuid();

    public string? Label { get; set; }


    public ObservableCollection<FileMetaViewModel> Files { get; } = new();

    public string? Filter { get; set; }


    //public bool CanAddFile => Files.Any(x => !x.Normal.CanSet && !x.Sealed.CanSet);

    /// <summary>
    /// Extension -> SpecificFileName
    /// </summary>
    public Func<string?, string>? SpecificFileName { get; set; }


    public string? SaveFolder { get; set; }

    public delegate void OnFileChangedHandler(MultiFile files);
    public event OnFileChangedHandler? FileChanged;

    private void ItemChanged(FileMetaViewModel sender)
    {
        if (sender.Meta is null)
            Files.Remove(sender);
        InvokeFileChanged();
    }

    [RelayCommand]
    public void AddFile()
    {
        var fd = new OpenFileDialog();
        fd.Filter = Filter;
        if (fd.ShowDialog() != true) return;

        var newf = new FileInfo(fd.FileName);
        var desire = SpecificFileName is null ? newf.Name : SpecificFileName(newf.Extension);

        var m = FileMeta.Create(newf, desire);
        FileMetaViewModel newv = new() { Meta = m };
        Files.Add(newv);
        newv.MetaChanged += ItemChanged;
        InvokeFileChanged();
    }


    public void AddFile(string file)
    {
        var newf = new FileInfo(file);
        var desire = SpecificFileName is null ? newf.Name : SpecificFileName(newf.Extension);

        var m = FileMeta.Create(newf, desire);
        FileMetaViewModel newv = new() { Meta = m };
        Files.Add(newv);
        newv.MetaChanged += ItemChanged;
        InvokeFileChanged();
    }


    protected virtual void InvokeFileChanged()
    {

        var files = new MultiFile
        {
            Label = Label,
            Files = Files.Where(x => x.Meta is not null).Select(x => x.Meta!).ToList()
        };
        FileChanged?.Invoke(files);
    }


}


public partial class MultiDualFileViewModel : ObservableObject
{
    public Guid Guid { get; } = Guid.NewGuid();

    public string? Label { get; set; }

    public string? Filter { get; set; }


    //public bool CanAddFile => Files.Any(x => !x.Normal.CanSet && !x.Sealed.CanSet);

    /// <summary>
    /// Extension -> SpecificFileName
    /// </summary>
    public Func<string?, string>? SpecificFileName { get; set; }


    public string? SaveFolder { get; set; }


    public ObservableCollection<DualFileMetaViewModel> Files { get; }


    public delegate void OnFileChangedHandler(MultiDualFile files);
    public event OnFileChangedHandler? FileChanged;



    public MultiDualFileViewModel(MultiDualFile? multiSealed = null)
    {
        Label = multiSealed?.Label;

        if (multiSealed?.Files is not null)
            Files = [.. multiSealed.Files.Where(x => x.Normal is not null || x.Another is not null).Select(x => new DualFileMetaViewModel(x))];
        else Files = [];

        foreach (var file in Files)
            file.MetaChanged += ItemChanged; ;

    }

    private void ItemChanged(DualFileMetaViewModel sender)
    {
        if (sender.Normal.Meta is null && sender.Another.Meta is null)
            Files.Remove(sender);
        InvokeFileChanged();
    }

    [RelayCommand]
    public void AddFile()
    {
        var fd = new OpenFileDialog();
        fd.Filter = Filter;
        if (fd.ShowDialog() != true) return;

        var newf = new FileInfo(fd.FileName);
        var desire = SpecificFileName is null ? newf.Name : SpecificFileName(newf.Extension);

        var m = FileMeta.Create(newf, desire);
        DualFileMetaViewModel newv = new(new DualFileMeta { Normal = m });
        Files.Add(newv);
        newv.MetaChanged += ItemChanged;
        InvokeFileChanged();
    }


    protected virtual void InvokeFileChanged()
    {

        var files = new MultiDualFile
        {
            Label = Label,
            Files = Files.Select(x => new DualFileMeta { Normal = x.Normal.Meta, Another = x.Another.Meta }).ToList()
        };
        FileChanged?.Invoke(files);
    }
}

