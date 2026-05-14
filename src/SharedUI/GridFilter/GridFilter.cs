using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.Windows;
using System.Windows.Data;

namespace FMO.Shared;

public interface IGridFilterItem
{
    bool IsSelected { get; set; }

    string Title { get; set; }
}

public partial class GridFilterItem : ObservableObject, IGridFilterItem
{
    public required string Title { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }


    public Func<object, bool> FilterFunc { get; set; } = x => true;

}



public partial class GridFilter : ObservableObject
{
    private readonly Throttle _throttle = new(TimeSpan.FromMilliseconds(50));
    private List<GridFilterItem> _selectedFiltersCache = new();
    private bool _isBulkUpdating;

    public GridFilter(params CollectionViewSource[] sources)
    {
        SourceList = sources.Select(x => new WeakReference<CollectionViewSource>(x)).ToList();

        foreach (var source in sources)
            source.Filter += (s, e) => e.Accepted = e.Accepted && Filter(e.Item);

        FilterSource.Filter += (s, e) => e.Accepted = string.IsNullOrWhiteSpace(SearchKey)
            || e.Item is GridFilterItem f && f.Title.Contains(SearchKey);
    }

    [ObservableProperty]
    public partial IEnumerable<GridFilterItem>? Filters { get; set; }

    [ObservableProperty]
    public partial string? SearchKey { get; set; }

    public CollectionViewSource FilterSource { get; } = new();

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public List<WeakReference<CollectionViewSource>> SourceList { get; }

    /// <summary>
    /// 核心优化：使用缓存列表替代 LINQ，避免每行数据都执行 Any + 属性访问
    /// </summary>
    public bool Filter(object obj)
    {
        if (!IsActive) return true;

        // OR 逻辑：只要匹配任意一个已选筛选器即通过
        foreach (var f in _selectedFiltersCache)
        {
            if (f.FilterFunc(obj)) return true;
        }
        return false;
    }

    private void UpdateFilters()
    {
        if (_isBulkUpdating) return;

        // 1. 缓存已选中的筛选器（仅计算一次）
        _selectedFiltersCache = Filters?.Where(x => x.IsSelected).ToList() ?? new();
        IsActive = _selectedFiltersCache.Count > 0;

        // 2. 必须在 UI 线程同步刷新，避免 BeginInvoke 导致刷新乱序/重复
        if (Application.Current.Dispatcher.CheckAccess())
            RefreshViews();
        else
            Application.Current.Dispatcher.Invoke(RefreshViews);
    }

    private void RefreshViews()
    {
        foreach (var weakSource in SourceList)
        {
            if (weakSource.TryGetTarget(out var cvs) && cvs.View != null)
            {
                cvs.View.Refresh();
            }
        }
    }

    partial void OnFiltersChanged(IEnumerable<GridFilterItem>? value)
    {
        Application.Current.Dispatcher.BeginInvoke(() => FilterSource.Source = value);
        if (value is null) return;

        foreach (var item in value)
        {
            item.PropertyChanged += Item_PropertyChanged;
        }
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isBulkUpdating && e.PropertyName == nameof(GridFilterItem.IsSelected))
            _throttle.Execute(UpdateFilters);
    }

    partial void OnSearchKeyChanged(string? value) => FilterSource.View?.Refresh();

    [RelayCommand]
    public void Clear()
    {
        if (Filters is null) return;

        // 标记批量更新，拦截节流器重复排队
        _isBulkUpdating = true;
        foreach (var item in Filters)
        {
            if (item.IsSelected)
                item.IsSelected = false;
        }
        _isBulkUpdating = false;

        // 批量修改完成后，统一同步刷新一次
        UpdateFilters();
    }
}