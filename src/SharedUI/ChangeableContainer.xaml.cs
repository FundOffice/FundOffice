using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FMO.Shared;

/// <summary>
/// EditableContainer.xaml 的交互逻辑
/// </summary>
public partial class ChangeableContainer : UserControl
{
    private ContentPresenter? _content;

    public IPropertyModifier Binding
    {
        get { return (IPropertyModifier)GetValue(BindingProperty); }
        set { SetValue(BindingProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Binding.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty BindingProperty =
        DependencyProperty.Register("Binding", typeof(IPropertyModifier), typeof(ChangeableContainer), new PropertyMetadata(null));




    public ChangeableEntityViewModel Entity
    {
        get { return (ChangeableEntityViewModel)GetValue(EntityProperty); }
        set { SetValue(EntityProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Entity.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty EntityProperty =
        DependencyProperty.Register("Entity", typeof(ChangeableEntityViewModel), typeof(ChangeableContainer), new PropertyMetadata(null));




    //public object Header
    //{
    //    get { return (object)GetValue(HeaderProperty); }
    //    set { SetValue(HeaderProperty, value); }
    //}

    //// Using a DependencyProperty as the backing store for Header.  This enables animation, styling, binding, etc...
    //public static readonly DependencyProperty HeaderProperty =
    //    DependencyProperty.Register("Header", typeof(object), typeof(ChangeableContainer), new PropertyMetadata(null));




    //public Dock StripPlacement
    //{
    //    get { return (Dock)GetValue(StripPlacementProperty); }
    //    set { SetValue(StripPlacementProperty, value); }
    //}

    //// Using a DependencyProperty as the backing store for StripPlacement.  This enables animation, styling, binding, etc...
    //public static readonly DependencyProperty StripPlacementProperty =
    //    DependencyProperty.Register("StripPlacement", typeof(Dock), typeof(ChangeableContainer), new PropertyMetadata(Dock.Left));




    public ControlTemplate EditTemplate
    {
        get { return (ControlTemplate)GetValue(EditTemplateProperty); }
        set { SetValue(EditTemplateProperty, value); }
    }

    // Using a DependencyProperty as the backing store for EditTemplate.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty EditTemplateProperty =
        DependencyProperty.Register("EditTemplate", typeof(ControlTemplate), typeof(ChangeableContainer), new PropertyMetadata(null));





    //Debouncer _debouncer;

    public ChangeableContainer()
    {
        InitializeComponent();

        //_debouncer = new Debouncer(() => Binding?.Refresh());
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _content = Template.FindName("PART_Content", this) as ContentPresenter;
    }



    //protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    //{
    //    base.OnPreviewMouseDown(e);

    //    if(e.Source.GetType() == typeof(ChangeableContainer))
    //        _debouncer.Invoke();
    //}

    //protected override void OnPreviewKeyDown(KeyEventArgs e)
    //{
    //    base.OnPreviewKeyDown(e);

    //    if (e.Source.GetType() == typeof(ChangeableContainer))
    //        _debouncer.Invoke();
    //}

}
