using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    // ── 推荐产品拖拽 ──

    private void FundList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ListBox listBox) return;
        var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (item?.DataContext is not FundInfoVM fund) return;
        var data = new DataObject("FundInfoVM", fund);
        DragDrop.DoDragDrop(listBox, data, DragDropEffects.Move | DragDropEffects.Copy);
    }

    private void FundList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("FundInfoVM") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void FundList_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (!e.Data.GetDataPresent("FundInfoVM")) return;
        var fund = (FundInfoVM)e.Data.GetData("FundInfoVM");
        if (listBox.DataContext is not TemplateFileViewModel vm) return;

        var targetIsRecommended = listBox.ItemsSource == vm.RecommendedFunds;

        if (targetIsRecommended)
        {
            // 拖到右边：如果已在列表中则调整顺序，否则添加
            var existingIdx = vm.RecommendedFunds.IndexOf(fund);
            if (existingIdx >= 0)
            {
                // 重排：找到放置位置
                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem?.DataContext is FundInfoVM target && target != fund)
                {
                    var targetIdx = vm.RecommendedFunds.IndexOf(target);
                    vm.RecommendedFunds.Move(existingIdx, targetIdx);
                    vm.SaveRecommend();
                }
            }
            else
            {
                vm.RecommendedFunds.Add(fund);
                vm.SaveRecommend();
            }
        }
        else
        {
            // 拖到左边：从推荐列表移除
            if (vm.RecommendedFunds.Contains(fund))
            {
                vm.RecommendedFunds.Remove(fund);
                vm.SaveRecommend();
            }
        }
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T t) return t;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
