using System;
using System.Windows;
using System.Linq;
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
        double currentY = 0;        // 🟢 所有列都从顶部(0)开始
        double maxHeight = 0;       // 🟢 记录所有列的最大高度

        foreach (UIElement child in InternalChildren.OfType<UIElement>().Reverse())
        {
            double h = Math.Min(child.DesiredSize.Height, availableHeight);
            if (h <= 0) continue;

            // 🟢 统一从上往下的换列逻辑
            if (currentY + h > availableHeight)
            {
                // 记录当前列的实际高度，更新最大值
                maxHeight = Math.Max(maxHeight, currentY);

                columns.Add(new Column(currentColumnItems, 0));
                currentColumnItems = new List<ItemPosition>();
                currentY = 0; // 🟢 新列从顶部重新开始
            }

            // 添加元素到当前列（位置为 currentY，高度为 h）
            currentColumnItems.Add(new ItemPosition(child, currentY, h));
            currentY += h; // 🟢 向下累加
        }

        // 🟢 处理最后一列
        if (currentColumnItems.Count > 0)
        {
            maxHeight = Math.Max(maxHeight, currentY);
            columns.Add(new Column(currentColumnItems, 0));
        }

        // 计算 X 坐标（列水平排列）
        double x = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0) x += spacing;
            columns[i].X = x;
            x += fixedWidth;
        }

        return new LayoutData(columns, x, maxHeight);
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
