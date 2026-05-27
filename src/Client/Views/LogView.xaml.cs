using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using LiteDB.Engine;
using MoT;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FMO;

/// <summary>
/// LogView.xaml 的交互逻辑
/// </summary>
public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
    }
}


public partial class LogViewModel : ObservableObject
{

    [ObservableProperty]
    public partial ObservableCollection<LogEvent> CommonLogs { get; set; } = [];

    public LogViewModel()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.db");
        using var db = new LiteDatabase($@"FileName={path};ReadOnly=true");

        try
        {
            CommonLogs = [.. Logg.Read().OrderByDescending(x => x.Timestamp).Take(100).ToArray()];
            //CommonLogs = [.. db.GetCollection("Logg").Query().OrderByDescending(x => x["_t"].AsDateTime).Limit(100).ToEnumerable().Select(x => To(x))];
        }
        catch (LiteException e)
        {
            using (var engine = new LiteEngine(path))
            {
                engine.Rebuild(); // 重建到新文件
            }
        }
    }


    private static LogMessage To(BsonDocument x)
    {
        return new LogMessage(x["_t"].AsDateTime, x[nameof(LogMessage.File)].AsString, x[nameof(LogMessage.Method)].AsString, x[nameof(LogMessage.Line)].AsInt32, x["_m"].AsString);
    }

    [RelayCommand]
    public void ScrollToEnd()
    {
        var data = Logg.Read().OrderByDescending(x => x.Timestamp).Skip(CommonLogs.Count).Take(100).ToArray();

        foreach (var item in data)
            CommonLogs.Add(item);
    }

    public record LogMessage(DateTime Time, string File, string Method, int Line, string Message);
}

public static class InfiniteScrollExtension
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(InfiniteScrollExtension),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached("CommandParameter", typeof(object), typeof(InfiniteScrollExtension),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ThresholdProperty =
        DependencyProperty.RegisterAttached("Threshold", typeof(double), typeof(InfiniteScrollExtension),
            new PropertyMetadata(0.0));

    public static void SetCommand(DependencyObject element, ICommand value) => element.SetValue(CommandProperty, value);
    public static ICommand GetCommand(DependencyObject element) => (ICommand)element.GetValue(CommandProperty);

    public static void SetCommandParameter(DependencyObject element, object value) => element.SetValue(CommandParameterProperty, value);
    public static object GetCommandParameter(DependencyObject element) => element.GetValue(CommandParameterProperty);

    public static void SetThreshold(DependencyObject element, double value) => element.SetValue(ThresholdProperty, value);
    public static double GetThreshold(DependencyObject element) => (double)element.GetValue(ThresholdProperty);

    private static readonly DependencyProperty InternalStateProperty =
        DependencyProperty.RegisterAttached("InternalState", typeof(ScrollState), typeof(InfiniteScrollExtension));

    private sealed class ScrollState
    {
        public required ScrollViewer ScrollViewer;
        public bool IsExecuting;
        public double CapturedOffset;
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl control) return;

        control.Loaded -= OnControlLoaded;
        control.Unloaded -= OnControlUnloaded;

        if (e.NewValue != null)
        {
            control.Loaded += OnControlLoaded;
            control.Unloaded += OnControlUnloaded;
            if (control.IsLoaded) AttachScrollViewer(control);
        }
        else
        {
            DetachScrollViewer(control);
        }
    }

    private static void OnControlLoaded(object sender, RoutedEventArgs e) => AttachScrollViewer((ItemsControl)sender);
    private static void OnControlUnloaded(object sender, RoutedEventArgs e) => DetachScrollViewer((ItemsControl)sender);

    private static void AttachScrollViewer(ItemsControl control)
    {
        var scrollViewer = FindScrollViewer(control);
        if (scrollViewer == null) return;

        // ⭐ 强制物理像素滚动：逻辑滚动(按Item)会导致新增项时Offset单位跳变，引发位置错乱
        ScrollViewer.SetCanContentScroll(scrollViewer, false);

        scrollViewer.ScrollChanged += OnScrollChanged;
        control.SetValue(InternalStateProperty, new ScrollState { ScrollViewer = scrollViewer });
    }

    private static void DetachScrollViewer(ItemsControl control)
    {
        if (control.GetValue(InternalStateProperty) is ScrollState state)
        {
            if (state.ScrollViewer != null)
                state.ScrollViewer.ScrollChanged -= OnScrollChanged;
            control.ClearValue(InternalStateProperty);
        }
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scrollViewer = sender as ScrollViewer;
        if (scrollViewer == null) return;

        var control = FindParentItemsControl(scrollViewer);
        if (control == null || control.GetValue(InternalStateProperty) is not ScrollState state) return;

        // 防并发锁
        if (state.IsExecuting) return;

        double threshold = GetThreshold(control);
        bool isAtBottom = scrollViewer.ScrollableHeight <= 0 ||
                          scrollViewer.VerticalOffset + scrollViewer.ViewportHeight >= scrollViewer.ExtentHeight - threshold;

        if (isAtBottom)
        {
            var cmd = GetCommand(control);
            var param = GetCommandParameter(control);

            if (cmd?.CanExecute(param) == true)
            {
                state.IsExecuting = true;
                state.CapturedOffset = scrollViewer.VerticalOffset; // 1. 记录触发前的精确视觉位置
                cmd.Execute(param);

                // 2. 等待布局完全更新后，恢复位置并解锁
                // 使用 ContextIdle 确保 Measure/Arrange/Render 全部完成
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    scrollViewer.ScrollToVerticalOffset(state.CapturedOffset);
                    state.IsExecuting = false; // 解锁，允许下次滚动触发
                }), DispatcherPriority.ContextIdle);
            }
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent == null) return null;
        if (parent is ScrollViewer sv) return sv;

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var found = FindScrollViewer(child);
            if (found != null) return found;
        }
        return null;
    }

    private static ItemsControl? FindParentItemsControl(DependencyObject child)
    {
        var current = child;
        while (current != null)
        {
            if (current is ItemsControl ic) return ic;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}