using System.IO;
using System.Windows;
using System.Windows.Controls;
using Vetting.ViewModel;

namespace Vetting.View;
public partial class VettingReportView : UserControl
{
    public VettingReportView() => InitializeComponent();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not VettingReportViewModel vm || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        Directory.CreateDirectory(vm.FolderPath);
        foreach (var file in files)
            if (File.Exists(file))
                File.Copy(file, Path.Combine(vm.FolderPath, Path.GetFileName(file)), overwrite: true);
    }
}
