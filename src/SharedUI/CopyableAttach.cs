using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FMO.Shared;

public class CopyableAttach
{
    public static readonly DependencyProperty CopyableProperty =
           DependencyProperty.RegisterAttached("Copyable", typeof(bool), typeof(CopyableAttach), new PropertyMetadata(false, OnCopyableChanged));
    public static bool GetCopyable(DependencyObject obj)
    {
        return (bool)obj.GetValue(CopyableProperty);
    }
    public static void SetCopyable(DependencyObject obj, bool value)
    {
        obj.SetValue(CopyableProperty, value);
    }
    private static void OnCopyableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
            {
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(element);
                if (adornerLayer is null && element is FrameworkElement fe && !fe.IsLoaded)
                    fe.Loaded += (s, e) => { adornerLayer = AdornerLayer.GetAdornerLayer(element); adornerLayer?.Add(new CopyableAdorner(element)); };
                else
                    adornerLayer?.Add(new CopyableAdorner(element));
            }
            else
            {
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(element);
                Adorner[] adorners = adornerLayer.GetAdorners(element);
                if (adorners != null)
                {
                    foreach (Adorner adorner in adorners)
                        adornerLayer.Remove(adorner);
                }
            }
        }
    }
}
public class CopyableAdorner : Adorner
{
    private readonly VisualCollection _visuals;
    private readonly Button _button;

    public CopyableAdorner(UIElement adornedElement) : base(adornedElement)
    {
        _visuals = new VisualCollection(this);
        // 创建 Button 控件
        _button = new Button();

        _button.Style = TryFindResource("ButtonIcon") as Style;
        _button.Visibility = Visibility.Collapsed;
        _button.Width = 30;
        _button.Height = 16;
        //_button.Background = Brushes.Red;
        _button.Padding = new Thickness(10, 0, 2, 0);
        _button.Margin = new Thickness(-1);
        _button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#326cf3"));
        var geo = Geometry.Parse("M208 0L332.1 0c12.7 0 24.9 5.1 33.9 14.1l67.9 67.9c9 9 14.1 21.2 14.1 33.9L448 336c0 26.5-21.5 48-48 48l-192 0c-26.5 0-48-21.5-48-48l0-288c0-26.5 21.5-48 48-48zM48 128l80 0 0 64-64 0 0 256 192 0 0-32 64 0 0 48c0 26.5-21.5 48-48 48L48 512c-26.5 0-48-21.5-48-48L0 176c0-26.5 21.5-48 48-48z");
        HandyControl.Controls.IconElement.SetGeometry(_button, geo);


        _button.Click += (sender, e) =>
        {
            switch (adornedElement)
            {
                case TextBlock tb:
                    Clipboard.SetDataObject(new DataObject(tb.Text));
                    break;

                case TextBox tb:
                    Clipboard.SetDataObject(new DataObject(tb.Text));
                    break;

                case Label tb:
                    Clipboard.SetDataObject(new DataObject(tb.Content));
                    break;
            }
        };


        _visuals.Add(_button);

        // 监听被装饰元素的鼠标进入和离开事件
        adornedElement.MouseEnter += (sender, e) =>
        {
            _button.Visibility = Visibility.Visible;
        };
        adornedElement.MouseLeave += (sender, e) =>
        {
            if (!_button.IsMouseOver)
                _button.Visibility = Visibility.Collapsed;
        };

        _button.MouseLeave += (sender, e) => _button.Visibility = Visibility.Collapsed;
    }


    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size MeasureOverride(Size constraint)
    {
        var sz = base.MeasureOverride(constraint);
        return new Size(30, 16);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // 获取被装饰元素的位置和大小
        Rect adornedElementRect = new Rect(AdornedElement.RenderSize);
        Point adornedElementPosition = AdornedElement.TranslatePoint(new Point(0, 0), this);
        // 假设让 Button 紧靠在被装饰元素的右侧
        // double buttonWidth = 42;
        //double buttonHeight = adornedElementRect.Height;

        double buttonX = adornedElementPosition.X + adornedElementRect.Width;
        double buttonY = adornedElementPosition.Y - finalSize.Height / 2 + adornedElementRect.Height / 2;
        // 安排 Button 的位置和大小
        _button.Arrange(new Rect(buttonX - 2, buttonY, finalSize.Width, finalSize.Height));
        return finalSize;
    }
}