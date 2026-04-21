using System.Windows;
using System.Windows.Controls;

namespace FMO.Shared;


public class FileView : HeaderedContentControl
{
    public bool IsReadOnly
    {
        get { return (bool)GetValue(IsReadOnlyProperty); }
        set { SetValue(IsReadOnlyProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsReadOnly.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(FileView), new PropertyMetadata(false));

    protected override void OnDrop(DragEventArgs e)
    {
        if (IsReadOnly) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is FileMetaViewModel vm)
                vm.SetFile(files[0]);
        }
    }
}



