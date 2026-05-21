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



public class FactorModifiableControl : HeaderedContentControl
{
    static FactorModifiableControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FactorModifiableControl), new FrameworkPropertyMetadata(typeof(FactorModifiableControl)));
    }


 

    public IFactorModifier Modifier
    {
        get => (IFactorModifier)GetValue(ModifierProperty);
        set => SetValue(ModifierProperty, value);
    }
    public static readonly DependencyProperty ModifierProperty =
        DependencyProperty.Register(nameof(Modifier), typeof(IFactorModifier), typeof(FactorModifiableControl),
            new FrameworkPropertyMetadata(null));


    public DataTemplate EditTemplate
    {
        get { return (DataTemplate)GetValue(EditTemplateProperty); }
        set { SetValue(EditTemplateProperty, value); }
    }

    // Using a DependencyProperty as the backing store for EditTemplate.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty EditTemplateProperty =
        DependencyProperty.Register("EditTemplate", typeof(DataTemplate), typeof(FactorModifiableControl), new PropertyMetadata(null));

 

    public bool IsReadOnly
    {
        get { return (bool)GetValue(IsReadOnlyProperty); }
        set { SetValue(IsReadOnlyProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsReadOnly.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(FactorModifiableControl), new PropertyMetadata(false));

}