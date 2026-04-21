using HandyControl.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace FMO.Shared;

public static class FilterColumn
{
    // 列头文本
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.RegisterAttached(
            "Header",
            typeof(string),
            typeof(FilterColumn),
            new PropertyMetadata(null));

    public static string GetHeader(DependencyObject d) => (string)d.GetValue(HeaderProperty);
    public static void SetHeader(DependencyObject d, string value) => d.SetValue(HeaderProperty, value);

    // GridFilter 数据源（现在是 GridFilter，非泛型）
    public static readonly DependencyProperty FilterSourceProperty =
        DependencyProperty.RegisterAttached(
            "FilterSource",
            typeof(GridFilter),
            typeof(FilterColumn),
            new PropertyMetadata(null, OnAnyChanged));

    public static GridFilter GetFilterSource(DependencyObject d) => (GridFilter)d.GetValue(FilterSourceProperty);
    public static void SetFilterSource(DependencyObject d, GridFilter value) => d.SetValue(FilterSourceProperty, value);

    // Popup 内容模板
    public static readonly DependencyProperty PopupContentTemplateProperty =
        DependencyProperty.RegisterAttached(
            "PopupContentTemplate",
            typeof(DataTemplate),
            typeof(FilterColumn),
            new PropertyMetadata(null));

    public static DataTemplate GetPopupContentTemplate(DependencyObject d) => (DataTemplate)d.GetValue(PopupContentTemplateProperty);
    public static void SetPopupContentTemplate(DependencyObject d, DataTemplate value) => d.SetValue(PopupContentTemplateProperty, value);

    // 防止重复应用
    private static readonly DependencyProperty AppliedProperty =
        DependencyProperty.RegisterAttached("Applied", typeof(bool), typeof(FilterColumn), new PropertyMetadata(false));

    private static bool GetApplied(DependencyObject d) => (bool)d.GetValue(AppliedProperty);
    private static void SetApplied(DependencyObject d, bool value) => d.SetValue(AppliedProperty, value);

    private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataGridTemplateColumn column && !GetApplied(column))
        {
            SetApplied(column, true);
            ApplyTemplate(column);
        }
    }

    private static void ApplyTemplate(DataGridTemplateColumn column)
    {
        var filter = GetFilterSource(column);

        var headerTemplate = CreateHeaderTemplate(
            GetHeader(column), filter,
            GetPopupContentTemplate(column)
        );
        column.HeaderTemplate = headerTemplate;

        // 设置默认单元格模板（可由外部覆盖）
        var cellTemplate = CreateCellTemplate();
        column.CellTemplate = cellTemplate;
    }

    private static DataTemplate CreateHeaderTemplate(string headerText, GridFilter filter, DataTemplate popupContentTemplate)
    {
        var template = new DataTemplate();

        // <ContentControl Content="{Binding}" /> → 绑定到 GridFilter
        var contentControl = new FrameworkElementFactory(typeof(ContentControl));
        contentControl.SetValue(ContentControl.ContentProperty, filter);

        var dockPanel = new FrameworkElementFactory(typeof(DockPanel));

        // 文本
        var textBlock = new FrameworkElementFactory(typeof(TextBlock));
        textBlock.SetValue(TextBlock.TextProperty, headerText);
        dockPanel.AppendChild(textBlock);

        // ToggleButton
        var toggleButton = new FrameworkElementFactory(typeof(ToggleButton));
        toggleButton.SetValue(IconElement.GeometryProperty, Application.Current.FindResource("f.filter") as Geometry);
        toggleButton.SetValue(ToggleButton.BackgroundProperty, Brushes.Transparent);
        toggleButton.SetValue(ToggleButton.BorderThicknessProperty, new Thickness(0));
        toggleButton.SetValue(ToggleButton.StyleProperty, Application.Current.FindResource("ToggleButtonIcon.Small") as Style);
       // toggleButton.SetValue(FrameworkElement.NameProperty, "toggleBtn");
        toggleButton.Name = "toggleBtn";
        toggleButton.SetValue(ToggleButton.ForegroundProperty, new Binding("IsActive") { Source = filter, Converter = new IsAcitveConverter() });
        dockPanel.AppendChild(toggleButton);

        // Popup
        var popup = new FrameworkElementFactory(typeof(Popup));
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetBinding(Popup.IsOpenProperty, new Binding("IsChecked") { ElementName = "toggleBtn" });
        popup.SetBinding(Popup.PlacementTargetProperty, new Binding { ElementName = "toggleBtn" }); // ✅ 关键
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.StaysOpenProperty, false);

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.WhiteSmoke);
        border.SetValue(Border.BorderBrushProperty, Brushes.LightGray);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.PaddingProperty, new Thickness(8));
        border.SetValue(Border.MinWidthProperty, 180.0);
        border.SetValue(Border.MaxHeightProperty, 600.0);

        var popupDock = new FrameworkElementFactory(typeof(DockPanel));

        // 用户自定义内容
        var contentControlInner = new FrameworkElementFactory(typeof(ContentControl));
        contentControlInner.SetValue(ContentControl.ContentProperty, new Binding()); // 继承 GridFilter
        contentControlInner.SetValue(ContentControl.ContentTemplateProperty, popupContentTemplate);

        popupDock.AppendChild(contentControlInner);
        border.AppendChild(popupDock);
        popup.AppendChild(border);

        dockPanel.AppendChild(popup);
         

        // 设置 ContentControl 的模板
        contentControl.SetValue(ContentControl.ContentTemplateProperty, new DataTemplate { VisualTree = dockPanel });
        template.VisualTree = contentControl;

        return template;
    }

    private static DataTemplate CreateCellTemplate()
    {
        var template = new DataTemplate();
        var dockPanel = new FrameworkElementFactory(typeof(DockPanel));
        dockPanel.SetValue(DockPanel.BackgroundProperty, Brushes.Transparent);
        dockPanel.SetValue(DockPanel.LastChildFillProperty, false);

        var textBlock = new FrameworkElementFactory(typeof(CopyableTextBlock));
        textBlock.SetBinding(CopyableTextBlock.TextProperty, new Binding("FundName"));
        dockPanel.AppendChild(textBlock);

        var button = new FrameworkElementFactory(typeof(Button));
        button.SetBinding(ButtonBase.CommandProperty, new Binding("OpenFundCommand"));
        button.SetValue(IconElement.GeometryProperty, Application.Current.FindResource("f.paper-plane") as Geometry);
        button.SetValue(Button.StyleProperty, Application.Current.FindResource("ButtonIcon.Small") as Style);
        button.SetValue(FrameworkElement.NameProperty, "goto");
        button.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        dockPanel.AppendChild(button);

        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible) { TargetName = "goto" });
        template.Triggers.Add(trigger);

        template.VisualTree = dockPanel;
        return template;
    }


    public class IsAcitveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch { true => Brushes.Green ,_=> Brushes.Black };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}