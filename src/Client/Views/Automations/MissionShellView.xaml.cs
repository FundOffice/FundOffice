using System.Windows;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// MissionShellView.xaml 的交互逻辑
/// </summary>
public partial class MissionShellView : UserControl
{
    public MissionShellView()
    {
        InitializeComponent();
    }


    protected override Size MeasureOverride(Size constraint)
    {
        if (ActualWidth > 0 && !double.IsInfinity(ActualWidth))
            return base.MeasureOverride(new Size(ActualWidth, constraint.Height));
        return base.MeasureOverride(constraint);
    }


    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        return base.ArrangeOverride(arrangeBounds);
    }
}
