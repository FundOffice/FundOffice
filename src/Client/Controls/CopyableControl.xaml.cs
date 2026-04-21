using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// CopyableControl.xaml 的交互逻辑
/// </summary>
public partial class CopyableControl : UserControl
{





    public bool CanCopy
    {
        get { return (bool)GetValue(CanCopyProperty); }
        private set { SetValue(CanCopyProperty, value); }
    }

    // Using a DependencyProperty as the backing store for CanCopy.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CanCopyProperty =
        DependencyProperty.Register("CanCopy", typeof(bool), typeof(ContentControl), new PropertyMetadata(true));




    public CopyableControl()
    {
        InitializeComponent();

        OnContentChanged();

        DependencyPropertyDescriptor d = DependencyPropertyDescriptor.FromProperty(CopyableControl.ContentProperty, typeof(CopyableControl));
        d.AddValueChanged(this, (s, e) => OnContentChanged());
    }

    private void btn_Click(object sender, RoutedEventArgs e)
    {
        switch (Content)
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
    }

    private void OnContentChanged()
    {
        switch (Content)
        {
            case TextBlock tb:
                DependencyPropertyDescriptor d = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
                d.AddValueChanged(tb, (s, e) => SetCanCopy(tb.Text));
                SetCanCopy(tb.Text);
                break;

            case TextBox tb:
                d = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBox));
                d.AddValueChanged(tb, (s, e) => SetCanCopy(tb.Text));
                SetCanCopy(tb.Text);
                break;

            case Label lb:
                d = DependencyPropertyDescriptor.FromProperty(Label.ContentProperty, typeof(Label));
                d.AddValueChanged(lb, (s, e) => SetCanCopy(lb.Content?.ToString()));
                SetCanCopy(lb.Content?.ToString());
                break;


        }
    }

    private void SetCanCopy(string? str)
    {
        CanCopy = !string.IsNullOrWhiteSpace(str) && str != "-";
    }

}
