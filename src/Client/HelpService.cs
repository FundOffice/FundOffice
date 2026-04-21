using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace FMO;




public enum Placement
{
    Left,
    Right,
    Top,
    Bottom,
    LeftTop,
    RightTop,
    LeftBottom,
    RightBottom,
    Center
}




/// <summary>
/// By deepseek
/// </summary>
public static class HelpService
{
    // 附加属性定义
    public static readonly DependencyProperty HelpProperty =
        DependencyProperty.RegisterAttached(
            "Help",
            typeof(DataTemplate),
            typeof(HelpService),
            new PropertyMetadata(null));


    public static readonly DependencyProperty PlacementProperty =
    DependencyProperty.RegisterAttached(
        "Placement",
        typeof(Placement),
        typeof(HelpService),
        new PropertyMetadata(Placement.Bottom));

    public static readonly DependencyProperty OffsetProperty =
        DependencyProperty.RegisterAttached(
            "Offset",
            typeof(Point),
            typeof(HelpService),
            new PropertyMetadata(new Point(0, 0)));


    [TypeConverter(typeof(StringToDataTemplateConverter))]
    public static DataTemplate GetHelp(DependencyObject obj) =>
        (DataTemplate)obj.GetValue(HelpProperty);

    [TypeConverter(typeof(StringToDataTemplateConverter))]
    public static void SetHelp(DependencyObject obj, DataTemplate value) =>
        obj.SetValue(HelpProperty, value);

    // 重载方法 - 这是关键
    //public static void SetHelp(DependencyObject obj, string value)
    //{
    //    var template = CreateDataTemplateFromString(value);
    //    obj.SetValue(HelpProperty, template);
    //}

    //private static DataTemplate CreateDataTemplateFromString(string text)
    //{
    //    var template = new DataTemplate();
    //    var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
    //    textBlockFactory.SetValue(TextBlock.TextProperty, text);
    //    textBlockFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
    //    template.VisualTree = textBlockFactory;
    //    return template;
    //}

    public static Placement GetPlacement(DependencyObject obj) =>
        (Placement)obj.GetValue(PlacementProperty);

    public static void SetPlacement(DependencyObject obj, Placement value) =>
        obj.SetValue(PlacementProperty, value);

    public static Point GetOffset(DependencyObject obj) =>
        (Point)obj.GetValue(OffsetProperty);

    public static void SetOffset(DependencyObject obj, Point value) =>
        obj.SetValue(OffsetProperty, value);


    public static bool HasHelp(DependencyObject obj) => obj.ReadLocalValue(HelpProperty) != DependencyProperty.UnsetValue || obj.ReadLocalValue(PlacementProperty) != DependencyProperty.UnsetValue;


    // 初始化帮助系统
    public static void Initialize(Window window)
    {
        // 获取帮助层
        var helpLayer = window.FindName("HelpLayer") as Canvas;// FindChild<Canvas>(window, "HelpLayer");
        if (helpLayer is null)
            return;

        helpLayer.PreviewMouseDown += (s, e) =>
        {
            // 点击帮助层时隐藏
            helpLayer.Visibility = Visibility.Collapsed;
            e.Handled = true;
        };

        window.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.F1)
            {
                ShowAllHelp(window);
                e.Handled = true;
            }
            else
            {
                // 获取帮助层
                var helpLayer = FindChild<Canvas>(window, "HelpLayer");
                if (helpLayer is not null)
                    helpLayer.Visibility = Visibility.Collapsed;
            }
        };


    }

    // 显示所有帮助内容
    private static void ShowAllHelp(Window window)
    {
        // 获取帮助层
        var helpLayer = FindChild<Canvas>(window, "HelpLayer");
        if (helpLayer == null) return;

        // 清除旧的帮助内容
        helpLayer.Children.Clear();

        // 背景
        var border = new Border { Background = Brushes.White, Opacity = 0.8, Width = window.ActualWidth, Height = window.ActualHeight };
        helpLayer.Children.Add(border);


        // 查找所有设置了Help属性的可见元素
        var helpElements = new List<FrameworkElement>();
        FindHelpElements(window, helpElements);

        if (helpElements.Count == 0)
        {
            // 如果没有帮助内容，直接返回
            helpLayer.Visibility = Visibility.Collapsed;
            return;
        }

        // 显示帮助层
        helpLayer.Visibility = Visibility.Visible;


        // 为每个元素创建帮助卡片
        foreach (var element in helpElements)
        {
            if (!element.IsVisible) continue;

            var helpTemplate = GetHelp(element);
            if (helpTemplate == null && element.ToolTip is null) continue;

            // 获取位置设置
            var placement = GetPlacement(element);
            var offset = GetOffset(element);

            // 计算元素在窗口中的位置 
            try
            {
                // 获取元素在窗口中的位置和尺寸
                var elementBounds = element.TransformToAncestor(window)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));


                // 画边框
                var bd = new Border { Width = elementBounds.Width, Height = elementBounds.Height, BorderBrush = Brushes.Orange, BorderThickness = new Thickness(1) };
                helpLayer.Children.Add(bd);
                Canvas.SetLeft(bd, elementBounds.Left);
                Canvas.SetTop(bd, elementBounds.Top);



                // 创建帮助卡片
                UIElement helpCard = helpTemplate is not null ? new ContentControl
                {
                    ContentTemplate = helpTemplate,
                    //Style = window.Resources["HelpCardStyle"] as Style,

                } : new ContentControl { Content = element.ToolTip };

                helpLayer.Children.Add(helpCard);


                // 强制布局更新以获取实际尺寸
                helpCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                helpCard.Arrange(new Rect(0, 0, helpCard.DesiredSize.Width, helpCard.DesiredSize.Height));

                double helpCardWidth = helpCard.DesiredSize.Width;
                double helpCardHeight = helpCard.DesiredSize.Height;

                // 计算位置
                var position = CalculatePosition(elementBounds, placement, offset, helpCardWidth, helpCardHeight);


                // 设置卡片位置
                Canvas.SetLeft(helpCard, position.X);
                Canvas.SetTop(helpCard, position.Y);
            }
            catch
            {
                continue; // 如果转换失败则跳过
            }

        }

        helpLayer.InvalidateVisual();
    }

    // 递归查找所有设置了Help属性的元素
    private static void FindHelpElements(DependencyObject parent, List<FrameworkElement> results)
    {
        if (parent == null) return;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            // 如果是FrameworkElement且设置了Help属性
            if (child is FrameworkElement element && HasHelp(element))
            {
                results.Add(element);
            }

            // 递归查找子元素
            FindHelpElements(child, results);
        }
    }

    // 查找子元素
    private static T FindChild<T>(DependencyObject parent, string childName)
        where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T result && (child as FrameworkElement)?.Name == childName)
                return result;

            var foundChild = FindChild<T>(child, childName);
            if (foundChild != null)
                return foundChild;
        }
        return null;
    }


    // 计算帮助卡片位置
    private static Point CalculatePosition(Rect elementBounds, Placement placement, Point offset, double helpCardWidth, double helpCardHeight)
    {
        double x = 0;
        double y = 0;

        switch (placement)
        {
            case Placement.Left:
                x = elementBounds.Left - helpCardWidth + offset.X;
                y = elementBounds.Top + (elementBounds.Height / 2) - (helpCardHeight / 2) + offset.Y;
                break;

            case Placement.Right:
                x = elementBounds.Right + offset.X;
                y = elementBounds.Top + (elementBounds.Height / 2) - (helpCardHeight / 2) + offset.Y;
                break;

            case Placement.Top:
                x = elementBounds.Left + (elementBounds.Width / 2) - (helpCardWidth / 2) + offset.X;
                y = elementBounds.Top - helpCardHeight + offset.Y;
                break;

            case Placement.Bottom:
                x = elementBounds.Left + (elementBounds.Width / 2) - (helpCardWidth / 2) + offset.X;
                y = elementBounds.Bottom + offset.Y;
                break;

            case Placement.LeftTop:
                x = elementBounds.Left;
                y = elementBounds.Top;
                break;

            case Placement.RightTop:
                x = elementBounds.Right;
                y = elementBounds.Top;
                break;

            case Placement.LeftBottom:
                x = elementBounds.Left;
                y = elementBounds.Bottom;
                break;

            case Placement.RightBottom:
                x = elementBounds.Right;
                y = elementBounds.Bottom;
                break;

            case Placement.Center:
                x = elementBounds.Left + elementBounds.Width / 2;
                y = elementBounds.Top + elementBounds.Height / 2;
                break;
        }

        // 应用偏移
        x += offset.X;
        y += offset.Y;

        return new Point(x, y);
    }
}


//public class HelpTextExtension : MarkupExtension
//{
//    public string? Text { get; set; }

//    public HelpTextExtension() { }

//    public HelpTextExtension(string text)
//    {
//        Text = text;
//    }

//    public override object ProvideValue(IServiceProvider serviceProvider)
//    {
//        var template = new DataTemplate();
//        var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
//        textBlockFactory.SetValue(TextBlock.TextProperty, Text);
//        template.VisualTree = textBlockFactory;
//        return template;
//    }
//}

public class StringToDataTemplateConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            // 创建DataTemplate
            var template = new DataTemplate();
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetValue(TextBlock.TextProperty, text);
            template.VisualTree = textBlockFactory;
            return template;
        }
        return base.ConvertFrom(context, culture, value);
    }
}