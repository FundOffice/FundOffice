using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using LiteDB;
using MoT;
using System.Collections.ObjectModel;
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

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        UpdateLines();
    }

    private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ModifyInheritWindowViewModel vm)
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ModifyInheritWindowViewModel.Changed))
                    UpdateLines();
            };
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

        var allItems = vm.Data.SelectMany(f => f.Shares).ToLookup(x => x.Id);

        foreach (var item in vm.Data.SelectMany(f => f.Shares))
        {
            bool isFromAbove = ShareClass.GetFlow(item.Id) != item.FlowId;
            int targetId = isFromAbove ? item.Id : item.Inherit;
            if (targetId <= 0 || allItems[targetId].LastOrDefault(x => x.FlowId < item.FlowId) is not ShareItem targetItem) continue;


            if (_bottomPoints.TryGetValue(targetItem, out var bottomPoint) && _topPoints.TryGetValue(item, out var topPoint))
            {
                if (!bottomPoint.IsLoaded || !topPoint.IsLoaded) continue;
                try
                {
                    var start = bottomPoint.TranslatePoint(new Point(bottomPoint.ActualWidth / 2, bottomPoint.ActualHeight / 2), HostGrid);
                    var end = topPoint.TranslatePoint(new Point(topPoint.ActualWidth / 2, topPoint.ActualHeight / 2), HostGrid);
                    double dy = Math.Max(Math.Abs(end.Y - start.Y) * 0.5, 10);

                    var brush = item.Id == targetItem.Id ? Brushes.LightGray : new SolidColorBrush(Color.FromRgb(100, 149, 237));
                    var path = new Path { Stroke = brush, StrokeThickness = 2, IsHitTestVisible = false };
                    path.Data = new PathGeometry
                    {
                        Figures = new PathFigureCollection { new PathFigure {
                        StartPoint = start, Segments = new PathSegmentCollection { new BezierSegment {
                            Point1 = new Point(start.X, start.Y + dy),
                            Point2 = new Point(end.X, end.Y - dy),
                            Point3 = end,
                            IsStroked = true
                        } } } }
                    };
                    LinesCanvas.Children.Add(path);

                    var arrow = new Polygon
                    {
                        Points = new PointCollection { new Point(end.X, end.Y), new Point(end.X - 4, end.Y - 8), new Point(end.X + 4, end.Y - 8) },
                        Fill = brush,
                        IsHitTestVisible = false
                    };
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
    public string FundName { get; }
    public int FlowId { get; }

    public List<Row> Data { get; set; }

    private int[] _shareIds;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool Changed { get; private set; }

    public ModifyInheritWindowViewModel(int fundId, string fundName, int flowId)
    {
        FundId = fundId;
        FundName = fundName;
        FlowId = flowId;
        using var db = DbHelper.Base();

        var classes = db.QueryFundFactor<ShareClass[]>(fundId, FactorFields.ShareClasses);

        var flows = db.GetCollection<FundFlow>().Query().Where(x => x.FundId == fundId).Where(Query.In(nameof(FundFlow.Id), classes.Select(x => new BsonValue(x.FlowId)))).
            Select(x => new { x.Id, x.Date, x.Name }).ToEnumerable().ToDictionary(x => x.Id, x => x);


        var shareInfo = db.QueryFundShares(fundId);

        _shareIds = shareInfo.SelectMany(x => x.Shares.Select(y => y.Id)).ToArray();

        Data = shareInfo.OrderBy(x => x.FlowId).Select(x => new Row(this, FundName, x.FlowId, x.FlowName, x.Date, x.Shares.Select(y => new ShareItem
        {
            Id = y.Id,
            Name = y.Name,
            FundName = y.FundName ?? fundName + y.Name,
            Code = y.Code!,
            Requirement = y.Requirement,
            Inherit = y.Inherit,
            FlowId = x.FlowId,
        }).ToArray(), x.FlowId == flowId)).ToList();

        for (int i = 0; i < Data.Count; i++)
        {

            // 缺少当前flow
            if (Data[i].FlowId < flowId && (i == Data.Count - 1 || Data[i + 1].FlowId > flowId))
            {
                var x = Data[i];
                Data.Insert(i + 1, new Row(this, fundName, flowId, x.FlowName, x.Date, x.Shares.Select(y => new ShareItem
                {
                    Id = y.Id,
                    Name = y.Name,
                    FundName = y.FundName!,
                    Code = y.Code,
                    Requirement = y.Requirement,
                    Inherit = y.Inherit,
                    FlowId = flowId,
                }).ToArray(), true));
                break;
            }
        }


    }

    public FundFactor<ShareClass[]>[] GetNew()
    {
        return [..Data.Select(row => new FundFactor<ShareClass[]>(FactorFields.ShareClasses, FundId, row.FlowId, [.. row.Shares.Select(x => new ShareClass
        {
            Id = x.Id,
            Name = x.Name,
            FundName = x.FundName!,
            Code = x.Code,
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
            // 检验合法
            var currentShares = Data.FirstOrDefault(x => x.FlowId == FlowId)!.Shares;
            foreach (var share in currentShares)
            {
                if (string.IsNullOrWhiteSpace(share.Name) || string.IsNullOrWhiteSpace(share.Code) || string.IsNullOrWhiteSpace(share.FundName) || (currentShares.Count == 1 || string.IsNullOrWhiteSpace(share.Requirement)))
                {
                    HandyControl.Controls.MessageBox.Show($"请确保份额{share.Name}名称、代码、基金名称均不为空", "提示");
                    return;
                }
            }


            using var db = DbHelper.Base();

            var upd = GetNew();
            db.GetCollection<IFundFactor>().DeleteMany(x => x.FundId == FundId && x.FactorId == FactorFields.ShareClasses);
            db.GetCollection<IFundFactor>().Upsert(upd);

            // 删除关联要素
            var newShareIds = upd.SelectMany(x => x.Data.Select(y => y.Id));
            var del = _shareIds.Except(newShareIds).ToArray();
            if (del.Length > 0)
            {
                Logg.Information($"删除份额相关要素：{string.Join(',', del)}");
                db.GetCollection<IFundFactor>().DeleteMany(Query.And(Query.EQ(nameof(IFundFactor.FundId), FundId), Query.In(nameof(IFundFactor.ShareId), del.Select(x => new BsonValue(x)))));
            }

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


    public partial class Row : ObservableObject
    {
        public Row(ModifyInheritWindowViewModel viewModel, string fundName, int flowId, string flowName, DateOnly date, ShareItem[] shares, bool isEnabled = false)
        {
            ViewModel = viewModel;
            FundName = fundName;
            FlowId = flowId;
            FlowName = flowName;
            Date = date;
            Shares = new(shares);
            IsEnabled = isEnabled;

            var isInherit = Shares.Any(x => x.Id / 1000 < flowId);

            foreach (var item in Shares)
                item.Changed += () => viewModel.Changed = true;
            Shares.CollectionChanged += (s, e) =>
            {
                viewModel.Changed = true;
                if (e.NewItems is not null)
                    foreach (var item in e.NewItems.OfType<ShareItem>())
                        item.Changed += () => viewModel.Changed = true;
            };
        }

        public ModifyInheritWindowViewModel ViewModel { get; }

        public string FundName { get; }

        public int FlowId { get; set; }

        public string FlowName { get; }

        public DateOnly Date { get; }

        [ObservableProperty]
        public partial bool IsEnabled { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMultipleShare))]
        public partial ObservableCollection<ShareItem> Shares { get; set; }

        public bool HasMultipleShare => Shares.Count > 1;

        public List<ShareItem> Deleted { get; } = [];



        private string GetNextClass()
        {
            var cnt = Shares.Count;
            var tmp = ((char)('A' + cnt++)).ToString();
            while (Shares.Any(x => x.Name == tmp))
            {
                tmp = ((char)('A' + cnt++)).ToString();
            }
            return tmp;
        }

        /// <summary>
        /// share是继承的，复制一份成可编辑，不增加share数量
        /// </summary>
        [RelayCommand]
        public void Copy(ShareItem share)
        {
            var id = Math.Max(Shares.Max(x => x.Id) + 1, ShareClass.MakeId(FlowId, 1));
            var item = new ShareItem
            {
                Name = share.Name,
                Code = share.Code,
                FundName = share.FundName!,
                Requirement = share.Requirement,
                CopyFrom = share.Id,
                FlowId = FlowId,
                IsNew = true,
                Inherit = share.Id,
                Id = id
            };
            var idx = Shares.IndexOf(share);
            Shares.RemoveAt(idx);
            Shares.Insert(idx, item);

            var relied = ViewModel.Data.Where(x => x.FlowId > FlowId && x.Shares.Any(y => y.Id == share.Id)).ToArray();
            foreach (var row in relied)
            {
                foreach (var sc in row.Shares.ToArray())
                {
                    if (sc.Id == share.Id)
                        sc.Id = item.Id;
                    else if (sc.Inherit == share.Id)
                        sc.Inherit = share.Inherit;
                }
            }
            ViewModel.Changed = true;
        }

        /// <summary>
        /// 从share复制，新增一个份额类别
        /// </summary>
        /// <param name="share"></param>
        [RelayCommand]
        public void Split(ShareItem share)
        {
            var id = Math.Max(Shares.Max(x => x.Id) + 1, ShareClass.MakeId(FlowId, 1));
            if (Shares.Count == 1)
            {
                Shares[0].Name = "A";
                Shares[0].FundName = FundName + "A";
                Shares[0].Code = Shares[0].Code[1..] + "A";
                if (Shares[0].IsInherited)
                {
                    Shares[0].Inherit = Shares[0].Id;
                    Shares[0].CopyFrom = Shares[0].Id;
                    Shares[0].Id = id++;
                }
            }

            string scn = GetNextClass();
            var item = new ShareItem
            {
                Name = scn,
                Code = share.Code[..^1] + scn,
                FundName = FundName + scn,
                Requirement = "请填写份额要求",
                FlowId = FlowId,
                IsNew = true,
                Inherit = share.IsInherited ? share.Id : share.Inherit,
                CopyFrom = share.Id,
                Id = id
            };
            Shares.Add(item);
            ViewModel.Changed = true;
        }

        [RelayCommand]
        public void Delete(ShareItem share)
        {
            // 全部删除，从上一个复制
            if (Shares.Count == 1)
            {
                var last = ViewModel.Data.LastOrDefault(x => x.FlowId < FlowId);

                if (last is null) return;

                Shares = [..last.Shares.Select(s => new ShareItem
                {
                    Name = s.Name,
                    Code = s.Code,
                    FundName = s.FundName,
                    Requirement = s.Requirement,
                    CopyFrom = s.Id,
                    FlowId = FlowId,
                    IsNew = true,
                    Inherit = s.Id,
                    Id = s.Id
                })];

                // 处理后续依赖
                foreach (var item in ViewModel.Data.Where(x => x.FlowId > FlowId))
                {
                    foreach (var sc in item.Shares)
                    {
                        if (sc.Id == share.Id)
                            sc.Id = share.Inherit;
                        else if (sc.Inherit == share.Id)
                            sc.Inherit = share.Inherit;
                    }
                }

                ViewModel.Changed = true;
                return;
            }

            // 检查有没有后续flow依赖此份额
            var relied = ViewModel.Data.Where(x => x.FlowId > FlowId && x.Shares.Any(y => y.Id == share.Id)).ToArray();
            if (relied.Length > 0)
            {
                var mr = HandyControl.Controls.MessageBox.Show("是否同步删除后续流程中使用的此份额？\n\n确认：删除此份额\n取消：放弃",
                    "提示", MessageBoxButton.OKCancel);

                if (mr == MessageBoxResult.Cancel) return;


                share.DeleteRelated = true;
                foreach (var item in relied)
                {
                    foreach (var sc in item.Shares.ToArray())
                    {
                        if (sc.Id == share.Id)
                            item.Shares.Remove(sc);
                    }

                    if (item.Shares.Count == 1)
                    {
                        item.Shares[0].Name = ShareClass.SingletonName;
                        item.Shares[0].FundName = FundName;
                        item.Shares[0].Requirement = null;
                    }
                }

            }

            foreach (var item in ViewModel.Data.Where(x => x.FlowId > FlowId))
            {
                foreach (var sc in item.Shares)
                {
                    if (sc.Inherit == share.Id)
                        sc.Inherit = share.Inherit;
                }
            }



            Deleted.Add(share);
            Shares.Remove(share);

            if (Shares.Count == 1)
            {
                Shares[0].Name = ShareClass.SingletonName;
                Shares[0].Code = "S" + Shares[0].Code[..^1];

                Shares[0].FundName = FundName;
            }
            ViewModel.Changed = true;
        }
    }



    public partial class ShareItem : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInherited))]
        public partial int Id { get; set; }

        [ObservableProperty]
        public partial string Name { get; set; }



        [ObservableProperty]
        public partial string FundName { get; set; }

        [ObservableProperty]
        public partial string Code { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInherited))]
        public partial string? Requirement { get; set; }

        public int CopyFrom { get; set; }

        [ObservableProperty]
        public partial int Inherit { get; set; }

        public int FlowId { get; set; }

        /// <summary>
        /// 新增加的
        /// </summary>
        public bool IsNew { get; set; }

        public bool IsInherited => ShareClass.GetFlow(Id) != FlowId;

        public bool DeleteRelated { get; internal set; }

        public delegate void ChangedHandler();
        public event ChangedHandler? Changed;

        partial void OnInheritChanged(int oldValue, int newValue)
        {
            if (oldValue > 0) Changed?.Invoke();
        }

        partial void OnNameChanged(string oldValue, string newValue)
        {
            if (!string.IsNullOrWhiteSpace(oldValue)) Changed?.Invoke();
        }

        partial void OnRequirementChanged(string? oldValue, string? newValue)
        {
            if (!string.IsNullOrWhiteSpace(oldValue)) Changed?.Invoke();
        }

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