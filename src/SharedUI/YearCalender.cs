using FMO.Models;
using System.Collections;
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



public class SimpleCalender : ItemsControl
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




    public IDate? SelectedDate
    {
        get { return (IDate?)GetValue(SelectedDateProperty); }
        set { SetValue(SelectedDateProperty, value); }
    }

    // Using a DependencyProperty as the backing store for SelectedDate.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(nameof(SelectedDate), typeof(IDate), typeof(SimpleCalender), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));




    // 2. 内部供 UI 绑定的数据源（包含 null 占位）
    public IEnumerable<IDate?> DisplayItems
    {
        get { return (IEnumerable<IDate?>)GetValue(DisplayItemsProperty); }
    }

    private static readonly DependencyPropertyKey DisplayItemsPropertyKey =
        DependencyProperty.RegisterReadOnly("DisplayItems", typeof(IEnumerable<IDate?>), typeof(SimpleCalender), new PropertyMetadata(null));

    public static readonly DependencyProperty DisplayItemsProperty = DisplayItemsPropertyKey.DependencyProperty;


    protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
    {
        if (newValue is IEnumerable<IDate> newItems)
        {
            CalibrateItems(newItems);
        }
        else
        {
            SetValue(DisplayItemsPropertyKey, null);
        }
    }


    private void CalibrateItems(IEnumerable<IDate> source)
    {
        int offset = 0;

        var firstDate = source.FirstOrDefault();
        if (firstDate != null)
        {
            // 假设你的 IDate 接口里有一个能获取 DateTime 的属性（例如 .Date）
            // 如果 1 号是星期二 (DayOfWeek.Tuesday = 2)，offset 就是 2
            offset = (int)firstDate.Date.DayOfWeek;
        }

        // 将带有 null 占位符的生成器赋值给 DisplayItems
        SetValue(DisplayItemsPropertyKey, GenerateWithOffset(source, offset));
    }

    // 核心：使用 yield return 动态生成带占位符的集合
    private IEnumerable<IDate?> GenerateWithOffset(IEnumerable<IDate> source, int offset)
    {
        // 1. 前面先返回 null 空出格子
        for (int i = 0; i < offset; i++)
        {
            yield return null;
        }

        // 2. 再返回真实的日期数据
        if (source != null)
        {
            foreach (var item in source)
            {
                yield return item;
            }
        }
    }



    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (Template.FindName("PART_Header", this) is ListBox lb)
        {
            lb.ItemsSource = (string[])["日", "一", "二", "三", "四", "五", "六"];
        }
    }
}