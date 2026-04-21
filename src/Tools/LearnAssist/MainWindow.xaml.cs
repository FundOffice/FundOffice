using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnAssist;
using Microsoft.Playwright;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace FMO.LearnAssist;

using Toast = HandyControl.Controls.Growl;


/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{


    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DataContext = new MainWindowViewModel();
    }
}




public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    public partial string? Name { get; set; }


    [ObservableProperty]
    public partial bool IsCheckingLogin { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowLoginTip { get; set; }

    [ObservableProperty]
    public partial bool ShowLoginError { get; set; }


    [ObservableProperty]
    public partial bool IsLogin { get; set; }

    [ObservableProperty]
    public partial int CurrentClass { get; set; }

    [ObservableProperty]
    public partial int CurrentChapter { get; set; }

    [ObservableProperty]
    public partial int? CountDown { get; set; }


    [ObservableProperty]
    public partial bool IsLoadingClasses { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool CanStartLearn { get; set; }

    public ObservableCollection<ClassLearnInfo> Classes { get; } = new();

    private bool IsInitialized = false;


    IPlaywright? Operator { get; set; }
    IBrowser? Browser { get; set; }
    IBrowserContext? Context { get; set; }

    [ObservableProperty]
    public partial decimal? TotalHour { get; private set; }

    [ObservableProperty]
    public partial decimal? MoralHour { get; private set; }

    [ObservableProperty]
    public partial decimal? LawHour { get; private set; }

    [ObservableProperty]
    public partial decimal? SkillHour { get; private set; }


    public decimal PeakedHour => Classes.Sum(x => x.Hour);

    public bool IsMoralNotEnough => Classes.Where(x => x.TypeId == 1).Sum(x => x.Hour) < 5;

    public bool IsTotalNotEnough => PeakedHour + TotalHour < 15;


    [ObservableProperty]
    public partial int Year { get; set; }

    public MainWindowViewModel()
    {
        Directory.CreateDirectory("data");
        Directory.CreateDirectory("files\\peixun");


        Init();

        IsInitialized = true;
    }

    private void Init()
    {

        Task.Run(async () =>
        {
            Operator = await Playwright.CreateAsync();
            Browser = await Operator.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = true });

            if (File.Exists("files\\peixun\\learn.json"))
            {
                Context = await Browser.NewContextAsync(new BrowserNewContextOptions { StorageStatePath = "files\\peixun\\learn.json" });
            }
            else
                Context = await Browser.NewContextAsync();
            var page = await Context.NewPageAsync();
            await page.GotoAsync("https://peixun.amac.org.cn/");

            await CheckLogin(page);

            IsCheckingLogin = false;

            if (!IsLogin)
                await ManualLogin();

            IsLoadingClasses = true;
            page = Context.Pages.First();

            await GetClassInfo(page);

            await GetCurrentYearLearn(page);
            IsLoadingClasses = false;



            // Classes.CollectionChanged += (s, e) => OnPropertyChanged(nameof(PeakedHour));

            await App.Current.Dispatcher.BeginInvoke(() => CanStartLearn = true);

            // 居中
            await App.Current.Dispatcher.BeginInvoke(() =>
            {
                var wnd = Application.Current.MainWindow;
                var screen = SystemParameters.WorkArea;
                wnd.Left = (screen.Width - wnd.Width) / 2 + screen.Left;
                wnd.Top = (screen.Height - wnd.Height) / 2 + screen.Top;
            });

        });
    }


    private async Task CheckLogin(IPage page, int timeout = 2000)
    {
        // 验证
        try
        {
            var ele = await page.WaitForSelectorAsync("span.myname", new PageWaitForSelectorOptions { Timeout = timeout });

            IsLogin = ele is null ? false : await ele.IsVisibleAsync();

            if (IsLogin)
            {
                Name = await ele!.InnerTextAsync();

                await page.Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = "files\\peixun\\learn.json" });
            }
        }
        catch (TimeoutException e)
        {
            //Toast.Warning("登录超时");
            //await App.Current.Dispatcher.BeginInvoke(() => App.Current.Shutdown());
        }
        catch (Exception e)
        {
            Toast.Warning("登录失败");
            return;
        }
    }


    private async Task ManualLogin()
    {
        ShowLoginTip = true;
        double bot = 100;
        await App.Current.Dispatcher.BeginInvoke(() =>
        {
            var wnd = App.Current.MainWindow;
            wnd.Top = 100;
            bot = wnd.Top + wnd.Height;
        });

        using var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "msedge",
            Headless = false,
            Args = new[]
        {
            $"--window-position=100,{bot+5}",
            "--window-size=600,600"
        }
        });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("https://peixun.amac.org.cn/");

        CountDown = 300;

        using var t = new Timer((x) => CountDown = Math.Max(1, CountDown.Value - 1), null, 0, 1000);


        await CheckLogin(page, (int)CountDown * 1000);

        t.Dispose();
        CountDown = null;

        if (!IsLogin)
            await App.Current.Dispatcher.BeginInvoke(() => App.Current.Shutdown());

        await page.CloseAsync();
        await browser.CloseAsync();
        pw.Dispose();

        await Context!.DisposeAsync();
        Context = await Browser!.NewContextAsync(new BrowserNewContextOptions { StorageStatePath = "files\\peixun\\learn.json" });

        page = await Context.NewPageAsync();
        await page.GotoAsync("https://peixun.amac.org.cn/");
        await CheckLogin(page, 4 * 1000);

        ShowLoginTip = false;
        IsCheckingLogin = false;

        if (!IsLogin)
        {
            ShowLoginError = true;
        }
    }


    [RelayCommand(CanExecute = nameof(CanStartLearn))]
    public async Task Start()
    {
        if (Classes?.Count == 0)
        {
            Toast.Warning("没有找到任何课程");
            return;
        }


        foreach (var current in Classes!)
        {
            try
            {
                // 打开课程页
                Toast.Info($"开始学习 【{current.Name}】");
                var page = Context?.Pages.FirstOrDefault();
                if (page is null) return;

                await page.GotoAsync("https://peixun.amac.org.cn/" + current.Url);
                await page.WaitForTimeoutAsync(2000);
                await SkipTips(page);

                // 获取所有章节
                var locator = page.Locator("#tab-content >> .tt2 >> div.xue-tt");
                List<ChapterInfo> cs = new();
                foreach (var ch in await locator.AllAsync())
                {
                    var name = await ch.Locator("a").First.InnerTextAsync(new LocatorInnerTextOptions { Timeout = 1000 });
                    var state = await ch.Locator("div.right >> a").First.InnerTextAsync();
                    var url = await ch.Locator("div.right >> a").First.GetAttributeAsync("data-href");

                    ChapterInfo chapter = new() { Name = name, Learned = state == "已学习", Url = url };
                    cs.Add(chapter);
                }

                // 获取测验
                List<TestInfo> testList = new();
                var tests = page.Locator("div.test-btn-box");
                if (await tests.CountAsync() > 0)
                {
                    //是否完成
                    foreach (var item in await tests.AllAsync())
                    {
                        var box = await item.Locator("div.hf_bg").BoundingBoxAsync(new LocatorBoundingBoxOptions { Timeout = 100 });

                        var url = await item.Locator("a.a1").GetAttributeAsync("href");
                        var @new = await item.Locator("a.a2").CountAsync() == 0;
                        testList.Add(new TestInfo { Url = url!, Score = @new ? 0 : box is null ? 100 : 0 });
                    }

                }

                current.Chapters = cs.ToArray();
                current.Tests = testList.ToArray();

                for (int i = 0; i < cs.Count; i++)
                {
                    var ch = cs[i];

                    if (ch.Learned) continue;

                    await LearnSingleClass(ch, page);
                }

                // 做题

                foreach (var item in testList)
                {
                    if (item.Score == 100) continue;

                    try
                    {
                        await DoTest(page, item);

                        if (item.Score != 100)
                            await DoTest(page, item);
                    }
                    catch (Exception ex) { Toast.Error($"答题出错： {ex.Message}"); }
                }
            }
            catch (Exception e)
            {
                Toast.Error($"{e}");
            }
        }
    }


    [RelayCommand]
    public async Task Pick()
    {
        CanStartLearn = false;


        var browser = await Operator!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = false });

        var context = File.Exists("files\\peixun\\learn.json") ? await browser.NewContextAsync(new BrowserNewContextOptions { StorageStatePath = "files\\peixun\\learn.json" }) : await browser.NewContextAsync();

        var page = await context.NewPageAsync();
        await page.GotoAsync("https://peixun.amac.org.cn/");

        page.Close += Page_Close;
    }

    private void Page_Close(object? sender, IPage e)
    {
        IsLoadingClasses = true;


        Toast.Info("重新加载课程");

        try
        {
            var page = Context!.Pages.First();

            App.Current.Dispatcher.BeginInvoke(async () =>
           {
               Classes.Clear();
               await GetClassInfo(page);

               await GetCurrentYearLearn(page);
               IsLoadingClasses = false;

               CanStartLearn = true;
           });
        }
        catch (Exception ex)
        {
            Toast.Error(ex.Message);
        }

    }

    [RelayCommand]
    public async Task Test()
    {
        try
        {
            var page = Context.Pages[0];


            var locator = page.Locator("div.cont-wrap >> div.video-box");
            var vi = await locator.IsVisibleAsync();


        }
        catch (Exception e)
        {

        }
    }


    private async Task GetCurrentYearLearn(IPage page)
    {
        var locator = page.Locator("#yearid");
        var year = DateTime.Today.Year;
        Year = year;
        await locator.ClickAsync(new LocatorClickOptions { Timeout = 2000 });
        locator = page.Locator($"ul.year-pulldown >> li:has-text('{year}')");
        await locator.ClickAsync(new LocatorClickOptions { Timeout = 2000 });

        await page.WaitForTimeoutAsync(2000);

        TotalHour = decimal.Parse(await page.Locator("#sum-hour-total").InnerTextAsync());
        MoralHour = decimal.Parse(await page.Locator("#sum-hour-type1").InnerTextAsync());
        LawHour = decimal.Parse(await page.Locator("#sum-hour-type2").InnerTextAsync());
        SkillHour = decimal.Parse(await page.Locator("#sum-hour-type3").InnerTextAsync());

    }

    private async Task GetClassInfo(IPage page)
    {
        var locator = page.Locator("div.fixed_top").GetByText("我的学习");

        if (!await locator.IsVisibleAsync())
        {
            Toast.Warning($"Error {LineNumber()}");
            return;
        }
        await locator.ClickAsync();

        // 未学习 div.title >> ul.studyState.clearfix
        // 学习中

        // 获取未学习
        locator = page.Locator("div.title >> ul.studyState.clearfix").GetByText("学习中");
        await locator.ClickAsync();

        try
        {
            await page.WaitForSelectorAsync("#userclass_list", new PageWaitForSelectorOptions { Timeout = 10000 });
            await page.WaitForTimeoutAsync(2000);


            var classes = await page.Locator("#userclass_list >> div.mycourse_list_cont").AllAsync();
            foreach (var c in classes)
            {
                var hour = await c.GetAttributeAsync("hour") is string s ? decimal.TryParse(s, out var h) ? h : 0 : 0;
                var tid = await c.GetAttributeAsync("gid") is string s2 ? int.TryParse(s2, out var t) ? t : 0 : 0;


                // 课程名
                var name = await c.Locator("h4").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2000 });
                var url = await c.Locator("h4 >> a").GetAttributeAsync("href", new LocatorGetAttributeOptions { Timeout = 2000 });
                var ratio = await c.Locator("span.mycourse_list_bar_text >> span").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2000 });

                var learn = c.GetByText("继续学习");

                await App.Current.Dispatcher.BeginInvoke(() => Classes.Add(new ClassLearnInfo { Name = name, Url = url, Hour = hour, TypeId = tid, Progress = decimal.Parse(ratio) / 100m }));

            }
        }
        catch { }

        locator = page.Locator("div.title >> ul.studyState.clearfix").GetByText("未学习");
        await locator.ClickAsync();

        try
        {
            locator = page.GetByText("本年度暂无未学习课程");
            if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync()) return;

            await page.WaitForSelectorAsync("#userclass_list", new PageWaitForSelectorOptions { Timeout = 10000 });
            await page.WaitForTimeoutAsync(2000);

            var classes = await page.Locator("#userclass_list >> div.mycourse_list_cont").AllAsync();
            foreach (var c in classes)
            {
                var hour = await c.GetAttributeAsync("hour") is string s ? decimal.TryParse(s, out var h) ? h : 0 : 0;
                var tid = await c.GetAttributeAsync("gid") is string s2 ? int.TryParse(s2, out var t) ? t : 0 : 0;

                // 课程名
                var name = await c.Locator("h4").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2000 });
                var url = await c.Locator("h4 >> a").GetAttributeAsync("href", new LocatorGetAttributeOptions { Timeout = 2000 });
                var ratio = await c.Locator("span.mycourse_list_bar_text >> span").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 2000 });

                var learn = c.GetByText("继续学习");

                await App.Current.Dispatcher.BeginInvoke(() => Classes.Add(new ClassLearnInfo { Name = name, Url = url, TypeId = tid, Hour = hour, Progress = decimal.Parse(ratio) }));

            }
        }
        catch { }

        OnPropertyChanged(nameof(PeakedHour));
        OnPropertyChanged(nameof(IsMoralNotEnough));
        OnPropertyChanged(nameof(IsTotalNotEnough));
    }


    private async Task PlayVideo(IPage page)
    {
        var locator = page.Locator("div.cont-wrap >> div.video-box");
        var vi = await locator.IsVisibleAsync();

        var box = await locator.BoundingBoxAsync(new LocatorBoundingBoxOptions { Timeout = 1000 });
        await page.Mouse.ClickAsync(box!.X + box.Width / 2, box.Y + box.Height / 2);
    }

    private async Task LearnSingleClass(ChapterInfo ch, IPage page)
    {
        Toast.Info($"学习 【{ch.Name}】");

        await page.GotoAsync("https://peixun.amac.org.cn/" + ch.Url);

        await SkipTips(page);

        await PlayVideo(page);

        // 等待视频加载
        for (int j = 0; j < 10; j++)
        {
            if (page.Frames.Count < 2)
                await page.WaitForTimeoutAsync(3000);
        }

        int noneplay = 0;
        double last = 0;

        var iframe = page.Frames.FirstOrDefault(x => x.Url.Contains("player"));
        if (iframe is not null)
        {
            var video = await iframe.WaitForSelectorAsync("video", new FrameWaitForSelectorOptions { Timeout = 2000, State = WaitForSelectorState.Visible });

            if (video is not null)
            {
                var du = await video.EvaluateAsync<double>("video => video.duration");

                var dur = TimeSpan.FromSeconds(du);

                for (int j = 0; j < 60 * 60; j++)//1小时
                {
                    await Task.Delay(1000);

                    var pro = await video.EvaluateAsync<double>("video => video.currentTime");

                    ch.VideoProgress = pro / du * 100;
                    ch.VideoTime = double.IsNaN(pro) ? "-" : @$"{TimeSpan.FromSeconds(pro):hh\:mm\:ss} / {dur:hh\:mm\:ss}";
                    if (pro < last)
                    {
                        ch.VideoProgress = 0;
                        ch.Learned = true;
                        ch.VideoTime = "";
                        Toast.Info($"学习 【{ch.Name}】 结束");
                        break;
                    }


                    if (last == pro)
                    {
                        ++noneplay;
                        Toast.Info("播放进度异常");
                    }

                    if (noneplay > 10)
                    {
                        Toast.Error("播放进度异常, 即将重新播放");

                        await LearnSingleClass(ch, page);

                        return;
                    }
                    last = pro;
                }
            }
        }
    }




    private async Task SkipTips(IPage page)
    {
        try
        {
            for (int i = 0; i < 10; i++)
            {
                var locator = page.Locator("span.next");
                foreach (var item in await locator.AllAsync())
                {
                    try
                    {
                        if (await item.IsVisibleAsync())
                        {
                            await item.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions { Timeout = 1000 });

                            await page.WaitForTimeoutAsync(300);
                            await item.ClickAsync();
                            break;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private int LineNumber([CallerLineNumber] int line = 0) => line;


    private async Task DoTest(IPage page, TestInfo ch, int[][]? answers = null)
    {
        await page.GotoAsync("https://peixun.amac.org.cn/" + ch.Url);

        await SkipTips(page);

        await page.WaitForTimeoutAsync(1000);

        for (int i = 0; i < 20; i++)
        {
            var locator = page.Locator("div.cont-wrap >> div.test >> div.test-cont");

            // 题目类型
            var type = await locator.Locator("span.test-style").First.InnerTextAsync();

            // 选项
            var sel = locator.Locator("div.xuanxiang");
            if (await sel.CountAsync() == 0)
            {
                Toast.Warning("无法获取题目选项");
                return;
            }

            // 答案
            var answerStr = await sel.First.GetAttributeAsync("answer");
            var cans = string.IsNullOrWhiteSpace(answerStr) ? null : answerStr.Split(',').Select(x => int.Parse(x) - 1).ToArray();

            var items = await sel.First.Locator("ul >> li").AllAsync();

            if (answers is null && cans is null)
            {
                if (type.Contains("多选"))
                {
                    foreach (var item in items)
                    {
                        await item.ClickAsync();
                    }
                }
                else await items[Random.Shared.Next(items.Count - 1)].ClickAsync();
            }
            else if (answers is not null)
            {
                foreach (var id in answers[i])
                {
                    await items[id].ClickAsync();
                }
            }
            else
            {
                foreach (var id in cans!)
                {
                    await items[id].ClickAsync();
                }
            }

            await page.WaitForTimeoutAsync(1000);

            locator = page.Locator("div.test >> a").Filter(new LocatorFilterOptions { HasText = "下一题" });
            if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync())
                await locator.First.ClickAsync();
            else break;
        }

        if (page.Locator("div.test >> a").Filter(new LocatorFilterOptions { HasText = "提交测验" }) is ILocator loc && await loc.CountAsync() > 0 && await loc.First.IsVisibleAsync())
        {
            await loc.ClickAsync();

            // 检查
            await SkipTips(page);
            var locator = page.Locator("div.cir-intro");
            try
            {
                var cont = await page.ContentAsync();
                if (cont.Contains("已完成本课程"))
                {
                    ch.Score = 100;
                    return;
                }

                await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 2000 });
                var result = await locator.InnerTextAsync();
                if (result.Contains("100")) //全对
                {
                    ch.Score = 100;
                    return;
                }
            }
            catch (Exception e)
            {
                Toast.Warning("无法获取答题结果");
                return;
            }

            Toast.Warning("无法获取答题结果，请关闭程序后再试");

            //if (answers is not null)
            //{
            //    Toast.Error("已有答案，但答题错误，请手动答题");
            //    return;
            //}

            //// 获取答案
            //locator = page.Locator("div.opt-btn >> a").Filter(new LocatorFilterOptions { HasText = "测验回顾" });
            

            //try
            //{
            //    await locator.ClickAsync();

            //    answers = await GetTestAnswer(page);

            //    await DoTest(page, ch, answers);
            //}
            //catch
            //{
            //    Toast.Error("无法获取答案");
            //}
        }


    }


    private async Task<int[][]> GetTestAnswer(IPage page)
    {
        List<int[]> results = new();

        while (true)
        {
            var loc = page.Locator("div.test >> div.test-result").Filter(new LocatorFilterOptions { HasText = "正确答案是" });
            var ans = await loc.InnerTextAsync();

            var ansid = Regex.Matches(ans, "[A-Z]").Select(x => x.Value[0] - 'A').ToArray();
            results.Add(ansid);

            await page.WaitForTimeoutAsync(1000);
            var locator = page.Locator("div.test >> a").Filter(new LocatorFilterOptions { HasText = "下一题" });
            if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync())
                await locator.First.ClickAsync();
            else break;
        }
        return results.ToArray();
    }


    public void Dispose()
    {
        Operator?.Dispose();
    }
}

