using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FMO.Shared;

public class HeaderWrapPanel : WrapPanel
{



    public double ReservedWidth
    {
        get { return (double)GetValue(ReservedWidthProperty); }
        set { SetValue(ReservedWidthProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ReservedWidth.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ReservedWidthProperty =
        DependencyProperty.Register("ReservedWidth", typeof(double), typeof(HeaderWrapPanel), new PropertyMetadata(0d));





    // 预留空白区域大小 
    private const double ReservedHeight = 30;

    protected override Size MeasureOverride(Size availableSize)
    { 
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = ReservedWidth; // 从左边开始偏移
        double y = 0; // 从顶部开始偏移
        double rowY = y;
        double rowHeight = 0.0;

        foreach (UIElement child in InternalChildren)
        {
            if (child == null) continue;

            double childWidth = child.DesiredSize.Width;
            double childHeight = child.DesiredSize.Height;

            // 如果当前行放不下这个元素，则换行
            if (x + childWidth > finalSize.Width)
            {
                x = 0;
                y = rowY + rowHeight;
                rowY = y;
                rowHeight = 0;
            }

            child.Arrange(new Rect(x, y, childWidth, childHeight));

            x += childWidth;
            rowHeight = Math.Max(rowHeight, childHeight);
        }

        return finalSize;
    }

}
