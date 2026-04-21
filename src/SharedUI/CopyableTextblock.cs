using System.Windows;
using System.Windows.Controls;

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
///     <MyNamespace:CopyableTextblock/>
///
/// </summary>
public class CopyableTextBlock : Control
{
    static CopyableTextBlock()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CopyableTextBlock), new FrameworkPropertyMetadata(typeof(CopyableTextBlock)));
    }




    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(CopyableTextBlock), new PropertyMetadata(null, ModifyMasked));


    public string MaskedText
    {
        get { return (string)GetValue(MaskedTextProperty); }
        set { SetValue(MaskedTextProperty, value); }
    }

    // Using a DependencyProperty as the backing store for MaskedText.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MaskedTextProperty =
        DependencyProperty.Register("MaskedText", typeof(string), typeof(CopyableTextBlock), new PropertyMetadata(null));




    public bool CanCopy
    {
        get { return (bool)GetValue(CanCopyProperty); }
        set { SetValue(CanCopyProperty, value); }
    }

    // Using a DependencyProperty as the backing store for CanCopy.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CanCopyProperty =
        DependencyProperty.Register("CanCopy", typeof(bool), typeof(CopyableTextBlock), new PropertyMetadata(false));






    public bool IsMasked
    {
        get { return (bool)GetValue(IsMaskedProperty); }
        set { SetValue(IsMaskedProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Mask.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsMaskedProperty =
        DependencyProperty.Register("IsMasked", typeof(bool), typeof(CopyableTextBlock), new PropertyMetadata(false, ModifyMasked));




    public bool IsPasswordLike
    {
        get { return (bool)GetValue(IsPasswordLikeProperty); }
        set { SetValue(IsPasswordLikeProperty, value); }
    }

    // Using a DependencyProperty as the backing store for IsPasswordLike.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsPasswordLikeProperty =
        DependencyProperty.Register("IsPasswordLike", typeof(bool), typeof(CopyableTextBlock), new PropertyMetadata(false, ModifyMasked));




    private static void ModifyMasked(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CopyableTextBlock tb) return;

        if (tb.IsPasswordLike && !string.IsNullOrWhiteSpace(tb.Text))
            tb.MaskedText = "●●●●●●●●";
        else
            tb.MaskedText = tb.IsMasked ? GenMask(tb.Text) : tb.Text;

        tb.SetCanCopy(tb.Text);
    }

    private static string GenMask(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        int maskCnt = text.Length / 5 * 2;
        var arr = text.ToCharArray();
        for (int i = maskCnt; i < text.Length - maskCnt; i++)
            arr[i] = '*';
        return new string(arr);
    }


    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (Template.FindName("PART_Copy", this) is Button button)
            button.Click += (s, e) => { if (Text is not null) Clipboard.SetDataObject(new DataObject(Text)); };
    }
    private void SetCanCopy(string? str)
    {
        CanCopy = !string.IsNullOrWhiteSpace(str) && str != "-";
    }
}
