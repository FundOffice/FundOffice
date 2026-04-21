using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FMO;

/// <summary>
/// NetValueCurveView.xaml 的交互逻辑
/// </summary>
public partial class NetValueCurveView : UserControl
{
    public NetValueCurveView()
    {
        InitializeComponent();
    }

    private void SetDateRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string s && DataContext is DailyValueCurveViewModel vm)
        {
            var t = DateTime.Now;
            int y = t.Year, m = t.Month, d = t.Day, w = (int)t.DayOfWeek, md = (int)DayOfWeek.Monday;
            var now = new DateOnly(y, m, d);

            switch (s)
            {
                case "上一周":
                    vm.StartDate = now.AddDays(-7 - w + md);
                    vm.EndDate = now.AddDays(-1 - w + md);
                    break;
                case "本周":
                    vm.StartDate = now.AddDays(-w + md);
                    vm.EndDate = now;
                    break;
                case "近一周":
                    vm.StartDate = now.AddDays(-7);
                    vm.EndDate = now;
                    break;
                case "上个月":
                    vm.StartDate = now.AddDays(-d + 1).AddMonths(-1);
                    vm.EndDate = now.AddDays(-d);
                    break;
                case "近一月":
                    vm.StartDate = now.AddMonths(-1);
                    vm.EndDate = now;
                    break;
                case "本月":
                    vm.StartDate = now.AddDays(-d + 1);
                    vm.EndDate = now;
                    break;

                case "上季度":
                    vm.StartDate = m <= 3 ? new DateOnly(y - 1, 10, 1) : new DateOnly(y, Math.Max(1, (m - 1) / 3 * 3 - 2), 1);
                    vm.EndDate = vm.StartDate.Value.AddMonths(3).AddDays(-1);
                    break;
                case "本季度":
                    vm.StartDate = new DateOnly(y, (m - 1) / 3 * 3 + 1, 1);
                    vm.EndDate = now;
                    break;

                case "本年度":
                    vm.StartDate = new DateOnly(y, 1, 1);
                    vm.EndDate = now;
                    break;

                case "近一年":
                    vm.StartDate = now.AddYears(-1);
                    vm.EndDate = now;
                    break;
                case "近两年":
                    vm.StartDate = now.AddYears(-2);
                    vm.EndDate = now;
                    break;
                case "近三年":
                    vm.StartDate = now.AddYears(-3);
                    vm.EndDate = now;
                    break;
                case "成立至今":
                    vm.StartDate = vm.SetupDate;
                    vm.EndDate = now;
                    break;
                default:
                    break;
            }

            vm.CustomDateRange = s != "成立至今";
        }
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext context = drawingVisual.RenderOpen())
            {
                VisualBrush brush = new VisualBrush(visual) { Stretch = Stretch.None };
                context.DrawRectangle(brush, null, new Rect(0, 0, visual.ActualWidth, visual.ActualHeight));
                context.Close();
            }

            var rtb = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Default);
            rtb.Render(drawingVisual);
             
            var dataObj = new DataObject();
           dataObj.SetData(DataFormats.Bitmap, rtb);

            //var ms = new MemoryStream();
            //var encoder = new PngBitmapEncoder();
            //encoder.Frames.Add(BitmapFrame.Create(rtb));
            //encoder.Save(ms);
            //ms.Seek(0, SeekOrigin.Begin);
            //dataObj.SetData("PNG", ms);

            Clipboard.SetDataObject(dataObj, true);
            HandyControl.Controls.Growl.Success("净值曲线已复制");
        }
        catch { HandyControl.Controls.Growl.Error("复制净值曲线失败"); }
    }
}


public partial class DailyValueCurveViewModel : ObservableObject
{
    #region property

    public DateOnly SetupDate { get; set; }

    public string? FundName { get; set; }

    [ObservableProperty]
    private List<DailyValue>? _data;


    public List<DailyValue>? Choosed { get; private set; }

    [ObservableProperty]
    private decimal? _lastValue;

    [ObservableProperty]
    private decimal? _aPY;

    [ObservableProperty]
    private decimal? _maxDrawDown;

    [ObservableProperty]
    private decimal? _valueChanged;

    [ObservableProperty]
    private bool _drawNetValue = true;

    [ObservableProperty]
    private bool _drawCumValue = true;


    [ObservableProperty]
    private bool _drawNetAssets = false;

    [ObservableProperty]
    private bool _drawAssets = false;

    [ObservableProperty]
    private bool _drawShares = true;


    [ObservableProperty]
    private Brush _netValueBrush = Brushes.Red;

    [ObservableProperty]
    private Brush _cumValueBrush = Brushes.Blue;

    [ObservableProperty]
    private Brush _netAssetsBrush = Brushes.Purple;

    [ObservableProperty]
    private Brush _assetsBrush = Brushes.Orange;

    [ObservableProperty]
    private Brush _sharesBrush = Brushes.PaleTurquoise;



    [ObservableProperty]
    private DateOnly? _startDate;

    [ObservableProperty]
    private DateOnly? _endDate;

    public bool CustomDateRange { get; set; } = false;


    public SolidColorBrush[] BrushCollection { get; } = [Brushes.AliceBlue, Brushes.PaleGoldenrod, Brushes.Orchid, Brushes.OrangeRed, Brushes.Orange, Brushes.OliveDrab, Brushes.Olive, Brushes.OldLace, Brushes.Navy, Brushes.NavajoWhite, Brushes.Moccasin, Brushes.MistyRose, Brushes.MintCream, Brushes.MidnightBlue, Brushes.MediumVioletRed, Brushes.MediumTurquoise, Brushes.MediumSpringGreen, Brushes.MediumSlateBlue, Brushes.LightSkyBlue, Brushes.LightSlateGray, Brushes.LightSteelBlue, Brushes.LightYellow, Brushes.Lime, Brushes.LimeGreen, Brushes.PaleGreen, Brushes.Linen, Brushes.Maroon, Brushes.MediumAquamarine, Brushes.MediumBlue, Brushes.MediumOrchid, Brushes.MediumPurple, Brushes.MediumSeaGreen, Brushes.Magenta, Brushes.PaleTurquoise, Brushes.PaleVioletRed, Brushes.PapayaWhip, Brushes.SlateGray, Brushes.Snow, Brushes.SpringGreen, Brushes.SteelBlue, Brushes.Tan, Brushes.Teal, Brushes.SlateBlue, Brushes.Thistle, Brushes.Transparent, Brushes.Turquoise, Brushes.Violet, Brushes.Wheat, Brushes.White, Brushes.WhiteSmoke, Brushes.Tomato, Brushes.LightSeaGreen, Brushes.SkyBlue, Brushes.Sienna, Brushes.PeachPuff, Brushes.Peru, Brushes.Pink, Brushes.Plum, Brushes.PowderBlue, Brushes.Purple, Brushes.Silver, Brushes.Red, Brushes.RoyalBlue, Brushes.SaddleBrown, Brushes.Salmon, Brushes.SandyBrown, Brushes.SeaGreen, Brushes.SeaShell, Brushes.RosyBrown, Brushes.Yellow, Brushes.LightSalmon, Brushes.LightGreen, Brushes.DarkRed, Brushes.DarkOrchid, Brushes.DarkOrange, Brushes.DarkOliveGreen, Brushes.DarkMagenta, Brushes.DarkKhaki, Brushes.DarkGreen, Brushes.DarkGray, Brushes.DarkGoldenrod, Brushes.DarkCyan, Brushes.DarkBlue, Brushes.Cyan, Brushes.Crimson, Brushes.Cornsilk, Brushes.CornflowerBlue, Brushes.Coral, Brushes.Chocolate, Brushes.AntiqueWhite, Brushes.Aqua, Brushes.Aquamarine, Brushes.Azure, Brushes.Beige, Brushes.Bisque, Brushes.DarkSalmon, Brushes.Black, Brushes.Blue, Brushes.BlueViolet, Brushes.Brown, Brushes.BurlyWood, Brushes.CadetBlue, Brushes.Chartreuse, Brushes.BlanchedAlmond, Brushes.DarkSeaGreen, Brushes.DarkSlateBlue, Brushes.DarkSlateGray, Brushes.HotPink, Brushes.IndianRed, Brushes.Indigo, Brushes.Ivory, Brushes.Khaki, Brushes.Lavender, Brushes.Honeydew, Brushes.LavenderBlush, Brushes.LemonChiffon, Brushes.LightBlue, Brushes.LightCoral, Brushes.LightCyan, Brushes.LightGoldenrodYellow, Brushes.LightGray, Brushes.LawnGreen, Brushes.LightPink, Brushes.GreenYellow, Brushes.Gray, Brushes.DarkTurquoise, Brushes.DarkViolet, Brushes.DeepPink, Brushes.DeepSkyBlue, Brushes.DimGray, Brushes.DodgerBlue, Brushes.Green, Brushes.Firebrick, Brushes.ForestGreen, Brushes.Fuchsia, Brushes.Gainsboro, Brushes.GhostWhite, Brushes.Gold, Brushes.Goldenrod, Brushes.FloralWhite, Brushes.YellowGreen];


    Debouncer _debouncer { get; }

    public int FundId { get; internal set; }

    [ObservableProperty]
    public partial List<FundStrategy>? Strategies { get; set; }

    #endregion


    public DailyValueCurveViewModel()
    {
        _debouncer = new(() => ResetDaily(), 100);
    }

    public static decimal CalcMaxDrawdown(decimal[] vals)
    {
        decimal dr = 0;
        for (int i = 0; i < vals.Length; i++)
        {
            for (int j = i + 1; j < vals.Length; j++)
            {
                if (vals[i] != 0)
                    dr = Math.Max(dr, (vals[i] - vals[j]) / vals[i]);
            }
        }
        return dr;
    }

    internal void ResetDaily()
    {
        Choosed = Data?.Where(x => x.Date >= StartDate && x.Date <= EndDate && x.NetValue > 0)?.ToList();

        LastValue = Choosed?.LastOrDefault()?.CumNetValue;
        APY = Choosed is null || Choosed.Count() < 2 ? null : (Choosed.Last().CumNetValue - Choosed.First().CumNetValue) / (Choosed.Last().Date.DayNumber - Choosed.First().Date.DayNumber) * 365;
        MaxDrawDown = Choosed is null || Choosed.Count() < 2 ? null : CalcMaxDrawdown(Choosed.Select(x => x.CumNetValue).ToArray());
        ValueChanged = Choosed?.LastOrDefault()?.CumNetValue - Choosed?.FirstOrDefault()?.CumNetValue;
    }

    partial void OnStartDateChanged(DateOnly? value)
    {
        _debouncer.Invoke();
    }

    partial void OnEndDateChanged(DateOnly? value)
    {
        _debouncer.Invoke();
    }

    partial void OnDataChanged(List<DailyValue>? value)
    {
        _debouncer.Invoke();
    }


    [RelayCommand]
    public void SetDateByStrategy(FundStrategy strategy)
    {
        if (strategy.Start >= strategy.End)
        {
            HandyControl.Controls.Growl.Warning("不合法的净值日期范围，起始日期大于终止日期");
            return;
        }
        StartDate = strategy.Start;
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        EndDate = strategy.End > today ? today : strategy.End;
    }
}

public class Axis
{
    public Rect Rect { get; set; }

    public decimal Unit { get; set; }

    public decimal Max { get; set; }

    public decimal Min { get; set; }

    public double Scale => Rect.Height / (double)(Max - Min);
}

public class DailyValueCurveDrawing : FrameworkElement
{

    #region visual
    private DrawingVisual _backgroundVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };

    private DrawingVisual _netValueVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };

    private DrawingVisual _cumValueVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };

    private DrawingVisual _sharesVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };

    private DrawingVisual _netAssetsVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };

    private DrawingVisual _assetsVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };

    private DrawingVisual _dateSepVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };

    private DrawingVisual _tooltipVisual { get; } = new() { XSnappingGuidelines = [0.5, 0.5], YSnappingGuidelines = [0.5, 0.5] };


    private readonly VisualCollection _children;
    #endregion


    private Debouncer _debouncer { get; }

    private Debouncer _hover;

    private Point _mousePoint;

    public DailyValueCurveDrawing()
    {
        _children = new VisualCollection(this) { _backgroundVisual, _dateSepVisual, _netAssetsVisual, _assetsVisual, _sharesVisual, _cumValueVisual, _netValueVisual, _tooltipVisual };

        DataContextChanged += DailyValueCurveDrawing_DataContextChanged;
        _debouncer = new(() => App.Current.Dispatcher.BeginInvoke(() => Update()), 100);
        _hover = new(() => App.Current.Dispatcher.BeginInvoke(() => ShowTip(_mousePoint)), 500);

        Loaded += (s, e) => Update();
    }

    private void DailyValueCurveDrawing_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is DailyValueCurveViewModel vm)
            vm.PropertyChanged += Vm_PropertyChanged;
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        //Debug.WriteLine(nameof(Vm_PropertyChanged), e.PropertyName);
        switch (e.PropertyName)
        {
            case nameof(DailyValueCurveViewModel.Data):
            case nameof(DailyValueCurveViewModel.StartDate):
            case nameof(DailyValueCurveViewModel.EndDate):
            case string n when n.Contains("Draw"):
            case string b when b.Contains("Brush"):
                Dispatcher.BeginInvoke(() =>
                {
                    if (DataContext is DailyValueCurveViewModel vm)
                        _debouncer.Invoke();
                });
                break;

            default:
                break;
        }
    }

    protected override int VisualChildrenCount => _children.Count;

    // Provide a required override for the GetVisualChild method.
    protected override Visual GetVisualChild(int index)
    {
        if (index < 0 || index >= _children.Count)
        {
            throw new ArgumentOutOfRangeException();
        }

        return _children[index];
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        _mousePoint = e.GetPosition(this);
        _hover.Invoke();
    }







    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        _tooltipVisual.RenderOpen().Close();
        //Task.Run(async () =>
        //{
        //    await Task.Delay(500);

        //    await App.Current.Dispatcher.BeginInvoke(() =>
        //    {
        //        _tooltipVisual.RenderOpen().Close();
        //        InvalidateVisual();
        //    });
        //});
    }

    public Pen _blackPen { get; } = new Pen(Brushes.Black, 1);
    public Pen _gridPen { get; } = new Pen(Brushes.LightGray, 1) { DashStyle = DashStyles.Dash };
    public Pen _gridPen2 { get; } = new Pen(new SolidColorBrush(Color.FromRgb(240, 240, 240)), 1) { DashStyle = DashStyles.Dash };
    public Pen _gridPeny { get; } = new Pen(new SolidColorBrush(Color.FromRgb(240, 240, 240)), 2) { DashStyle = DashStyles.Dash };

    public double _heightOfXAxis { get; set; } = 80;
    public double _heightOfTopInfo { get; set; } = 50;

    private double _widthOfYAxis { get; set; } = 50;

    private double _bodyWidth = 0;

    private Axis _left = new();
    private Axis _right = new();

    public void Update()
    {
        if (DataContext is not DailyValueCurveViewModel vm || vm.Choosed is null) return;

        var data = vm.Choosed.Where(x => x.NetValue != 0 && x.CumNetValue != 0).ToArray();
        if (data.Length < 2) { DrawBlank(); return; }

        if (ActualWidth == 0 || ActualHeight == 0) return;

        GenerateAxis(data, vm);

        double w = ActualWidth, h = ActualHeight;
        // 绘制净值
        //if (vm.DrawNetValue || vm.DrawCumValue)
        {
            var min = vm.DrawNetValue ? data.Min(x => x.NetValue) : data.Min(x => x.CumNetValue);
            var max = vm.DrawCumValue ? data.Max(x => x.CumNetValue) : data.Max(x => x.NetValue);


            DrawBackground(ref min, ref max, w, h, data, vm);
            DrawDateSeprator(w, h, data);

            DrawCumValue(min, max, w, h, data, vm);
            DrawNetValue(min, max, w, h, data, vm);

            DrawShares(data, vm);
            DrawNetAssets(data, vm);
            DrawAssets(data, vm);
        }

    }

    private decimal CalcUnit(decimal width)
    {
        int len = 1;
        decimal tmp = width;
        for (len = 0; len < 10; len++)
            if (tmp > 1) break;
            else tmp *= 10;

        var v = Math.Ceiling(tmp / 5) * 5 / (decimal)Math.Pow(10, len + 1);
        return v;
    }

    private string[] GenY(decimal min, decimal max)
    {
        min *= 0.95m;
        max /= 0.95m;

        var unit = Math.Ceiling((max - min) / 10);
        if ((decimal)Math.Pow(10, unit.ToString().Length - 1) is decimal xx)
            unit = Math.Round(unit / xx) * xx;

        min = Math.Floor(min / unit) * unit;
        max = Math.Ceiling(max / unit) * unit;

        List<string> list = new();
        for (decimal i = min; i < max; i += unit)
            list.Add(i.ToString());

        return list.ToArray();
    }

    private void GenerateAxis(DailyValue[] data, DailyValueCurveViewModel vm)
    {
        var min = vm.DrawNetValue ? data.Min(x => x.NetValue) : data.Min(x => x.CumNetValue);
        var max = vm.DrawCumValue ? data.Max(x => x.CumNetValue) : data.Max(x => x.NetValue);
        //decimal unit = Math.Ceiling((max - min) / 10 / 0.1m) * 0.1m;
        decimal unit = CalcUnit(max - min);// Math.Ceiling((max - min) / 10);
        //if ((decimal)Math.Pow(10, unit.ToString().Length - 1) is decimal x)
        //    unit = Math.Round(unit / x) * x;

        min = decimal.Floor(min / unit) * unit;
        max = decimal.Ceiling(max / unit) * unit;
        _left = new Axis { Max = max, Min = min, Unit = unit, Rect = new Rect(_widthOfYAxis, _heightOfTopInfo, ActualWidth - _widthOfYAxis * 2, ActualHeight - _heightOfTopInfo - _heightOfXAxis) };


        var xd = new List<decimal>();
        xd.Add(data.Min(x => x.Share)); xd.Add(data.Max(x => x.Share));
        if (vm.DrawNetAssets) xd.Add(data.Max(x => x.NetAsset));
        if (vm.DrawAssets) xd.Add(data.Max(x => x.Asset));



        var min2 = xd.Min() * 0.9m;
        var max2 = xd.Max();

        if (min2 == max2) { max2 *= 1.1m; min2 *= 0.9m; }
        unit = Math.Ceiling((max2 - min2) / 10);
        if ((decimal)Math.Pow(10, unit.ToString().Length - 1) is decimal xx)
            unit = Math.Round(unit / xx) * xx;

        min2 = decimal.Floor(min2 / unit) * unit;
        max2 = decimal.Ceiling(max2 / unit) * unit;

        _right = new Axis { Max = max2, Min = min2, Unit = unit, Rect = new Rect(_widthOfYAxis, _heightOfTopInfo, ActualWidth - _widthOfYAxis * 2, ActualHeight - _heightOfTopInfo - _heightOfXAxis) };

    }

    private void DrawDateSeprator(double w, double h, DailyValue[] data)
    {
        var dc = _dateSepVisual.RenderOpen();

        var dates = data.Last().Date.DayNumber - data.First().Date.DayNumber;


        // 日期线
        int year = data.First().Date.Year;
        int month = data.First().Date.Month;
        for (int i = 1; i < data.Length - 1; i++)
        {

            double bodyw = _bodyWidth;
            var xp = _widthOfYAxis + bodyw * i / (data.Length - 1);


            if (dates < 400)
            {
                if (data[i + 1].Date.Month != month)
                {
                    if (month != 12)
                    {
                        dc.DrawLine(_gridPeny, new Point(xp, _heightOfTopInfo), new Point(xp, h - _heightOfXAxis));

                        if (xp < _left.Rect.Right - 50 && xp > _left.Rect.Left + 50)
                        {
                            var fmt = new FormattedText($"{month}.{data[i].Date.Day}", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);
                            dc.DrawText(fmt, new Point(xp - fmt.Width / 2, h - _heightOfXAxis + 10));
                        }
                    }
                    month = month % 12 + 1;
                }
            }

            if (dates < 40)
            {
                if (data[i].Date.DayOfWeek < data[i - 1].Date.DayOfWeek)
                {
                    if (data[i].Date.Day > 4 && data[i].Date.Day < 26)
                    {
                        dc.DrawLine(_gridPen2, new Point(xp, _heightOfTopInfo), new Point(xp, h - _heightOfXAxis));

                        if (xp < _left.Rect.Right - 50 && xp > _left.Rect.Left + 50)
                        {
                            var fmt = new FormattedText($"{data[i].Date.ToString("yyyy.MM.dd")}", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);

                            Point origin = new Point(xp - fmt.Width / 2, h - _heightOfXAxis + 10);
                            dc.DrawRectangle(Brushes.White, null, new Rect(origin, new Size(fmt.Width, fmt.Height)));
                            dc.DrawText(fmt, origin);
                        }
                    }
                }
            }

            if (dates < 15)
            {
                var fmt = new FormattedText($"{data[i].Date.ToString("yyyy.MM.dd")}", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);
                Point origin = new Point(xp - fmt.Width / 2, h - _heightOfXAxis + 10);
                dc.DrawRectangle(Brushes.White, null, new Rect(origin, new Size(fmt.Width, fmt.Height)));
                dc.DrawText(fmt, new Point(xp - fmt.Width / 2, h - _heightOfXAxis + 10));
            }

            if (data[i].Date.Year != year)
            {
                dc.DrawLine(_gridPen, new Point(xp, _heightOfTopInfo), new Point(xp, h - _heightOfXAxis));

                if (xp < _left.Rect.Right - 50 && xp > _left.Rect.Left + 50)
                {
                    var fmt = new FormattedText($"{year + 1}", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);
                    Point origin = new Point(xp - fmt.Width / 2, h - _heightOfXAxis + 10);
                    dc.DrawText(fmt, origin);
                }
                ++year;

            }


        }





        dc.Close();

    }

    private void DrawNetValue(decimal min, decimal max, double w, double h, DailyValue[] data, DailyValueCurveViewModel vm)
    {
        var dc = _netValueVisual.RenderOpen();
        if (!vm.DrawNetValue) { dc.Close(); return; }


        double bodyh = h - _heightOfXAxis - _heightOfTopInfo;
        double bodyw = w - _widthOfYAxis * 2;
        double per = bodyh / (double)(max - min);
        int cnt = data.Length - 1;

        var ps = Enumerable.Range(0, data.Length).Select(x => new Point(_widthOfYAxis + bodyw * x / cnt, (double)(max - data[x].NetValue) * per + _heightOfTopInfo)).ToArray();

        var poly = new StreamGeometry();

        using (var ss = poly.Open())
        {
            ss.BeginFigure(ps[0], false, false);
            ss.PolyLineTo(ps[1..].ToList(), true, true);
        }

        dc.DrawGeometry(Brushes.Transparent, new Pen(vm.NetValueBrush, 2), poly);

        dc.Close();
    }
    private void DrawCumValue(decimal min, decimal max, double w, double h, DailyValue[] data, DailyValueCurveViewModel vm)
    {
        var dc = _cumValueVisual.RenderOpen();
        if (!vm.DrawCumValue || (vm.DrawNetValue && vm.Choosed!.Last().NetValue == vm.Choosed!.Last().CumNetValue)) { dc.Close(); return; }

        double bodyh = h - _heightOfXAxis - _heightOfTopInfo;
        double bodyw = w - _widthOfYAxis * 2;
        double per = bodyh / (double)(max - min);
        int cnt = data.Length - 1;

        var ps = Enumerable.Range(0, data.Length).Select(x => new Point(_widthOfYAxis + bodyw * x / cnt, (double)(max - data[x].CumNetValue) * per + _heightOfTopInfo)).ToArray();

        var poly = new StreamGeometry();

        using (var ss = poly.Open())
        {
            ss.BeginFigure(ps[0], false, false);
            ss.PolyLineTo(ps[1..].ToList(), true, true);
        }

        dc.DrawGeometry(Brushes.Transparent, new Pen(vm.CumValueBrush, 2), poly);

        dc.Close();
    }


    private void DrawShares(DailyValue[] data, DailyValueCurveViewModel vm)
    {
        var dc = _sharesVisual.RenderOpen();
        if (!vm.DrawShares) { dc.Close(); return; }


        double bodyh = _right.Rect.Height;
        double top = _right.Rect.Top;
        double bodyw = _right.Rect.Width;
        double per = _right.Scale;
        var max = _right.Max;
        var h = _right.Rect.Height;
        int cnt = data.Length - 1;

        var ps = Enumerable.Range(0, data.Length).Select(x => new Point(_widthOfYAxis + bodyw * x / cnt, top + (double)(max - data[x].Share) * per)).ToList();
        ps.Add(new Point(ps[^1].X, h + top));
        ps.Add(new Point(_widthOfYAxis, h + top));

        var poly = new StreamGeometry();

        using (var ss = poly.Open())
        {
            ss.BeginFigure(ps[0], true, true);
            ss.PolyLineTo(ps[1..], true, true);
        }

        var br = vm.SharesBrush.Clone();
        br.Opacity = 0.3;
        dc.DrawGeometry(br, new Pen(br, 1), poly);

        dc.Close();
    }
    private void DrawNetAssets(DailyValue[] data, DailyValueCurveViewModel vm)
    {
        var dc = _netAssetsVisual.RenderOpen();
        if (!vm.DrawNetAssets) { dc.Close(); return; }


        double bodyh = _right.Rect.Height;
        double top = _right.Rect.Top;
        double bodyw = _right.Rect.Width;
        double per = _right.Scale;
        var h = _right.Rect.Height;
        var max = _right.Max;
        int cnt = data.Length - 1;

        var ps = Enumerable.Range(0, data.Length).Select(x => new Point(_widthOfYAxis + bodyw * x / cnt, top + (double)(max - data[x].NetAsset) * per)).ToList();
        ps.Add(new Point(ps[^1].X, h + top));
        ps.Add(new Point(_widthOfYAxis, h + top));

        var poly = new StreamGeometry();

        using (var ss = poly.Open())
        {
            ss.BeginFigure(ps[0], true, true);
            ss.PolyLineTo(ps[1..], true, true);
        }

        var br = vm.NetAssetsBrush.Clone();
        br.Opacity = 0.3;
        dc.DrawGeometry(br, new Pen(br, 1), poly);

        dc.Close();
    }

    private void DrawAssets(DailyValue[] data, DailyValueCurveViewModel vm)
    {
        var dc = _assetsVisual.RenderOpen();
        if (!vm.DrawAssets) { dc.Close(); return; }


        double bodyh = _right.Rect.Height;
        double top = _right.Rect.Top;
        double bodyw = _right.Rect.Width;
        double per = _right.Scale;
        decimal max = _right.Max;
        double w = _right.Rect.Width, h = _right.Rect.Height;
        int cnt = data.Length - 1;

        var ps = Enumerable.Range(0, data.Length).Select(x => new Point(_widthOfYAxis + bodyw * x / cnt, top + (double)(max - data[x].Asset) * per)).ToList();
        ps.Add(new Point(ps[^1].X, h + top));
        ps.Add(new Point(_widthOfYAxis, h + top));

        var poly = new StreamGeometry();

        using (var ss = poly.Open())
        {
            ss.BeginFigure(ps[0], true, true);
            ss.PolyLineTo(ps[1..], true, true);
        }

        var br = vm.AssetsBrush.Clone();
        br.Opacity = 0.3;
        dc.DrawGeometry(br, new Pen(br, 1), poly);

        dc.Close();
    }

    private void DrawBackground(ref decimal min, ref decimal max, double w, double h, DailyValue[] data, DailyValueCurveViewModel vm)
    {
        var dc = _backgroundVisual.RenderOpen();

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        // 坐标


        // 右侧
        if (vm.DrawAssets || vm.DrawNetAssets || vm.DrawShares)
        {
            _bodyWidth = w - _widthOfYAxis * 2;
            // dc.DrawLine(_blackPen, new Point(w - _widthOfYAxis + 5, _heightOfTopInfo), new Point(w - _widthOfYAxis + 5, h - _heightOfXAxis));
        }
        dc.DrawLine(_blackPen, new Point(_widthOfYAxis, h - _heightOfXAxis), new Point(_widthOfYAxis + _bodyWidth + 5, h - _heightOfXAxis));
        dc.DrawLine(_blackPen, new Point(_widthOfYAxis, _heightOfTopInfo), new Point(_widthOfYAxis, h - _heightOfXAxis));

        // 起始时间
        var fmt = new FormattedText(data.First().Date.ToString("yyyy.MM.dd"), CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);
        dc.DrawText(fmt, new Point(_widthOfYAxis - fmt.Width / 2, h - _heightOfXAxis + 10));
        // 终止时间
        fmt = new FormattedText(data.Last().Date.ToString("yyyy.MM.dd"), CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);
        dc.DrawText(fmt, new Point(_right.Rect.Right - fmt.Width / 2, h - _heightOfXAxis + 10));


        // 左 y 分隔 
        if (vm.DrawNetValue || vm.DrawCumValue)
        {
            decimal unit = _left.Unit;

            min = decimal.Floor(min / unit) * unit;
            max = decimal.Ceiling(max / unit) * unit;

            double bodyh = h - _heightOfXAxis - _heightOfTopInfo;
            double per = bodyh / (double)(max - min);

            var yx = max;
            while (yx >= min)
            {
                var yp = (double)(max - yx) * per + _heightOfTopInfo;
                var xp = _widthOfYAxis;

                fmt = new FormattedText(yx.ToString(), CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);
                dc.DrawText(fmt, new Point(xp - fmt.Width - 8, yp - fmt.Height / 2));
                dc.DrawLine(yx == 1 ? _blackPen : _gridPen, new Point(xp, yp), new Point(xp + _bodyWidth + 5, yp));
                yx = yx - unit;
            }
        }

        if (vm.DrawAssets || vm.DrawNetAssets || vm.DrawShares)
        {
            double bodyh = _right.Rect.Height;
            double per = _right.Scale;

            var yx = _right.Max;
            while (yx > _right.Min)
            {
                var yp = (double)(_right.Max - yx) * per + _heightOfTopInfo;
                var xp = _widthOfYAxis;

                fmt = new FormattedText((yx / 10000).ToString("0万"), CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1);
                dc.DrawText(fmt, new Point(_widthOfYAxis + _bodyWidth + 10, yp));
                //dc.DrawLine(yx == 1 ? _blackPen : _gridPen, new Point(xp, yp), new Point(xp + _bodyWidth + 5, yp));
                yx = yx - _right.Unit;
            }
        }



        // 图例
        var ls = new List<(FormattedText t, Brush b)>();
        if (vm.DrawNetValue)
            ls.Add((new FormattedText("单位净值", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1), vm.NetValueBrush));
        if (vm.DrawCumValue)
            ls.Add((new FormattedText("累计净值", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1), vm.CumValueBrush));
        if (vm.DrawNetAssets)
            ls.Add((new FormattedText("资产净值", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1), vm.NetAssetsBrush));
        if (vm.DrawAssets)
            ls.Add((new FormattedText("资产总值", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1), vm.AssetsBrush));
        if (vm.DrawShares)
            ls.Add((new FormattedText("基金份额", CultureInfo.CurrentCulture, FlowDirection, new Typeface("等线"), 12, Brushes.Black, 1), vm.SharesBrush));

        var legendw = ls.Sum(x => x.t.Width) + ls.Count * 30;
        var sp = (w - legendw) / 2;
        for (int i = 0; i < ls.Count; i++)
        {
            var xp = sp;
            var yp = h - _heightOfXAxis + 50;

            dc.DrawLine(new Pen(ls[i].b, 3), new Point(xp, yp), new Point(xp + 8, yp));
            dc.DrawText(ls[i].t, new Point(xp + 10, yp - ls[i].t.Height / 2));

            sp += 30 + ls[i].t.Width;
        }

        dc.Close();
    }


    private void DrawBlank()
    {
        foreach (DrawingVisual item in _children)
            item.RenderOpen().Close();
    }


    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        Update();
    }


    private void ShowTip(Point mousePos)
    {
        if (DataContext is not DailyValueCurveViewModel vm || vm.Choosed is null)
            return;

        // 判断是否离开
        if (this.InputHitTest(Mouse.GetPosition(this)) is null)
            return;

        var data = vm.Choosed.Where(x => x.NetValue != 0 && x.CumNetValue != 0).ToArray();
        if (data.Length < 2) return;

        // 清除之前的 Tooltip
        _tooltipVisual.RenderOpen().Close();

        // 计算最近的数据点索引
        double bodyw = ActualWidth - _widthOfYAxis * 2;
        int closestIndex = (int)Math.Round(((mousePos.X - _widthOfYAxis) / bodyw) * (data.Length - 1));
        closestIndex = Math.Clamp(closestIndex, 0, data.Length - 1);

        // 获取当前数据点
        var point = data[closestIndex];

        // 创建 Tooltip 内容
        List<(string label, string value)> values = new()
            {
                ("日期", point.Date.ToString("yyyy-MM-dd")),
                ("单位净值", point.NetValue.ToString("F4")),
                ("累计净值", point.CumNetValue.ToString("F4")),
                ("基金份额", point.Share.ToString("F2")),
                ("资产净值", point.NetAsset.ToString("F2")),
                ("资产总值", point.Asset.ToString("F2"))
            };

        // 设置字体和格式
        Typeface typeface = new("等线");
        Brush foreground = Brushes.Black;
        double fontSize = 12;

        // 准备绘制区域
        double padding = 5;
        double rowHeight = fontSize + 4;
        double maxWidth = values.Max(v => CreateFormattedText(v.label + ": " + v.value, typeface, fontSize).Width) + padding * 2;

        // 获取当前可视区域的边界（减去margin）
        double margin = 50;
        double maxCanvasWidth = this.ActualWidth - margin * 2;
        double maxCanvasHeight = this.ActualHeight - margin * 2;

        // 计算初始位置
        double startX = mousePos.X + 10;
        double startY = mousePos.Y - 50;

        // 确保tooltip不会超出右边界
        if (startX + maxWidth > maxCanvasWidth + margin)
        {
            startX = maxCanvasWidth + margin - maxWidth;
        }

        // 确保tooltip不会超出左边界
        if (startX < margin)
        {
            startX = margin;
        }



        // 绘制背景矩形
        using (DrawingContext dc = _tooltipVisual.RenderOpen())
        {
            Rect backgroundRect = new(startX, startY, maxWidth, values.Count * rowHeight + padding * 2);
            dc.DrawRectangle(Brushes.White, new Pen(Brushes.Gray, 1), backgroundRect);

            // 绘制每一行文本
            for (int i = 0; i < values.Count; i++)
            {
                var item = values[i];
                FormattedText text = CreateFormattedText($"{item.label}: {item.value}", typeface, fontSize);
                dc.DrawText(text, new Point(startX + padding, startY + padding + i * rowHeight));
            }
        }

        // 将 Tooltip 添加到视觉树
        InvalidateVisual();
    }

    // 辅助方法：创建 FormattedText
    private FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection,
            typeface,
            fontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip
        );
    }
}
