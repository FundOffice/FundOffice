using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FMO;

/// <summary>
/// VerifyWindow.xaml 的交互逻辑
/// </summary>
public partial class VerifyWindow : Window
{
    public VerifyWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is VerifyWindowViewModel v) v.Release();
    }

    private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is VerifyWindowViewModel v && v.TimeOut > 0)
            Task.Run(async () =>
            {
                await Task.Delay(v.TimeOut);
                await App.Current.Dispatcher.InvokeAsync(() => this.Close());
            });

    }
}



public partial class VerifyWindowViewModel : ObservableObject
{

    /// <summary>
    /// 显示第一列
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirstColumn))]
    public partial bool ShowCaptcha { get; set; }


    public GridLength FirstColumn => ShowCaptcha ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    [ObservableProperty]
    public partial ImageSource? CaptchaImage { get; set; }


    [ObservableProperty]
    public partial string? Code { get; set; }

    private VerifyMessage Verify { get; }

    public string? Title { get; private set; }

    /// <summary>
    /// millseconds
    /// </summary>
    public int TimeOut { get; set; }

    public VerifyWindowViewModel(VerifyMessage verify)
    {
        Title = verify.Title;
        TimeOut = verify.TimeOut;

        if (verify.Type == VerifyType.Captcha)
            ShowCaptcha = true;

        if (verify.Image is not null)
        {
            var img = new BitmapImage();
            img.BeginInit();
            var ms = new MemoryStream(verify.Image);
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();

            CaptchaImage = img;
        }

        Verify = verify;
    }


    [RelayCommand]
    public void Confirm(Window window)
    {
        if (string.IsNullOrWhiteSpace(Code)) return;


        Verify.Code = Code;
        Verify.Waiter.Set();
        window.Close();
    }

    internal void Release()
    {
        Verify.Waiter.Set();
    }
}