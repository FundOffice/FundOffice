using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FMO;

public class DelayMouseOverExpandBehavior : Behavior<FrameworkElement>
{
    // 目标展开高度
    public static readonly DependencyProperty TargetMaxHeightProperty =
        DependencyProperty.Register(
            nameof(TargetMaxHeight),
            typeof(double),
            typeof(DelayMouseOverExpandBehavior),
            new PropertyMetadata(200.0));

    public double TargetMaxHeight
    {
        get => (double)GetValue(TargetMaxHeightProperty);
        set => SetValue(TargetMaxHeightProperty, value);
    }

    // 展开延迟时间
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(0.5);

    private DispatcherTimer? _timer;

    private double _originalHeight;

    protected override void OnAttached()
    {
        base.OnAttached();

        var element = AssociatedObject;

        // 设置初始 MaxHeight（防止被外部绑定覆盖）
        //if (double.IsNaN(element.MaxHeight) || element.ReadLocalValue(FrameworkElement.MaxHeightProperty) == DependencyProperty.UnsetValue)
        //{
        //    _originalHeight = element.Height;
        //}

        _timer = new DispatcherTimer { Interval = Delay };
        _timer.Tick += OnDelayElapsed;

        element.MouseEnter += OnMouseEnter;
        element.MouseLeave += OnMouseLeave;
    }

    protected override void OnDetaching()
    {
        var element = AssociatedObject;
        element.MouseEnter -= OnMouseEnter;
        element.MouseLeave -= OnMouseLeave;

        _timer?.Stop();
        _timer = null;

        base.OnDetaching();
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        _originalHeight = (sender as FrameworkElement)?.ActualHeight ?? 20;
        // 启动延迟计时器
        _timer?.Start();
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        // 取消延迟展开
        _timer?.Stop();

        var element = AssociatedObject;

        // 只有当前是展开状态，才执行收起动画
        // 注意：使用 ActualHeight 或当前 MaxHeight 判断
        double currentMax = element.MaxHeight;

        if (Math.Abs(currentMax - _originalHeight) > 0.5) // 防浮点误差
        {
            var anim = new DoubleAnimation(currentMax, _originalHeight, TimeSpan.FromSeconds(0.5))
            {
                DecelerationRatio = 0.9
            };
            element.BeginAnimation(FrameworkElement.MaxHeightProperty, anim);
        }
    }

    private void OnDelayElapsed(object? sender, EventArgs e)
    {
        _timer?.Stop();

        var element = AssociatedObject;

        // 执行展开动画：从当前 MaxHeight → TargetMaxHeight
        var from = element.MaxHeight;
        var to = TargetMaxHeight;

        var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(0.3))
        {
            DecelerationRatio = 0.9
        };
        element.BeginAnimation(FrameworkElement.MaxHeightProperty, anim);
    }
}