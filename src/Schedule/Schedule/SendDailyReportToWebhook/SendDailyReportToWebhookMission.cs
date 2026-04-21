using FMO.Models;
using FMO.Shared;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FMO.Schedule;

public class SendDailyReportToWebhookMission : Mission
{

    TimeOnly _time = new TimeOnly(9, 0, 0);
    public TimeOnly Time { get => _time; set { _time = value; if (NextRun is not null) SetNextRun(); } }

    public List<WebHookInfo> WebHooks { get; set; } = new();


    protected override void SetNextRun()
    {
        NextRun = LastRun < DateTime.Today ? DateTime.Today.AddTicks(Time.Ticks) : new DateTime(Days.NextTradingDay(DateTime.Today), Time);
    }


    protected override bool WorkOverride()
    {
        var urls = WebHooks.Where(x => x.IsEnabled).Select(x => x.Url).ToArray();
        if (urls.Length == 0) return false;


        Task.Run(() =>
        {

            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                DailyReportGridView dailyReportView = new(); 
                dailyReportView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                dailyReportView.Arrange(new Rect(0, 0, dailyReportView.DesiredSize.Width, dailyReportView.DesiredSize.Height));
                await Task.Delay(1000);

                try
                {
                    var dg = dailyReportView;
                    var watermark = "内部资料";

                    DrawingVisual drawingVisual = new DrawingVisual();
                    using var dc = drawingVisual.RenderOpen();


                    int w = (int)dg.ActualWidth, h = (int)dg.ActualHeight;

                    double rh = (w + h) / 1.41421356237309;

                    dc.DrawRectangle(new VisualBrush(dg), null, new Rect(0, 0, w, h ));
                    dc.PushOpacity(0.2);
                    dc.PushTransform(new RotateTransform(-45));
                    var fmt = new FormattedText(watermark, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("等线"), 32, Brushes.Black, 96);

                    for (int i = 0; i < rh; i += 200)
                    {
                        dc.DrawText(fmt, new Point(50 - i, i));
                        dc.DrawText(fmt, new Point(50 - i + fmt.Width + 150, i));
                        dc.DrawText(fmt, new Point(50 - i + (fmt.Width + 150) * 2, i));
                        dc.DrawText(fmt, new Point(50 - i + (fmt.Width + 150) * 3, i));
                    }
                    dc.Pop();
                    dc.Pop();
                    dc.Close();

                    RenderTargetBitmap render = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Pbgra32);
                    render.Render(drawingVisual);

                    using var ms = new MemoryStream();
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(render));
                    encoder.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    byte[] buf = ms.ToArray();
                    var b64 = Convert.ToBase64String(buf);
                    var md5 = System.Security.Cryptography.MD5.Create();
                    var hash = Encoding.UTF8.GetString(md5.ComputeHash(buf));
                    hash = BitConverter.ToString(md5.ComputeHash(buf)).Replace("-", "").ToLowerInvariant();
                    var json = $"{{ \"msgtype\": \"image\", \"image\": {{\"base64\": \"{b64}\", \"md5\": \"{hash}\" }} }}";

                    using var client = new HttpClient();
                    foreach (var url in urls)
                    {
                        await client.PostAsync(url, new StringContent(json));
                    }
                }
                catch { HandyControl.Controls.Growl.Error("发送日报失败"); }
            });


        }).Wait();


        return true;
    }



    public class WebHookInfo
    {
        public bool IsEnabled { get; set; }

        public string? Url { get; set; }

        public WebHookInfo() { }

        public WebHookInfo(bool isEnabled, string? url)
        {
            IsEnabled = isEnabled;
            Url = url;
        }
    }
}
