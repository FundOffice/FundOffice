using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vetting.ViewModel;

namespace Vetting.View;
public partial class DataCenterWindow : Window
{
    public DataCenterWindow()
    {
        InitializeComponent();
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem newTab)
        {
            foreach (var dg in FindVisualChildren<DataGrid>(newTab))
            {
                if (dg.CanUserDeleteRows)
                {
                    dg.Loaded -= OnDataGridLoaded;
                    dg.Loaded += OnDataGridLoaded;
                }
            }
        }
    }

    private static void OnDataGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid { ItemsSource: INotifyCollectionChanged ncc })
        {
            ncc.CollectionChanged -= OnCollectionChanged;
            ncc.CollectionChanged += OnCollectionChanged;
        }
    }

    private static void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Remove || e.OldItems is null) return;
        using var db = new Vetting.Copilot.Data.VettingDbContext();
        foreach (var item in e.OldItems)
        {
            var entityProp = item.GetType().GetProperty("Entity");
            var entity = entityProp?.GetValue(item);
            if (entity == null) continue;
            var idProp = entity.GetType().GetProperty("Id");
            if (idProp?.GetValue(entity) is int id && id > 0)
                db.DeleteEntity(entity.GetType(), id);
        }
    }

    // ═══ 推荐产品拖拽 ═══

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ListBox lb) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(lb);
        var hit = lb.InputHitTest(pos) as DependencyObject;
        var lbi = FindAncestor<ListBoxItem>(hit);
        if (lbi?.DataContext is not FundInfoVM fund) return;
        DragDrop.DoDragDrop(lb, new DataObject("FundInfoVM", fund), DragDropEffects.Move);
    }

    private void OnAllFundsDrop(object sender, DragEventArgs e)
    {
        // 从推荐列表拖到所有列表 = 取消推荐
        if (DataContext is not DataCenterViewModel vm) return;
        if (!e.Data.GetDataPresent("FundInfoVM")) return;
        var fund = (FundInfoVM)e.Data.GetData("FundInfoVM")!;
        if (vm.GlobalRecommendedFunds.Contains(fund))
        {
            vm.GlobalRecommendedFunds.Remove(fund);
            vm.SaveGlobalRecommend();
        }
    }

    private void OnRecFundsDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not DataCenterViewModel vm) return;
        if (!e.Data.GetDataPresent("FundInfoVM")) return;
        var fund = (FundInfoVM)e.Data.GetData("FundInfoVM")!;

        var target = (ListBox)sender;
        var dropPos = e.GetPosition(target);
        var hitItem = target.InputHitTest(dropPos) as DependencyObject;
        var hitLbi = FindAncestor<ListBoxItem>(hitItem);
        int targetIndex = hitLbi != null
            ? target.ItemContainerGenerator.IndexFromContainer(hitLbi)
            : vm.GlobalRecommendedFunds.Count;

        if (vm.GlobalRecommendedFunds.Contains(fund))
        {
            // 同列表内拖动 = 调整顺序
            int oldIdx = vm.GlobalRecommendedFunds.IndexOf(fund);
            if (oldIdx == targetIndex) return;
            vm.GlobalRecommendedFunds.Move(oldIdx, targetIndex > oldIdx ? targetIndex - 1 : targetIndex);
        }
        else
        {
            // 从所有列表拖过来 = 添加
            if (targetIndex > vm.GlobalRecommendedFunds.Count) targetIndex = vm.GlobalRecommendedFunds.Count;
            vm.GlobalRecommendedFunds.Insert(targetIndex, fund);
        }
        vm.SaveGlobalRecommend();
    }

    // ═══ 已有附件文件拖拽（从资源管理器拖入复制）═══

    private void OnPredDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPredDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not DataCenterViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;
        vm.ImportPredFiles(files);
        HandyControl.Controls.Growl.Success($"已导入 {files.Length} 个文件");
        e.Handled = true;
    }

    // ═══ 常用文件拖拽（扫描件/用印件自动改名）═══

    private void OnCommonDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCommonScanDrop(object sender, DragEventArgs e) => OnCommonDrop(sender, e, "scan");

    private void OnCommonStampDrop(object sender, DragEventArgs e) => OnCommonDrop(sender, e, "stamp");

    private void OnCommonDrop(object sender, DragEventArgs e, string zone)
    {
        if (DataContext is not DataCenterViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;
        if (sender is FrameworkElement { Tag: CommonFileVM cf })
        {
            vm.ImportCommonFile(cf, zone, files[0]);
            HandyControl.Controls.Growl.Success($"已保存 {cf.Name}{(zone == "stamp" ? "_用印" : "")}");
        }
        e.Handled = true;
    }

    // ═══ 图片拖拽导入 ═══

    private void OnPhotoDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPhotoDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not DataCenterViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;

        vm.ImportPhotoFiles(files);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var c in FindVisualChildren<T>(child)) yield return c;
        }
    }
}
