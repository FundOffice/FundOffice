using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FMO.Shared;

public class TwoColumnPanel : Panel
{
    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(nameof(ColumnSpacing), typeof(double), typeof(TwoColumnPanel),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(nameof(ItemSpacing), typeof(double), typeof(TwoColumnPanel),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    private readonly List<int> _columnAssignments = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        double colWidth = (availableSize.Width - ColumnSpacing) / 2;
        if (colWidth < 0) colWidth = 0;

        _columnAssignments.Clear();
        double leftHeight = 0, rightHeight = 0;

        // 收集可见子元素及其高度
        var visibleItems = new List<(UIElement child, double height)>();
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                _columnAssignments.Add(-1);
                continue;
            }

            child.Measure(new Size(colWidth, double.PositiveInfinity));
            visibleItems.Add((child, child.DesiredSize.Height));
            _columnAssignments.Add(0); // 占位，后面会重新赋值
        }

        if (visibleItems.Count == 0)
            return new Size(availableSize.Width, 0);

        int visibleCount = visibleItems.Count;

        // --- 第 1 步：标准瀑布流分配 ---
        var assignments = new int[visibleCount];
        leftHeight = 0;
        rightHeight = 0;

        for (int i = 0; i < visibleCount; i++)
        {
            double h = visibleItems[i].height;
            if (leftHeight <= rightHeight)
            {
                if (leftHeight > 0) leftHeight += ItemSpacing;
                leftHeight += h;
                assignments[i] = 0;
            }
            else
            {
                if (rightHeight > 0) rightHeight += ItemSpacing;
                rightHeight += h;
                assignments[i] = 1;
            }
        }

        // --- 第 2 步：如果右列更高，需要调整 ---
        if (rightHeight > leftHeight)
        {
            // 2a: 将最后一项强制放到左列
            int lastIdx = visibleCount - 1;
            double lastH = visibleItems[lastIdx].height;
            assignments[lastIdx] = 0;

            // 重新计算高度
            leftHeight = 0;
            rightHeight = 0;
            for (int i = 0; i < visibleCount; i++)
            {
                double h = visibleItems[i].height;
                if (assignments[i] == 0)
                {
                    if (leftHeight > 0) leftHeight += ItemSpacing;
                    leftHeight += h;
                }
                else
                {
                    if (rightHeight > 0) rightHeight += ItemSpacing;
                    rightHeight += h;
                }
            }

            // 2b: 如果左列仍然更矮，从右列搬运非末尾项到左列
            while (rightHeight > leftHeight)
            {
                bool moved = false;
                for (int i = 0; i < visibleCount; i++)
                {
                    if (i == lastIdx) continue; // 末尾项固定在左列
                    if (assignments[i] != 1) continue;

                    double h = visibleItems[i].height;
                    // 搬运后：左 = leftHeight + spacing + h，右 = rightHeight - h
                    // 需要 leftHeight + spacing + h >= rightHeight - h
                    double spacing = leftHeight > 0 ? ItemSpacing : 0;
                    if (leftHeight + spacing + h >= rightHeight - h)
                    {
                        assignments[i] = 0;
                        leftHeight += spacing + h;
                        rightHeight -= h;
                        moved = true;
                        break;
                    }
                }
                if (!moved) break;
            }
        }

        // --- 将最终分配结果写回 _columnAssignments ---
        int vi = 0;
        for (int i = 0; i < _columnAssignments.Count; i++)
        {
            if (_columnAssignments[i] == -1) continue; // 跳过折叠项
            _columnAssignments[i] = assignments[vi];
            vi++;
        }

        return new Size(availableSize.Width, Math.Max(leftHeight, rightHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double colWidth = (finalSize.Width - ColumnSpacing) / 2;
        if (colWidth < 0) colWidth = 0;
        double leftY = 0, rightY = 0;

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            UIElement child = InternalChildren[i];
            if (child.Visibility == Visibility.Collapsed) continue;

            int col = i < _columnAssignments.Count ? _columnAssignments[i] : 0;
            double x, y;

            if (col == 0)
            {
                x = 0;
                y = leftY;
                leftY += child.DesiredSize.Height + ItemSpacing;
            }
            else
            {
                x = colWidth + ColumnSpacing;
                y = rightY;
                rightY += child.DesiredSize.Height + ItemSpacing;
            }

            child.Arrange(new Rect(x, y, colWidth, child.DesiredSize.Height));
        }

        return finalSize;
    }
}