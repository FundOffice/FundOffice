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
///     <MyNamespace:YearCalender/>
///
/// </summary>
public class YearCalender : Control
{
    private ListBox? PART_Calenders;

    static YearCalender()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(YearCalender), new FrameworkPropertyMetadata(typeof(YearCalender)));
    }


    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        PART_Calenders = Template.FindName("PART_Calenders", this) as ListBox;
    }

    public int Year
    {
        get { return (int)GetValue(YearProperty); }
        set { SetValue(YearProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Year.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty YearProperty =
        DependencyProperty.Register("Year", typeof(int), typeof(YearCalender), new PropertyMetadata(2025));


    public IEnumerable<IDate> ItemsSource
    {
        get { return (IEnumerable<IDate>)GetValue(ItemsSourceProperty); }
        set { SetValue(ItemsSourceProperty, value); }
    }
    // Using a DependencyProperty as the backing store for ItemsSource.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register("ItemsSource", typeof(IEnumerable<IDate>), typeof(YearCalender), new PropertyMetadata(null, OnItemsChanged));

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is YearCalender yc && yc.PART_Calenders is ListBox lb && yc.ItemsSource is not null)
        {
            var g = yc.ItemsSource.OrderBy(x => x.Date).GroupBy(x => x.Date.Month);
            List<(int m, IList<IDate> d)> list = new(g.Count());

            foreach (var item in g)
            {
                var l = item.ToList();
                l.InsertRange(0, new IDate[((int)item.First().Date.DayOfWeek)]);
                list.Add((item.Key, l));
            }

            lb.ItemsSource = list.Select(x => new { Month = x.m, Items = x.d });
        }
    }
}



public class SimpleCalender : Control
{

    static SimpleCalender()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SimpleCalender), new FrameworkPropertyMetadata(typeof(SimpleCalender)));
    }




    public int? Month
    {
        get { return (int?)GetValue(MonthProperty); }
        set { SetValue(MonthProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Month.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MonthProperty =
        DependencyProperty.Register("Month", typeof(int?), typeof(SimpleCalender), new PropertyMetadata(null));



    public IEnumerable<IDate> ItemsSource
    {
        get { return (IEnumerable<IDate>)GetValue(ItemsSourceProperty); }
        set { SetValue(ItemsSourceProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ItemsSource.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register("ItemsSource", typeof(IEnumerable<IDate>), typeof(SimpleCalender), new PropertyMetadata(null));


    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (Template.FindName("PART_Header", this) is ListBox lb)
        {
            lb.ItemsSource = (string[])["日", "一", "二", "三", "四", "五", "六"];
        }
    }
}