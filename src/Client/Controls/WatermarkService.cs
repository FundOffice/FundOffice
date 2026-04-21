using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FMO;


using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public static class WatermarkService
{
    // 水印文字
    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.RegisterAttached(
            "Watermark",
            typeof(string),
            typeof(WatermarkService),
            new PropertyMetadata(string.Empty, OnWatermarkChanged));

    // 水印颜色
    public static readonly DependencyProperty WatermarkForegroundProperty =
        DependencyProperty.RegisterAttached(
            "WatermarkForeground",
            typeof(Brush),
            typeof(WatermarkService),
            new PropertyMetadata(Brushes.Gray));

    // 保存原始文本换行设置
    private static readonly DependencyProperty OriginalTextWrappingProperty =
        DependencyProperty.RegisterAttached(
            "OriginalTextWrapping",
            typeof(TextWrapping),
            typeof(WatermarkService));

    // 保存原始接受回车设置
    private static readonly DependencyProperty OriginalAcceptsReturnProperty =
        DependencyProperty.RegisterAttached(
            "OriginalAcceptsReturn",
            typeof(bool),
            typeof(WatermarkService));


    public static string GetWatermark(DependencyObject obj) => (string)obj.GetValue(WatermarkProperty);
    public static void SetWatermark(DependencyObject obj, string value) => obj.SetValue(WatermarkProperty, value);

    public static Brush GetWatermarkForeground(DependencyObject obj) => (Brush)obj.GetValue(WatermarkForegroundProperty);
    public static void SetWatermarkForeground(DependencyObject obj, Brush value) => obj.SetValue(WatermarkForegroundProperty, value);

    
    private static bool IsWatermarkActive(TextBox textBox) =>
        textBox.Text == GetWatermark(textBox);

    private static void SetWatermarkIfNeeded(TextBox textBox)
    {
        if (string.IsNullOrEmpty(textBox.Text))
        {
            ShowWatermark(textBox);
        }
    }

 

    private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            // ... 保留原有事件处理 ...

            // 新增初始化处理
            textBox.Loaded += (s, ev) =>
            {
                // 保存原始设置
                textBox.SetValue(OriginalTextWrappingProperty, textBox.TextWrapping);
                textBox.SetValue(OriginalAcceptsReturnProperty, textBox.AcceptsReturn);
                SetWatermarkIfNeeded(textBox);
            };
        }
    }

    private static void ShowWatermark(TextBox textBox)
    {
        var watermark = GetWatermark(textBox);

        // 检测是否需要多行模式
        bool isMultiLine = watermark.Contains("\n");

        // 保存原始设置
        textBox.SetValue(OriginalTextWrappingProperty, textBox.TextWrapping);
        textBox.SetValue(OriginalAcceptsReturnProperty, textBox.AcceptsReturn);

        // 临时启用多行支持
        if (isMultiLine)
        {
            textBox.TextWrapping = TextWrapping.Wrap;
            textBox.AcceptsReturn = true;
            textBox.VerticalContentAlignment = VerticalAlignment.Top;
        }

        textBox.Text = watermark;
        textBox.Foreground = GetWatermarkForeground(textBox);
    }

    private static void ClearWatermark(TextBox textBox)
    {
        // 恢复原始设置
        textBox.TextWrapping = (TextWrapping)textBox.GetValue(OriginalTextWrappingProperty);
        textBox.AcceptsReturn = (bool)textBox.GetValue(OriginalAcceptsReturnProperty);

        textBox.Clear();
        textBox.Foreground = Brushes.Black;
    }

}