using FMO.Models;
using FMO.Utilities;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace FMO.Shared;

/// <summary>
/// 全局隐私遮罩服务（按F9开关，自动处理所有文本/图片/日期选择器）
/// </summary>
public static class MaskService
{
    private static Dictionary<string, string> _maskMap = [];
    // 常用姓氏（单姓 + 复姓）
    private static readonly List<string> _lNames = new List<string>
    {
        "王", "李", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴",
        "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗",
        "欧阳", "上官", "司马", "东方", "夏侯", "诸葛", "闻人", "拓跋"
    };

    // 常用男名用字
    private static readonly List<string> _fNames = new List<string>
    {
        "伟", "强", "磊", "军", "洋", "勇", "杰", "波", "明", "亮",
        "超", "浩", "凯", "健", "俊", "飞", "鹏", "峰", "旭", "晨" ,
        "芳", "娜", "敏", "静", "颖", "琳", "倩", "婷", "丽", "娟",
        "艳", "梅", "雪", "玲", "佳", "怡", "梦", "琪", "雨", "欣"
    };

    #region 依赖属性
    // 启用遮罩标记
    public static readonly DependencyProperty IsMaskProperty =
        DependencyProperty.RegisterAttached("IsMask", typeof(bool), typeof(MaskService),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnIsMaskChanged));

    public static bool GetIsMask(DependencyObject obj) => (bool)obj.GetValue(IsMaskProperty);
    public static void SetIsMask(DependencyObject obj, bool value) => obj.SetValue(IsMaskProperty, value);

    // 模糊半径
    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.RegisterAttached("BlurRadius", typeof(double), typeof(MaskService),
            new PropertyMetadata(15.0));

    public static double GetBlurRadius(DependencyObject obj) => (double)obj.GetValue(BlurRadiusProperty);
    public static void SetBlurRadius(DependencyObject obj, double value) => obj.SetValue(BlurRadiusProperty, value);

    // 存储原始文本
    private static readonly DependencyProperty OriginalTextProperty =
        DependencyProperty.RegisterAttached("OriginalText", typeof(string), typeof(MaskService),
            new PropertyMetadata(default(string)));

    private static string GetOriginalText(DependencyObject obj) => (string)obj.GetValue(OriginalTextProperty);
    private static void SetOriginalText(DependencyObject obj, string value) => obj.SetValue(OriginalTextProperty, value);

    // 存储原始效果
    private static readonly DependencyProperty OriginalEffectProperty =
        DependencyProperty.RegisterAttached("OriginalEffect", typeof(Effect), typeof(MaskService),
            new PropertyMetadata(default(Effect)));

    private static Effect GetOriginalEffect(DependencyObject obj) => (Effect)obj.GetValue(OriginalEffectProperty);
    private static void SetOriginalEffect(DependencyObject obj, Effect value) => obj.SetValue(OriginalEffectProperty, value);

    // 标记：是否正在更新遮罩文本（防止无限循环）
    private static readonly DependencyProperty IsUpdatingMaskProperty =
        DependencyProperty.RegisterAttached("IsUpdatingMask", typeof(bool), typeof(MaskService),
            new PropertyMetadata(false));

    private static bool GetIsUpdatingMask(DependencyObject obj) => (bool)obj.GetValue(IsUpdatingMaskProperty);
    private static void SetIsUpdatingMask(DependencyObject obj, bool value) => obj.SetValue(IsUpdatingMaskProperty, value);
    #endregion

    #region 全局状态
    /// <summary>
    /// 全局遮罩启用状态
    /// </summary>
    private static bool _isGlobalMaskEnabled;

    /// <summary>
    /// 文本控件类型匹配
    /// </summary>
    private static readonly Regex _textControlRegex = new(
        @"^(AbbreviationText|CopyableTextBlock|TextBox|Label|TextBlock|DateTimePicker|DatePicker)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    #endregion

    static MaskService()
    {
        // 全局监听控件加载（解决动态加载控件不生效）
        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnFrameworkElementLoaded),
            handledEventsToo: true);

        // 全局F9快捷键
        EventManager.RegisterClassHandler(
            typeof(Window),
            Window.KeyDownEvent,
            new KeyEventHandler(OnGlobalKeyDown));

        GenerateMap();
    }

    private static void GenerateMap()
    {
        using var db = DbHelper.Base();
        var m = db.GetCollection<Manager>().Query().First();
        if (m is not null)
            _maskMap.TryAdd(m.Name, "暴富基金公司");

        int fid = 1;
        var cus = db.GetCollection<Investor>().Query().Select(x => new { x.Name, x.EntityType }).ToArray();
        foreach (var c in cus)
            _maskMap.TryAdd(c.Name, c.EntityType switch
            {
                EntityType.Natural => $"{_lNames[Random.Shared.Next(_lNames.Count)]}{_fNames[Random.Shared.Next(_fNames.Count)]}",
                EntityType.Product => $"暴富{fid++}号",
                EntityType.Institution => $"演示机构{fid++}",
                _ => $"演示客户"
            });

        var pr = db.GetCollection<Participant>().Query().Select(x => new { x.Name, x.Email, x.Phone, x.CertCode, x.Identity.Id }).ToArray();
        foreach (var p in pr)
        {
            _maskMap.TryAdd(p.Name ?? "", $"{_lNames[Random.Shared.Next(_lNames.Count)]}{_fNames[Random.Shared.Next(_fNames.Count)]}");
            _maskMap.TryAdd(p.Email ?? "", "xxx@163.com");
            _maskMap.TryAdd(p.Phone ?? "", "13912345678");
            _maskMap.TryAdd(p.CertCode ?? "", "A12345678");
            _maskMap.TryAdd(p.Id ?? "", "*************1234");
            if (p.Id?.Length == 18)
                _maskMap.TryAdd($"{p.Id[..6]}******{p.Id[^6..]}", "*************1234");
        }

        var funds = db.GetCollection<Fund>().Query().Select(x => new { x.Name, x.ShortName }).ToArray();
        foreach (var f in funds)
        {
            _maskMap.TryAdd(f.Name, $"暴富{fid++}号");
            _maskMap.TryAdd(f.ShortName ?? "", $"暴富{fid++}号");
        }

    }











    #region 核心：动态控件自动生效 + 绑定更新监听
    private static void OnFrameworkElementLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isGlobalMaskEnabled || sender is not FrameworkElement element)
            return;

        // 延迟执行，确保可视化树完全构建
        element.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (GetIsMask(element))
            {
                ApplyMask(element);
            }
            // 递归处理子控件
            ProcessVisualTreeChildren(element, true);
        });
    }

    /// <summary>
    /// 为文本控件注册文本变更监听（绑定更新时自动重新遮罩）
    /// </summary>
    private static void RegisterTextChangedListener(UIElement element)
    {
        if (element is TextBlock tb)
        {
            // TextBlock 监听依赖属性变更
            var desc = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            desc.AddValueChanged(tb, OnTextUpdated);
            return;
        }

        if (element is Control ctrl && _textControlRegex.IsMatch(ctrl.GetType().Name))
        {
            var textProp = ctrl.GetType().GetProperty("Text");
            if (textProp == null) return;

            // 监听 TextProperty 变更
            var dp = DependencyPropertyDescriptor.FromName(textProp.Name, ctrl.GetType(), ctrl.GetType());
            dp?.AddValueChanged(ctrl, OnTextUpdated);
        }

        if (element is ContentControl cc)
        {
            // 监听 Content 变更
            var desc = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));
            desc.AddValueChanged(cc, OnTextUpdated);
        }
    }

    /// <summary>
    /// 文本/内容变更后自动重新遮罩
    /// </summary>
    private static void OnTextUpdated(object? sender, EventArgs e)
    {
        if (!_isGlobalMaskEnabled || sender is not UIElement element || !GetIsMask(element))
            return;

        // 防止递归死循环
        if (GetIsUpdatingMask(element))
            return;

        try
        {
            SetIsUpdatingMask(element, true);

            var original = GetOriginalText(element);
            if (string.IsNullOrEmpty(original))
            {
                // 首次变更：保存新的原始值并覆盖
                SaveNewOriginalAndApplyMask(element);
            }
            else
            {
                // 原始值已存在：直接覆盖为脱敏文本
                ApplyMaskDirectly(element, original);
            }
        }
        finally
        {
            SetIsUpdatingMask(element, false);
        }
    }
    #endregion

    #region 手动标记变更
    private static void OnIsMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;
        var enable = (bool)e.NewValue;

        if (_isGlobalMaskEnabled && enable)
            ApplyMask(element);
        else
            RestoreMask(element);
    }
    #endregion

    #region F9 全局开关
    private static void OnGlobalKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F9 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            _isGlobalMaskEnabled = !_isGlobalMaskEnabled;

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
            {
                RefreshAllMasks();
                UpdateStatusBar(sender as Window);
            });
        }
    }

    /// <summary>
    /// 刷新所有窗口+弹出层
    /// </summary>
    private static void RefreshAllMasks()
    {
        foreach (Window window in Application.Current.Windows)
        {
            if (!window.IsLoaded) continue;
            ProcessVisualTree(window, _isGlobalMaskEnabled);
            ProcessAllPopups(window, _isGlobalMaskEnabled);
        }
    }
    #endregion

    #region 可视化树遍历（通用）
    private static void ProcessVisualTree(DependencyObject parent, bool enableMask)
    {
        if (parent == null) return;

        if (parent is UIElement elem && GetIsMask(elem))
        {
            if (enableMask) ApplyMask(elem);
            else RestoreMask(elem);
        }

        ProcessVisualTreeChildren(parent, enableMask);
    }

    /// <summary>
    /// 只遍历子元素（避免重复处理自身）
    /// </summary>
    private static void ProcessVisualTreeChildren(DependencyObject parent, bool enableMask)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            ProcessVisualTree(VisualTreeHelper.GetChild(parent, i), enableMask);
        }

        // 兼容 Decorator (Border/Viewbox)
        if (count == 0 && parent is Decorator dec && dec.Child != null)
        {
            ProcessVisualTree(dec.Child, enableMask);
        }
    }

    /// <summary>
    /// 处理弹出层 Popup
    /// </summary>
    private static void ProcessAllPopups(DependencyObject parent, bool enableMask)
    {
        if (parent == null) return;

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is Popup { IsOpen: true, Child: { } popupChild })
            {
                ProcessVisualTree(popupChild, enableMask);
                ProcessAllPopups(popupChild, enableMask);
            }

            ProcessAllPopups(child, enableMask);
        }
    }
    #endregion

    #region 应用遮罩
    private static void ApplyMask(UIElement element)
    {
        if (element == null) return;

        // 已处理过，直接跳过
        if (!string.IsNullOrEmpty(GetOriginalText(element)) || GetOriginalEffect(element) != null)
            return;

        // 注册文本变更监听（修复绑定更新不生效）
        RegisterTextChangedListener(element);

        // 优先处理 DateTimePicker / DatePicker
        if (HandleDateTimePickerMask(element))
            return;

        // 文本处理
        ApplyTextMask(element);

        // 图片模糊
        ApplyImageBlur(element);
    }

    /// <summary>
    /// 保存新的原始值并重新遮罩（用于绑定更新）
    /// </summary>
    private static void SaveNewOriginalAndApplyMask(UIElement element)
    {
        string newOriginal = string.Empty;

        if (element is TextBlock tb)
            newOriginal = tb.Text;
        else if (element is Control ctrl && _textControlRegex.IsMatch(ctrl.GetType().Name))
        {
            var textProp = ctrl.GetType().GetProperty("Text");
            newOriginal = textProp?.GetValue(ctrl)?.ToString() ?? "";
        }
        else if (element is ContentControl cc && cc.Content is string s)
            newOriginal = s;

        if (string.IsNullOrEmpty(newOriginal)) return;

        SetOriginalText(element, newOriginal);
        ApplyMaskDirectly(element, newOriginal);
    }

    /// <summary>
    /// 直接应用遮罩（不修改原始值）
    /// </summary>
    private static void ApplyMaskDirectly(UIElement element, string originalText)
    {
        string maskText = ToNewText(originalText);

        if (element is TextBlock tb)
            tb.Text = maskText;
        else if (element is Control ctrl && _textControlRegex.IsMatch(ctrl.GetType().Name))
        {
            var textProp = ctrl.GetType().GetProperty("Text");
            textProp?.SetValue(ctrl, maskText);
        }
        else if (element is ContentControl cc)
            cc.Content = maskText;
    }

    /// <summary>
    /// 专门处理日期选择器：遮罩时固定显示 2000-01-01
    /// </summary>
    private static bool HandleDateTimePickerMask(UIElement element)
    {
        try
        {
            var type = element.GetType();
            if (!type.Name.Equals("DatePicker", StringComparison.OrdinalIgnoreCase) &&
                !type.Name.Equals("DateTimePicker", StringComparison.OrdinalIgnoreCase))
                return false;

            var textProp = type.GetProperty("Text");
            var valueProp = type.GetProperty("Value");

            if (textProp == null) return false;

            var originalText = textProp.GetValue(element)?.ToString() ?? "";
            SetOriginalText(element, originalText);

            textProp.SetValue(element, "2000-01-01");

            if (valueProp != null && valueProp.PropertyType == typeof(DateTime?))
                valueProp.SetValue(element, new DateTime(2000, 1, 1));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyTextMask(UIElement element)
    {
        // TextBlock
        if (element is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text))
        {
            SetOriginalText(tb, tb.Text);
            tb.Text = ToNewText(tb.Text);
            return;
        }

        // 带Text属性的控件
        if (element is Control ctrl && _textControlRegex.IsMatch(ctrl.GetType().Name))
        {
            var textProp = ctrl.GetType().GetProperty("Text");
            if (textProp is null) return;

            var original = textProp.GetValue(ctrl)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(original)) return;

            SetOriginalText(ctrl, original);
            textProp.SetValue(ctrl, ToNewText(original));
            return;
        }

        // Content 是纯文本的控件（Label/Button等）
        if (element is ContentControl cc && cc.Content is string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            SetOriginalText(cc, content);
            cc.Content = ToNewText(content);
        }
    }

    private static string ToNewText(string? old)
    {
        if (string.IsNullOrWhiteSpace(old)) return "";


        if (_maskMap.TryGetValue(old, out var result))
            return result;
        else return $"演示{old.GetHashCode()}";

        if (DateOnly.TryParse(old, out DateOnly d))
            return "2000-01-01";

        if (Regex.Match(old, @"[\d一二三四五六七八九十]+号") is Match m && m.Success)
            return $"演示产品{m.Value}";

        if (Regex.IsMatch(old, @"S\w{5}"))
            return "SABCDE";

        if (Regex.IsMatch(old, @"^1\d{10}$"))
            return "13999999999";

        if (Regex.IsMatch(old, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return "mail@abc.com";

        if (Regex.IsMatch(old, @"^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            return "www.abc.com";

        if (Regex.IsMatch(old, @"^[\d\*]{17}[\dXx]$"))
            return $"{old[..6]}*************";


        if (int.TryParse(old, null, out var dd))
            return "999";

        return $"演示{old.GetHashCode()}";
    }

    private static void ApplyImageBlur(UIElement element)
    {
        if (element is not Image img) return;

        SetOriginalEffect(img, img.Effect);
        img.Effect = new BlurEffect { Radius = GetBlurRadius(img) };
    }
    #endregion

    #region 恢复原始内容
    private static void RestoreMask(UIElement element)
    {
        if (element == null) return;

        // 恢复日期选择器
        if (HandleDateTimePickerRestore(element))
            return;

        RestoreText(element);
        RestoreImageEffect(element);
    }

    /// <summary>
    /// 恢复日期选择器原始值
    /// </summary>
    private static bool HandleDateTimePickerRestore(UIElement element)
    {
        try
        {
            var type = element.GetType();
            if (!type.Name.Equals("DateTimePicker", StringComparison.OrdinalIgnoreCase) &&
                !type.Name.Equals("DatePicker", StringComparison.OrdinalIgnoreCase))
                return false;

            var original = GetOriginalText(element);
            if (string.IsNullOrEmpty(original)) return false;

            var textProp = type.GetProperty("Text");
            textProp?.SetValue(element, original);

            SetOriginalText(element, string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreText(UIElement element)
    {
        var original = GetOriginalText(element);
        if (string.IsNullOrEmpty(original)) return;

        try
        {
            SetIsUpdatingMask(element, true);

            // TextBlock
            if (element is TextBlock tb)
                tb.Text = original;
            // Text 控件
            else if (element is Control ctrl && _textControlRegex.IsMatch(ctrl.GetType().Name))
            {
                var textProp = ctrl.GetType().GetProperty("Text");
                textProp?.SetValue(ctrl, original);
            }
            // Content 文本
            else if (element is ContentControl cc)
                cc.Content = original;
        }
        finally
        {
            SetIsUpdatingMask(element, false);
            SetOriginalText(element, string.Empty);
        }
    }

    private static void RestoreImageEffect(UIElement element)
    {
        if (element is not Image img) return;

        img.Effect = GetOriginalEffect(img);
        SetOriginalEffect(img, null);
    }
    #endregion

    #region 状态栏提示
    private static void UpdateStatusBar(Window? window)
    {
        if (window == null) return;

        if (window.FindName("statusIndicator") is Border indicator &&
            window.FindName("statusText") is TextBlock txt)
        {
            if (_isGlobalMaskEnabled)
            {
                indicator.Background = Brushes.Red;
                txt.Text = "隐私保护已启用（F9关闭）";
            }
            else
            {
                indicator.Background = Brushes.Green;
                txt.Text = "隐私保护已禁用（F9启用）";
            }
        }
    }
    #endregion
}