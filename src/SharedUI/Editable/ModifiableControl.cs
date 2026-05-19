using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FMO.Shared;

/// <summary>
/// 支持只读/编辑态切换、自定义 Header、状态驱动的可配置属性容器
/// 绑定目标：ModifiableViewModel&lt;TValue&gt; 或 ModifiableViewModel&lt;TValue, TDisplay&gt;
/// </summary>
public class ModifiableControl : HeaderedContentControl
{
    static ModifiableControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ModifiableControl), new FrameworkPropertyMetadata(typeof(ModifiableControl)));
    }

    // 🔑 绑定 ViewModel（IValueModifier）
    public IValueModifier Modifier
    {
        get => (IValueModifier)GetValue(ModifierProperty);
        set => SetValue(ModifierProperty, value);
    }
    public static readonly DependencyProperty ModifierProperty =
        DependencyProperty.Register(nameof(Modifier), typeof(IValueModifier), typeof(ModifiableControl),
            new FrameworkPropertyMetadata(null));

    // 🔑 只读模式（控制查看/编辑态切换）
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ModifiableControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender| FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    // 🔑 编辑态专用模板（只读态使用默认 ContentTemplate）
    public DataTemplate EditTemplate
    {
        get => (DataTemplate)GetValue(EditTemplateProperty);
        set => SetValue(EditTemplateProperty, value);
    }
    public static readonly DependencyProperty EditTemplateProperty =
        DependencyProperty.Register(nameof(EditTemplate), typeof(DataTemplate), typeof(ModifiableControl),
            new FrameworkPropertyMetadata(null));

 
}