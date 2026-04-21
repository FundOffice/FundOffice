using Microsoft.Win32;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FMO;

/// <summary>
/// 按照步骤 1a 或 1b 操作，然后执行步骤 2 以在 XAML 文件中使用此自定义控件。
///
/// 步骤 1a) 在当前项目中存在的 XAML 文件中使用该自定义控件。
/// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
/// 元素中:
///
///     xmlns:MyNamespace="clr-namespace:FMO"
///
///
/// 步骤 1b) 在其他项目中存在的 XAML 文件中使用该自定义控件。
/// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
/// 元素中:
///
///     xmlns:MyNamespace="clr-namespace:FMO;assembly=FMO"
///
/// 您还需要添加一个从 XAML 文件所在的项目到此项目的项目引用，
/// 并重新生成以避免编译错误:
///
///     在解决方案资源管理器中右击目标项目，然后依次单击
///     “添加引用”->“项目”->[浏览查找并选择此项目]
///
///
/// 步骤 2)
/// 继续操作并在 XAML 文件中使用控件。
///
///     <MyNamespace:MultiFileDisplay/>
///
/// </summary>
public class MultiFileDisplay : Control
{
    public object Header
    {
        get { return (object)GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Header.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register("Header", typeof(object), typeof(MultiFileDisplay), new PropertyMetadata(null));




    public FileInfo? Current
    {
        get { return (FileInfo?)GetValue(CurrentProperty); }
        set { SetValue(CurrentProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Current.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentProperty =
        DependencyProperty.Register("Current", typeof(FileInfo), typeof(MultiFileDisplay), new PropertyMetadata(null));




    public ObservableCollection<FileInfo> Files
    {
        get { return (ObservableCollection<FileInfo>)GetValue(FilesProperty); }
        set { SetValue(FilesProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Files.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty FilesProperty =
        DependencyProperty.Register("Files", typeof(ObservableCollection<FileInfo>), typeof(MultiFileDisplay), new PropertyMetadata(null, OnChanged));





    public bool IsReadOnly
    {
        get { return (bool)GetValue(IsReadOnlyProperty); }
        set { SetValue(IsReadOnlyProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsReadOnly.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(MultiFileDisplay), new PropertyMetadata(false));





    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MultiFileDisplay o && e.NewValue is ObservableCollection<FileInfo> f && f.Count > 0)
            o.Current = f.Last();
    }

    public static RoutedUICommand ViewFileCommand { get; } = new();

    public static RoutedUICommand FileSaveAsCommand { get; } = new();

    public static RoutedUICommand FileDeleteCommand { get; } = new();


    static MultiFileDisplay()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiFileDisplay), new FrameworkPropertyMetadata(typeof(MultiFileDisplay)));
    }

    public MultiFileDisplay()
    {
        CommandBindings.Add(new CommandBinding(ViewFileCommand, OnOpenFile));
        CommandBindings.Add(new CommandBinding(FileSaveAsCommand, OnSaveFile));
        CommandBindings.Add(new CommandBinding(FileDeleteCommand, OnDeleteFile));

        AllowDrop = true; 
    }


    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var btn = Template.FindName("PART_SetFile", this) as Button;
        if (btn is not null)
            btn.Click += Modify_Click;
    }
     

    private void Modify_Click(object sender, RoutedEventArgs e)
    {
        var fd = new OpenFileDialog();
        if (fd.ShowDialog() == true)
        {
            Current = new FileInfo(fd.FileName);
            if (Files is not null)
                Files.Add(Current);
        }
    }

    private void OnDeleteFile(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not FileInfo fi || !fi.Exists)
            return;

        Files.Remove(fi);
        if (Current == fi)
            Current = Files.LastOrDefault();
    }

    private void OnSaveFile(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is not FileInfo fi || !fi.Exists)
            return;

        try
        {
            var d = new SaveFileDialog();
            d.FileName = fi.Name;
            if (d.ShowDialog() == true)
                File.Copy(fi.FullName, d.FileName);
        }
        catch (Exception ex)
        {
            Log.Error($"文件另存为失败: {ex.Message}");
        }
    }

    private void OnOpenFile(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is FileInfo fi)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fi.FullName) { UseShellExecute = true }); } catch { }
    }


    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);

        if(e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (var item in paths)
                Files.Add(new FileInfo(item));
        }
    }
}
