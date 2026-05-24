using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using static FMO.ModifyInheritWindowViewModel;

namespace FMO;

internal class DragPayload
{
    public required ShareItem SourceItem { get; set; }

    public int SourceFlowId { get; set; }
}


/// <summary>
/// ModifyInheritWindow.xaml 的交互逻辑
/// </summary>


public partial class ModifyInheritWindow : Window
{
    public ModifyInheritWindow() => InitializeComponent();

    // --- 拖拽状态 ---
    private bool _isDragging;
    private ShareItem? _dragSource;
    private Border? _hoverBorder;
    private ShareItem? _hoverTarget;
    private Path? _tempLine;

    // --- 元素缓存 ---
    private readonly List<Border> _cardBorders = new();
    private readonly Dictionary<ShareItem, Ellipse> _topPoints = new();
    private readonly Dictionary<ShareItem, Ellipse> _bottomPoints = new();

    #region 1. 核心拖拽逻辑 (PreviewMouseMove + 矩形边界检测)

    private void TopPoint_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.DataContext is ShareItem item)
        {
            _isDragging = true;
            _dragSource = item;

            _tempLine = new Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                IsHitTestVisible = false
            };
            LinesCanvas.Children.Add(_tempLine);
            Panel.SetZIndex(_tempLine, 11);

            Mouse.Capture(ellipse); // ⚠️ 捕获后，MouseEnter/Leave 将被系统屏蔽，必须用 PreviewMouseMove 手动检测
            e.Handled = true;
        }
    }

    private void TopPoint_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _dragSource is null) return;

        // 🌟 松开鼠标时才真正修改数据
        if (_hoverTarget != null && IsValidTarget(_dragSource, _hoverTarget))
            _dragSource.Inherit = _hoverTarget.Id;

        // 清理状态
        _isDragging = false;
        _dragSource = null;

        if (_hoverBorder != null) ResetBorderVisuals(_hoverBorder);
        _hoverBorder = null;
        _hoverTarget = null;

        if (_tempLine != null)
        {
            LinesCanvas.Children.Remove(_tempLine);
            _tempLine = null;
        }

        Mouse.Capture(null); // 释放捕获
        e.Handled = true;
    }
    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {

        Debug.WriteLine($"{_isDragging} {Stopwatch.GetTimestamp()}");

        if (!_isDragging || _dragSource == null) return;


        // 1. 获取相对于 HostGrid 的鼠标坐标 (自动包含 ScrollViewer 滚动偏移)
        Point mousePos = e.GetPosition(HostGrid);
        Point startPos = GetEllipseCenter(_topPoints, _dragSource);
        Point endPos = mousePos;
        bool isValid = false;

        // 2. 重置上一次悬停的卡片样式
        if (_hoverBorder != null) ResetBorderVisuals(_hoverBorder);
        _hoverBorder = null;
        _hoverTarget = null;

        // 3. 🌟 纯数学矩形边界检测 (无视 Mouse.Capture 屏蔽、无视 Canvas 遮挡)
        foreach (var border in _cardBorders)
        {
            if (!border.IsLoaded || border.DataContext is not ShareItem target) continue;

            try
            {
                // 获取卡片左上角相对于 HostGrid 的坐标
                Point borderPos = border.TranslatePoint(new Point(0, 0), HostGrid);
                Rect rect = new Rect(borderPos, new Size(border.ActualWidth, border.ActualHeight));

                // 判断鼠标是否落入该卡片矩形内
                if (rect.Contains(mousePos))
                {
                    _hoverBorder = border;
                    _hoverTarget = target;
                    isValid = IsValidTarget(_dragSource, target);

                    // 实时变色反馈
                    border.BorderThickness = new Thickness(2);
                    border.BorderBrush = new SolidColorBrush(isValid ? Colors.Green : Colors.Red);
                    border.Background = new SolidColorBrush(isValid ? Color.FromRgb(240, 255, 240) : Color.FromRgb(255, 240, 240));

                    // 合法则吸附到目标底部锚点
                    if (isValid)
                        endPos = GetEllipseCenter(_bottomPoints, target);

                    break; // 找到即退出
                }
            }
            catch { }
        }

        // 4. 绘制虚线
        if (_tempLine is not null)
            DrawBezier(_tempLine, startPos, endPos, isValid);
    }


    #endregion

    #region 2. 辅助方法与永久连线

    private bool IsValidTarget(ShareItem source, ShareItem target) =>
        target.FlowId < source.FlowId && target.Id != source.Id;

    private Point GetEllipseCenter(Dictionary<ShareItem, Ellipse> dict, ShareItem item)
    {
        if (dict.TryGetValue(item, out var ellipse) && ellipse.IsLoaded)
            return ellipse.TranslatePoint(new Point(ellipse.ActualWidth / 2, ellipse.ActualHeight / 2), HostGrid);
        return new Point(0, 0);
    }

    private void DrawBezier(Path path, Point start, Point end, bool isValid)
    {
        if (path == null) return;
        double dy = Math.Max(Math.Abs(end.Y - start.Y) * 0.5, 10);
        path.Data = new PathGeometry
        {
            Figures = new PathFigureCollection
            {
                new PathFigure
                {
                    StartPoint = start,
                    Segments = new PathSegmentCollection
                    {
                        new BezierSegment { Point1 = new Point(start.X, start.Y + dy), Point2 = new Point(end.X, end.Y - dy), Point3 = end, IsStroked = true }
                    }
                }
            }
        };
        path.Stroke = isValid ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Color.FromRgb(100, 149, 237));
    }

    private void ResetBorderVisuals(Border border)
    {
        border.BorderBrush = new SolidColorBrush(Color.FromRgb(221, 221, 221));
        border.BorderThickness = new Thickness(1);
        border.Background = Brushes.White;
    }

    private void CardBorder_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border && !_cardBorders.Contains(border))
            _cardBorders.Add(border);
    }

    private void CardBorder_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border)
            _cardBorders.Remove(border);
    }

    private void TopPoint_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.DataContext is ShareItem item) _topPoints[item] = ellipse;
        UpdateLines();
    }

    private void BottomPoint_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.DataContext is ShareItem item) _bottomPoints[item] = ellipse;
        UpdateLines();
    }

    private void Point_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Ellipse ellipse && ellipse.DataContext is ShareItem item)
        {
            _topPoints.Remove(item);
            _bottomPoints.Remove(item);
        }
    }

    private void MainScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isDragging) // 滚动时强制中断拖拽，防止坐标撕裂
        {
            _isDragging = false;
            if (_tempLine != null) { LinesCanvas.Children.Remove(_tempLine); _tempLine = null; }
            Mouse.Capture(null);
        }
        UpdateLines();
    }

    private void UpdateLines()
    {
        LinesCanvas.Children.Clear();
        if (DataContext is not ModifyInheritWindowViewModel vm || !HostGrid.IsLoaded) return;

        var allItems = vm.Data.SelectMany(f => f.Shares.Where(x => ShareClass.GetFlow(x.Id) == x.FlowId)).ToDictionary(x => x.Id);

        foreach (var item in vm.Data.SelectMany(f => f.Shares))
        {
            bool isFromAbove = ShareClass.GetFlow(item.Id) != item.FlowId;
            int targetId = isFromAbove ? item.Id : item.Inherit;
            if (targetId <= 0 || !allItems.TryGetValue(targetId, out var targetItem)) continue;

            if (_bottomPoints.TryGetValue(targetItem, out var bottomPoint) && _topPoints.TryGetValue(item, out var topPoint))
            {
                if (!bottomPoint.IsLoaded || !topPoint.IsLoaded) continue;
                try
                {
                    var start = bottomPoint.TranslatePoint(new Point(bottomPoint.ActualWidth / 2, bottomPoint.ActualHeight / 2), HostGrid);
                    var end = topPoint.TranslatePoint(new Point(topPoint.ActualWidth / 2, topPoint.ActualHeight / 2), HostGrid);
                    double dy = Math.Max(Math.Abs(end.Y - start.Y) * 0.5, 10);

                    var path = new Path { Stroke = new SolidColorBrush(Color.FromRgb(100, 149, 237)), StrokeThickness = 2, IsHitTestVisible = false };
                    path.Data = new PathGeometry { Figures = new PathFigureCollection { new PathFigure { StartPoint = start, Segments = new PathSegmentCollection { new BezierSegment { Point1 = new Point(start.X, start.Y + dy), Point2 = new Point(end.X, end.Y - dy), Point3 = end, IsStroked = true } } } } };
                    LinesCanvas.Children.Add(path);

                    var arrow = new Polygon { Points = new PointCollection { new Point(end.X, end.Y), new Point(end.X - 4, end.Y - 8), new Point(end.X + 4, end.Y - 8) }, Fill = path.Stroke, IsHitTestVisible = false };
                    LinesCanvas.Children.Add(arrow);
                }
                catch { }
            }
        }
    }

    private void ClearInherit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ShareItem item) item.Inherit = -1;
    }

    #endregion

}








public partial class ModifyInheritWindowViewModel : ObservableObject
{
    public int FundId { get; set; }


    public List<Row> Data { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool Changed { get; private set; }

    public ModifyInheritWindowViewModel(int fundId)
    {
        FundId = fundId;

        using var db = DbHelper.Base();

        var classes = db.QueryFundFactor<ShareClass[]>(fundId, FactorFields.ShareClasses);

        var flows = db.GetCollection<FundFlow>().Query().Where(x => x.FundId == fundId).Where(Query.In(nameof(FundFlow.Id), classes.Select(x => new BsonValue(x.FlowId)))).
            Select(x => new { x.Id, x.Date, x.Name }).ToEnumerable().ToDictionary(x => x.Id, x => x);


        var info = db.QueryFundShares(fundId);

        Data = info.OrderBy(x => x.FlowId).Select(x => new Row(x.FlowId, x.FlowName, x.Date, x.Shares.Select(y => new ShareItem
        {
            Id = y.Id,
            Name = y.Name,
            Requirement = y.Requirement,
            Inherit = y.Inherit,
            FlowId = x.FlowId,
        }).ToArray())).ToList();

        foreach (var row in Data)
        {
            foreach (var item in row.Shares)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ShareItem.Inherit))
                    {
                        Changed = true;
                    }
                };
            }
        }
    }

    public FundFactor<ShareClass[]>[] GetNew()
    {
        return [..Data.Select(row => new FundFactor<ShareClass[]>(FactorFields.ShareClasses, FundId, row.FlowId, [.. row.Shares.Select(x => new ShareClass
        {
            Id = x.Id,
            Name = x.Name,
            Requirement = x.Requirement,
            Inherit = x.Inherit
        })]))];


    }

    [RelayCommand(CanExecute = nameof(Changed))]
    public void Confirm(Window wnd)
    {

        // 有变化 
        if (Changed)
        {
            using var db = DbHelper.Base();

            FundFactor<ShareClass[]>[] entities = GetNew();
            db.GetCollection<IFundFactor>().Upsert(entities);

            WeakReferenceMessenger.Default.Send(new FundShareChangedMessage { FundId = FundId, FlowId = -1 });

            wnd.DialogResult = true;
        }
        wnd.Close();
    }


    [RelayCommand]
    public void Cancel(Window wnd)
    {
        wnd.DialogResult = false;
        wnd.Close();
    }


    public class Row
    {
        public Row(int flowId, string flowName, DateOnly date, ShareItem[] shares)
        {
            FlowId = flowId;
            FlowName = flowName;
            Date = date;
            Shares = shares;
        }

        public int FlowId { get; set; }

        public string FlowName { get; }

        public DateOnly Date { get; }

        public ShareItem[] Shares { get; set; }
    }



    public partial class ShareItem : ObservableObject
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public string? Requirement { get; set; }

        [ObservableProperty]
        public partial int Inherit { get; set; }

        public int FlowId { get; set; }

        public bool IsInherited => ShareClass.GetFlow(Id) != FlowId;

    }
}


#region 转换器

public class InheritDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int inheritId && inheritId > 0) return $"继承自 ID: {inheritId}";
        return "无继承";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InheritVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int id && id > 0) return Visibility.Visible;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

#endregion