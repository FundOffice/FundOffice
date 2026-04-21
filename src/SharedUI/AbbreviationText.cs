using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FMO.Shared;

/// <summary>
/// 按照步骤 1a 或 1b 操作，然后执行步骤 2 以在 XAML 文件中使用此自定义控件。
///
/// 步骤 1a) 在当前项目中存在的 XAML 文件中使用该自定义控件。
/// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
/// 元素中:
///
///     xmlns:MyNamespace="clr-namespace:FMO.Shared"
///
///
/// 步骤 1b) 在其他项目中存在的 XAML 文件中使用该自定义控件。
/// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
/// 元素中:
///
///     xmlns:MyNamespace="clr-namespace:FMO.Shared;assembly=SharedUI"
///
/// 您还需要添加一个从 XAML 文件所在的项目到此项目的项目引用，
/// 并重新生成以避免编译错误:
///
///     在解决方案资源管理器中右击目标项目，然后依次单击
///     “添加引用”->“项目”->[浏览查找并选择此项目]
///
///
/// 步骤 2)
/// 继续操作并在 XAML 文件中使用控件。
///
///     <MyNamespace:AbbreviationText/>
///
/// </summary>
public class AbbreviationText : Control
{
    static AbbreviationText()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(AbbreviationText), new FrameworkPropertyMetadata(typeof(AbbreviationText)));
    }

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(AbbreviationText), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsArrange));


    

    protected override Size MeasureOverride(Size constraint)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return new Size(0, 0);

        var fontFamily = FontFamily;
        var fontStyle = FontStyle;
        var fontWeight = FontWeight;
        var fontSize = FontSize;
        var pad = Padding;

        // 获取当前控件的 DPI 信息
        var dpi = VisualTreeHelper.GetDpi(this);

        // 创建 FormattedText 对象进行文本测量
        var formattedText = new FormattedText(
            Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal),
            fontSize,
            Foreground,
            dpi.PixelsPerDip);

        double desiredHeight = formattedText.Height + pad.Top + pad.Bottom;

        // 考虑约束条件
        double availableWidth = constraint.Width;
        if (!double.IsInfinity(availableWidth))
        {
            // 减去内边距后的可用宽度
            availableWidth = Math.Max(0, availableWidth - pad.Left - pad.Right);

            // 设置最大宽度，使文本能够换行
            formattedText.MaxTextWidth = availableWidth;
        }

        // 计算所需大小，包括内边距
        double desiredWidth = formattedText.Width + pad.Left + pad.Right;

        // 确保不超过约束的最大值（如果不是无限大）
        if (!double.IsInfinity(constraint.Width))
            desiredWidth = Math.Min(desiredWidth, constraint.Width);

        if (!double.IsInfinity(constraint.Height))
            desiredHeight = Math.Min(desiredHeight, constraint.Height);


        return new Size(desiredWidth, desiredHeight);
    }


    protected override void OnRender(DrawingContext drawingContext)
    {
        if (string.IsNullOrWhiteSpace(Text)) return;

        var text = Text;
        var fontFamily = FontFamily;
        var fontStyle = FontStyle;
        var fontWeight = FontWeight;
        var fontSize = FontSize;
        var w = RenderSize.Width;
        var h = RenderSize.Height;

        // 获取当前控件的 DPI 信息
        var dpi = VisualTreeHelper.GetDpi(this);

        // 初始文本测量
        var originalFormattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal),
            fontSize,
            Foreground,
            dpi.PixelsPerDip);

        var pad = Padding;
        var rc = new Rect(pad.Left, pad.Top, w - pad.Left - pad.Right, h - pad.Top - pad.Bottom);


        // 检查文本是否需要截断
        FormattedText formattedText;
        string displayText = text;

        if (originalFormattedText.Width > rc.Width + 1)
        {
            const string ellipsis = "...";

            // 估算可显示的字符数
            double charsPerPixel = text.Length / originalFormattedText.Width;
            int availableChars = Math.Max(0, (int)(rc.Width * charsPerPixel) - ellipsis.Length);

            if (availableChars > 0)
            {
                // 计算前后保留的字符数
                int halfLength = availableChars / 2;
                if (halfLength * 2 + ellipsis.Length > text.Length)
                    halfLength = (text.Length - ellipsis.Length) / 2;

                if (halfLength > 0)
                {
                    // 创建中间省略的文本
                    displayText = text.Substring(0, halfLength) + ellipsis +
                                  text.Substring(text.Length - halfLength);
                }
            }

            // 创建最终要显示的文本
            formattedText = new FormattedText(
                displayText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily, fontStyle, fontWeight, FontStretches.Normal),
                fontSize,
                Foreground,
                dpi.PixelsPerDip);
        }
        else
        {
            formattedText = originalFormattedText;
        }

        // 计算文本绘制位置（考虑 HorizontalContentAlignment 和 VerticalContentAlignment）
        double x = rc.Left;
        double y = rc.Top;

        switch (HorizontalContentAlignment)
        {
            case HorizontalAlignment.Center:
                x += (rc.Width - formattedText.Width) / 2;
                break;
            case HorizontalAlignment.Right:
                x += rc.Width - formattedText.Width;
                break;
        }

        switch (VerticalContentAlignment)
        {
            case VerticalAlignment.Center:
                y += (rc.Height - formattedText.Height) / 2;
                break;
            case VerticalAlignment.Bottom:
                y += rc.Height - formattedText.Height;
                break;
        }

        // 绘制文本
        drawingContext.DrawText(formattedText, new Point(x, y));
    }
}
