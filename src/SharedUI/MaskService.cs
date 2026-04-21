using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace FMO.Shared;

public static class MaskService
{
    // 注册附加属性 IsMask
    public static readonly DependencyProperty IsMaskProperty =
        DependencyProperty.RegisterAttached(
            "IsMask",
            typeof(bool),
            typeof(MaskService),
            new PropertyMetadata(false, OnIsMaskChanged));

    public static bool GetIsMask(DependencyObject obj) =>
        (bool)obj.GetValue(IsMaskProperty);

    public static void SetIsMask(DependencyObject obj, bool value) =>
        obj.SetValue(IsMaskProperty, value);

    // 注册附加属性 BlurRadius
    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.RegisterAttached(
            "BlurRadius",
            typeof(double),
            typeof(MaskService),
            new PropertyMetadata(15.0, OnBlurRadiusChanged));

    public static double GetBlurRadius(DependencyObject obj) =>
        (double)obj.GetValue(BlurRadiusProperty);

    public static void SetBlurRadius(DependencyObject obj, double value) =>
        obj.SetValue(BlurRadiusProperty, value);

    // 内部属性存储原始效果
    private static readonly DependencyProperty OriginalEffectProperty =
        DependencyProperty.RegisterAttached(
            "OriginalEffect",
            typeof(Effect),
            typeof(MaskService));

    private static Effect GetOriginalEffect(DependencyObject obj) =>
        (Effect)obj.GetValue(OriginalEffectProperty);

    private static void SetOriginalEffect(DependencyObject obj, Effect? value) =>
        obj.SetValue(OriginalEffectProperty, value);

    // 全局模糊状态
    private static bool _isGlobalMaskEnabled = false;

    static MaskService()
    {
        // 注册全局键盘事件
        EventManager.RegisterClassHandler(
            typeof(Window),
            Window.KeyDownEvent,
            new KeyEventHandler(HandleKeyDown)
        );

        // 监听所有窗口的Loaded事件，以便处理后续创建的Popup
        EventManager.RegisterClassHandler(
            typeof(Window),
            Window.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded)
        );
    }

    // 窗口加载时注册Popup监测
    private static void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            // 监测窗口中所有元素的加载事件，以捕获动态创建的Popup
            window.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler(OnElementLoaded));
        }
    }

    // 元素加载时检查是否为Popup并注册事件
    private static void OnElementLoaded(object? sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Popup popup)
        {
            // 为每个Popup实例单独注册IsOpen属性变化事件
            DependencyPropertyDescriptor.FromProperty(Popup.IsOpenProperty, typeof(Popup))
                .AddValueChanged(popup, Popup_IsOpenChanged);
        }
    }

    // 处理单个Popup的IsOpen属性变化
    private static void Popup_IsOpenChanged(object? sender, EventArgs e)
    {
        if (sender is Popup popup && popup.IsOpen && _isGlobalMaskEnabled)
        {
            // 延迟处理以确保Popup内容已加载
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (popup.Child != null && popup.IsOpen)
                {
                    ProcessVisualTree(popup.Child, true);
                }
            }));
        }
    }

    // 属性变更回调
    private static void OnIsMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        // 当元素的IsMask属性变化时，如果全局启用则立即应用效果
        if (_isGlobalMaskEnabled && (bool)e.NewValue)
        {
            ApplyBlurEffect(element);
        }
        else if (!(bool)e.NewValue && GetOriginalEffect(element) != null)
        {
            RestoreOriginalEffect(element);
        }

        // 如果是容器元素，监听其布局变化以捕获新添加的子元素
        if (d is Panel panel)
        {
            panel.LayoutUpdated += Panel_LayoutUpdated;
        }
        else if (d is ContentControl contentControl)
        {
            contentControl.LayoutUpdated += ContentControl_LayoutUpdated;
        }
    }

    // 监听ContentControl的布局更新
    private static void ContentControl_LayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is ContentControl contentControl && _isGlobalMaskEnabled)
        {
            if (contentControl.Content is DependencyObject content)
            {
                ProcessVisualTree(content, true);
            }
        }
    }

    // 监听Panel的布局更新
    private static void Panel_LayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is Panel panel && _isGlobalMaskEnabled)
        {
            ProcessVisualTree(panel, true);
        }
    }

    private static void OnBlurRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element && _isGlobalMaskEnabled && GetIsMask(element))
        {
            ApplyBlurEffect(element);
        }
    }

    // 全局按键处理
    private static void HandleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F9 && Keyboard.Modifiers == ModifierKeys.None)
        {
            _isGlobalMaskEnabled = !_isGlobalMaskEnabled;
            ToggleMaskEffects();
            UpdateStatusIndicator(sender as Window);
            e.Handled = true;
        }
    }

    // 切换所有元素的模糊效果
    private static void ToggleMaskEffects()
    {
        foreach (Window window in Application.Current.Windows)
        {
            ProcessVisualTree(window, _isGlobalMaskEnabled);

            // 处理所有打开的Popup
            ProcessAllPopups(window, _isGlobalMaskEnabled);
        }
    }

    // 处理所有Popup
    private static void ProcessAllPopups(DependencyObject parent, bool applyBlur)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is Popup popup && popup.IsOpen)
            {
                if (popup.Child != null)
                {
                    ProcessVisualTree(popup.Child, applyBlur);
                }
            }

            // 递归查找子元素中的Popup
            ProcessAllPopups(child, applyBlur);
        }
    }

    // 递归处理视觉树
    private static void ProcessVisualTree(DependencyObject parent, bool applyBlur)
    {
        if (parent is UIElement element && GetIsMask(element))
        {
            if (applyBlur)
                ApplyBlurEffect(element);
            else
                RestoreOriginalEffect(element);
        }

        // 处理子元素
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            ProcessVisualTree(child, applyBlur);
        }
    }

    // 应用模糊效果
    private static void ApplyBlurEffect(UIElement element)
    {
        if (GetOriginalEffect(element) == null)
        {
            // 保存原始效果
            SetOriginalEffect(element, element.Effect);

            // 应用模糊效果
            double radius = GetBlurRadius(element);
            element.Effect = new BlurEffect { Radius = radius };
        }
        else
        {
            // 更新现有模糊效果
            if (element.Effect is BlurEffect blurEffect)
            {
                blurEffect.Radius = GetBlurRadius(element);
            }
        }
    }

    // 恢复原始效果
    private static void RestoreOriginalEffect(UIElement element)
    {
        var originalEffect = GetOriginalEffect(element);

        element.Effect = originalEffect;
        SetOriginalEffect(element, null);
    }

    // 更新状态指示器
    private static void UpdateStatusIndicator(Window? window)
    {
        if (window == null) return;

        if (window.FindName("statusIndicator") is Border indicator &&
            window.FindName("statusText") is TextBlock statusText)
        {
            if (_isGlobalMaskEnabled)
            {
                indicator.Background = new SolidColorBrush(Colors.Red);
                statusText.Text = "隐私保护已启用 (按 F9 关闭)";
            }
            else
            {
                indicator.Background = new SolidColorBrush(Colors.Green);
                statusText.Text = "隐私保护已禁用 (按 F9 启用)";
            }
        }
    }
}
