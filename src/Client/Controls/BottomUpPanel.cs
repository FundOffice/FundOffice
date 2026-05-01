using System;
using System.Windows;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// 布局规则：
/// 1. 每一项固定宽度 ItemWidth
/// 2. 每一列 从下向上 排列
/// 3. 一列排满后，自动向右新建一列
/// 4. 所有列都从底部对齐
/// </summary>
public class BottomUpPanel : Panel
{
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(double),
            typeof(BottomUpPanel),
            new PropertyMetadata(300.0, OnLayoutChanged));

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as BottomUpPanel)?.InvalidateMeasure();
    }

    // 添加依赖属性
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(
            nameof(ColumnSpacing),
            typeof(double),
            typeof(BottomUpPanel),
            new PropertyMetadata(5.0, OnLayoutChanged)); // 默认 5px



    protected override Size MeasureOverride(Size availableSize)
    {
        // 处理无限高情况（如未限制高度时给一个默认安全值）
        double availableHeight = availableSize.Height == double.PositiveInfinity ? 1000 : availableSize.Height;
        availableHeight = Math.Max(availableHeight, 1);

        // 测量所有子元素
        double fixedWidth = Math.Max(ItemWidth, 1);
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(fixedWidth, double.PositiveInfinity));
        }

        // 计算布局并返回所需尺寸
        var layout = ComputeLayout(availableHeight, fixedWidth);
        return new Size(layout.TotalWidth, layout.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double availableHeight = finalSize.Height == double.PositiveInfinity ? 1000 : finalSize.Height;
        availableHeight = Math.Max(availableHeight, 1);
        double fixedWidth = Math.Max(ItemWidth, 1);

        var layout = ComputeLayout(availableHeight, fixedWidth);

        // 执行排列
        foreach (var col in layout.Columns)
        {
            foreach (var item in col.Items)
            {
                item.Child.Arrange(new Rect(col.X, item.Y, fixedWidth, item.Height));
            }
        }

        return finalSize;
    }

    private LayoutData ComputeLayout(double availableHeight, double fixedWidth)
    {
        double spacing = Math.Max(0, ColumnSpacing); // 防止负值

        var columns = new List<Column>();
        var currentColumnItems = new List<ItemPosition>();
        double currentY = availableHeight;
        double height = 0;
        int colIndex = 0;

        foreach (UIElement child in InternalChildren)
        {
            double h = Math.Min(child.DesiredSize.Height, availableHeight);
            if (h <= 0) continue;

            if (colIndex == 0) // 🟢 第一列：从下向上
            {
                if (currentY - h < 0)
                {
                    columns.Add(new Column(currentColumnItems, 0));
                    currentColumnItems = new List<ItemPosition>();
                    colIndex++;
                    currentY = h;
                    currentColumnItems.Add(new ItemPosition(child, 0, h));
                }
                else
                {
                    height += h;
                    currentY -= h;
                    currentColumnItems.Add(new ItemPosition(child, currentY, h));
                }
            }
            else // 🔵 后续列：从上向下（保持原逻辑）
            {
                if (currentY + h > availableHeight)
                {
                    columns.Add(new Column(currentColumnItems, 0));
                    currentColumnItems = new List<ItemPosition>();
                    colIndex++;
                    currentY = h;
                    currentColumnItems.Add(new ItemPosition(child, 0, h));
                }
                else
                {
                    currentColumnItems.Add(new ItemPosition(child, currentY, h));
                    currentY += h;
                }
            }
        }

        if (currentColumnItems.Count > 0)
            columns.Add(new Column(currentColumnItems, 0));

        // 计算 X 坐标时使用 spacing 变量
        double x = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0) x += spacing;
            columns[i].X = x;
            x += fixedWidth;
        }

        return new LayoutData(columns, x, height);
    }

    #region 内部数据结构

    private class ItemPosition
    {
        public UIElement Child { get; }
        public double Y { get; }
        public double Height { get; }
        public ItemPosition(UIElement child, double y, double height) => (Child, Y, Height) = (child, y, height);
    }

    private class Column
    {
        public List<ItemPosition> Items { get; }
        public double X { get; set; }
        public Column(List<ItemPosition> items, double x) => (Items, X) = (items, x);
    }

    private class LayoutData
    {
        public List<Column> Columns { get; }
        public double TotalWidth { get; }
        public double Height { get; }

        public LayoutData(List<Column> columns, double totalWidth, double height) => (Columns, TotalWidth, Height) = (columns, totalWidth, height);
    }

    #endregion
}
