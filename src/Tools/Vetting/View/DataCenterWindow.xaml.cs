using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
        using var db = new Vetting.Data.VettingDbContext();
        foreach (var item in e.OldItems)
        {
            // item is VM, get Entity from it
            var entityProp = item.GetType().GetProperty("Entity");
            var entity = entityProp?.GetValue(item);
            if (entity == null) continue;
            var idProp = entity.GetType().GetProperty("Id");
            if (idProp?.GetValue(entity) is int id && id > 0)
                db.DeleteEntity(entity.GetType(), id);
        }
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
