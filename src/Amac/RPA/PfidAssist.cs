using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.OCR;
using FMO.TPL;
using FMO.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.Playwright;
using MiniExcelLibs;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FMO.IO.AMAC;

public static class PfidAssist
{


    public static async Task<(bool Success, string Message)> CheckAndLogin(IPage page)
    {
        var locator = page.Locator("#menu-product");

        // 登陆
        if (await page.Locator("#username").IsVisibleAsync() || !await locator.IsVisibleAsync())
        {
            using var db = DbHelper.Base();
            var amacc = db.GetCollection<AmacAccount>().FindById("xinpi");

            // 设置账号密码
            await page.Locator("#username").FillAsync(amacc.Name);
            await page.Locator("#password").FillAsync(amacc.Password);
            await page.Locator("#captcha").FillAsync("");

            locator = page.Locator("#img_captcha");
            var buf = await locator.ScreenshotAsync();

            VerifyMessage verify = new VerifyMessage { Title = "请输入验证码", Type = VerifyType.Captcha, Image = buf, TimeOut = 1000 * 30 };
            try
            {
                WeakReferenceMessenger.Default.Send(verify);


                // 等待验证码，1分钟
                //if (!verify.Waiter.WaitOne(new TimeSpan(0, 1, 0)))
                //    return (false, "加载验证码失败");

                // 空
                if (string.IsNullOrWhiteSpace(verify.Code))
                {
                    WeakReferenceMessenger.Default.Send(new VerifyResultMessage(verify.Id, false, "未输入验证码"));
                    return (false, "未输入验证码");
                }

                // 验证登录
                await page.Locator("#captcha").FillAsync(verify.Code);
                await page.Locator("#btnLogin").ClickAsync();



                // 验证码错误
                locator = page.Locator("#message_login");
                if (await locator.IsVisibleAsync())
                {
                    WeakReferenceMessenger.Default.Send(new VerifyResultMessage(verify.Id, false, await locator.InnerTextAsync()));
                    return (false, "验证码错误");
                }
                return (true, "");
            }
            finally { verify.Waiter.Dispose(); }
        }
        else return (false, "未找到登陆界面");
    }



    private static async Task<bool> FillCaptcha(IPage page, int times = 3)
    {
        string last = "";
        for (int i = 0; i < times; i++)
        {
            var locator = page.Locator("#img_captcha");
            var buf = await locator.ScreenshotAsync();


            var code = await OCRWorker.VerifyCode(buf);
            if (code == last) return false;

            if (code.Length != 5)
            {
                await page.Mouse.ClickAsync(10, 10);
                await page.Locator("#captcha").FillAsync("");
                continue;
            }


            await page.Locator("#captcha").FillAsync(code);
            return true;
        }
        return false;
    }


    public static async Task<(bool Success, string Message)> CheckAndLoginAuto(IPage page)
    {
        var locator = page.Locator("#menu-product");

        // 登陆
        if (await page.Locator("#username").IsVisibleAsync() || !await locator.IsVisibleAsync())
        {
            using var db = DbHelper.Base();
            var amacc = db.GetCollection<AmacAccount>().FindById("xinpi");

            // 设置账号密码
            await page.Locator("#username").FillAsync(amacc.Name);
            await page.Locator("#password").FillAsync(amacc.Password);
            await page.Locator("#captcha").FillAsync("");

            try
            {
                var vef = await FillCaptcha(page);
                if (!vef)
                {
                    return (false, "未能成功识别验证码");
                }



                // 验证登录
                await page.Locator("#btnLogin").ClickAsync();



                // 验证码错误
                locator = page.Locator("#message_login");
                if (await locator.IsVisibleAsync())
                {
                    return (false, await locator.InnerTextAsync());
                }
                return (true, "");
            }
            finally { }
        }
        else return (false, "未找到登陆界面");
    }








    public static async Task<(bool IsSuccess, IList<PendingReportInfo> Pendings)> Query()
    {
        using var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge" });

        var fi = new FileInfo(@"config\pfid.ck");
        var context = fi.Exists ? await browser.NewContextAsync(new BrowserNewContextOptions { StorageStatePath = fi.FullName }) : await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync("https://pfid.amac.org.cn/");


        // 等待页面完全加载（包括图片、CSS、JS 等）
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 检查及登陆
        var login = await CheckAndLogin(page);


        return default;
    }




    /// <summary>
    /// 获取投资人账号
    /// </summary>
    /// <param name="amacc"></param>
    /// <returns></returns>
    public static async Task<PfidAccount[]> QueryInvestorAccounts(AmacAccount amacc)
    {
        // 获取账号
        //using var db = DbHelper.Base();
        //if( db.GetCollection<AmacAccount>().FindById("xinpi") is not AmacAccount amacc)
        //{
        //    return [];
        //} 

        using var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = false });

        var fi = new FileInfo(@"config\pfid.ck");
        var context = fi.Exists ? await browser.NewContextAsync(new BrowserNewContextOptions { StorageStatePath = fi.FullName }) : await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync("https://pfid.amac.org.cn/");

        // 等待页面完全加载（包括图片、CSS、JS 等）
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 检查登陆
        var locator = page.Locator("#menu-product");

        var login = await CheckAndLoginAuto(page);

        if (!login.Success) return [];
        try
        {

            // 检查登陆
            locator = page.Locator("#menu-product");
            if (!await locator.IsVisibleAsync())
                return [];

            // 登陆成功，保存cookie
            await browser.Contexts[0].StorageStateAsync(new BrowserContextStorageStateOptions { Path = Path.GetFullPath(@"config\pfid.ck") });

            // 定向披露
            var cusloc = page.Locator("#reportInvest").Filter(new LocatorFilterOptions { HasText = "投资者账号列表" });
            if (!await cusloc.IsVisibleAsync())
            {
                locator = page.Locator("aside >> dt").Filter(new LocatorFilterOptions { HasText = "定向披露" });
                await locator.ClickAsync();
            }

            if (!await cusloc.IsVisibleAsync())
                return [];

            await cusloc.ClickAsync();



            // 每页记录
            var frame = page.Frames.Last();
            // 等待页面完全加载（包括图片、CSS、JS 等）
            try { await frame.WaitForLoadStateAsync(LoadState.NetworkIdle); } catch { }

            locator = frame.Locator("select.select");
            var box = await locator.BoundingBoxAsync(new LocatorBoundingBoxOptions { Timeout = 1000 });

            if (!await locator.IsVisibleAsync())
                return [];

            IResponse? response = null;
            await page.RunAndWaitForResponseAsync(async () => await locator.SelectOptionAsync(new SelectOptionValue { Value = "-1" }), x =>
             {
                 if (x.Url.Contains("find-cr-investor-list.do"))
                 {
                     response = x;
                     return true;
                 }
                 return false;
             });

            if (response is null) return [];

            var json = await response.JsonAsync();


            var str = await response.TextAsync();
            var node = JsonNode.Parse(str);

            if (node?["total"] is JsonValue tot && tot.GetValue<int>() is int total && total > 0 && node?["aaData"] is JsonArray arr)
            {
                var da = arr.Deserialize<PfidAccountJson[]>();

                using var db = DbHelper.Base();
                var cus = db.GetCollection<Investor>().FindAll().ToArray();

                var res = new List<PfidAccount>(da!.Length);
                foreach (var item in da)
                {
                    if (cus.FirstOrDefault(x => x.Identity is not null && IsLike(x.Name, item.Name!) && IsLike(x.Identity.Id, item.Identity!)) is Investor investor)
                        res.Add(new PfidAccount(investor.Id, item.Account!));
                }

                return res.ToArray();
            }


        }
        catch (Exception e)
        {

        }


        return [];
    }


    public static async Task<(bool Success, string Message)> Login(IPage page, AmacAccount acc)
    {
        await page.GotoAsync("https://pfid.amac.org.cn/");

        // 等待页面完全加载（包括图片、CSS、JS 等）
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 检查登陆
        var locator = page.Locator("#menu-product");
        if (await locator.IsVisibleAsync())
            return (true, "");

        // 登陆
        if (await page.Locator("#username").IsVisibleAsync() || !await locator.IsVisibleAsync())
        {
            using var db = DbHelper.Base();
            var amacc = db.GetCollection<AmacAccount>().FindById("xinpi");

            // 设置账号密码
            await page.Locator("#username").FillAsync(amacc.Name);
            await page.Locator("#password").FillAsync(amacc.Password);
            await page.Locator("#captcha").FillAsync("");

            try
            {
                var vef = await FillCaptcha(page);
                if (!vef)
                {
                    return (false, "未能成功识别验证码");
                }

                // 验证登录
                await page.Locator("#btnLogin").ClickAsync();

                // 验证码错误
                locator = page.Locator("#message_login");
                if (await locator.IsVisibleAsync())
                {
                    return (false, await locator.InnerTextAsync());
                }
                return (true, "");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
        else return (false, "未找到登陆界面");
    }


    public static async Task<(bool Success, string Error)> UpdateInvestorAccounts(AmacAccount amac, bool full = false)
    {
        // 检查历史记录
        using var db = DbHelper.Base();
        var last = full ? default : db.GetCollection<PfidAccount>().Query().OrderByDescending(x => x.Update).FirstOrDefault()?.Update ?? default;
        if (last.Year > 1970) last = last.AddDays(-5);

        // 投资人新购或全部赎回
        var changed = db.GetCollection<InvestorFundEntry>().Query().Where(x => x.FirstBuy > last).ToList();

        if (changed.Count == 0) // 没有变动
            return (true, "");

        var cids = changed.Select(x => x.InvestorId).Distinct().ToList();
        var investors = db.GetCollection<Investor>().FindAll().ToList();
        var fcode = db.GetCollection<Fund>().Query().Select(x => new { x.Id, x.Code }).ToList();
        var fff = db.GetCollection<InvestorFundEntry>().Find(x => cids.Contains(x.InvestorId) && x.SellOut == default).Select(x => new { x.InvestorId, x.FundId }).
            Join(fcode, x => x.FundId, x => x.Id, (o, i) => new { o.InvestorId, i.Code }).GroupBy(x => x.InvestorId).Select(x => new { InvestorId = x.Key, Codes = x.Select(x => x.Code!).Distinct().ToList() }).
            Join(investors, x => x.InvestorId, x => x.Id, (o, i) => new PfidAccountInfo() { Id = i.Id, Name = i.Name, Type = i.Type, Identity = i.Identity!, Phone = i.Phone, Email = i.Email, FundCodes = o.Codes }).ToList();

        using var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = false });

        var fi = new FileInfo(@"config\pfid.ck");
        var context = fi.Exists ? await browser.NewContextAsync(new BrowserNewContextOptions { StorageStatePath = fi.FullName }) : await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var login = await Login(page, amac);

        if (!login.Success) return (false, "登陆失败");

        try
        {
            // 检查登陆
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var locator = page.Locator("#menu-product");
            if (!await locator.IsVisibleAsync())
                return (false, "未找到主菜单");

            // 登陆成功，保存cookie
            await browser.Contexts[0].StorageStateAsync(new BrowserContextStorageStateOptions { Path = Path.GetFullPath(@"config\pfid.ck") });

            // 定向披露
            var cusloc = page.Locator("#reportInvest").Filter(new LocatorFilterOptions { HasText = "投资者账号列表" });
            if (!await cusloc.IsVisibleAsync())
            {
                locator = page.Locator("aside >> dt").Filter(new LocatorFilterOptions { HasText = "定向披露" });
                await locator.ClickAsync();
            }

            if (!await cusloc.IsVisibleAsync())
                return (false, "未找到账号列表菜单");

            await cusloc.ClickAsync();



            // 每页记录
            var frame = page.Frames.Last();
            // 等待页面完全加载（包括图片、CSS、JS 等）
            try { await frame.WaitForLoadStateAsync(LoadState.NetworkIdle); } catch { }

            locator = frame.Locator("select.select");
            var box = await locator.BoundingBoxAsync(new LocatorBoundingBoxOptions { Timeout = 1000 });

            if (!await locator.IsVisibleAsync())
                return (false, "切换全部数据失败");

            IResponse? response = null;
            await page.RunAndWaitForResponseAsync(async () => await locator.SelectOptionAsync(new SelectOptionValue { Value = "-1" }), x =>
            {
                if (x.Url.Contains("find-cr-investor-list.do"))
                {
                    response = x;
                    return true;
                }
                return false;
            });

            if (response is null) return (false, "获取已知账号失败");

            var json = await response.JsonAsync();


            var str = await response.TextAsync();
            var node = JsonNode.Parse(str);
            var exist = new Dictionary<int, string>();
            if (node?["total"] is JsonValue tot && tot.GetValue<int>() is int total && total > 0 && node?["aaData"] is JsonArray arr && arr.Count > 0)
            {
                var da = arr.Deserialize<PfidAccountJson[]>();

                var cus = db.GetCollection<Investor>().FindAll().ToArray();

                try
                {
                    foreach (var item in da!)
                    {
                        if (cus.FirstOrDefault(x => x.Identity is not null && IsLike(x.Name, item.Name!) && IsLike(x.Identity.Id, item.Identity!)) is Investor investor)
                            exist[investor.Id] = item.Account!;
                    }
                }
                catch (Exception e)
                {

                }
            }

            // 合并
            foreach (var item in fff)
            {
                if (exist.TryGetValue(item.Id, out var value))
                    item.PfidAccount = value;
                else
                    item.PfidAccount = string.IsNullOrWhiteSpace(item.Phone) ? item.Email : item.Phone;
            }

            // 生成表格
            var gpath = Path.GetFullPath("temp\\pfidinv.xlsx");
            Tpl.Generate(gpath, @"files\tpl\pfid_investor.xlsx", new
            {
                i = fff.Select(x => new
                {
                    x.PfidAccount,
                    x.Name,
                    Type = EnumDescriptionTypeConverter.GetEnumDescription(x.Type),
                    IdType = x.Identity.Type == IDType.IdentityCard ? "身份证" : EnumDescriptionTypeConverter.GetEnumDescription(x.Identity.Type),
                    IdType2 = x.Identity.Other,
                    Id = x.Identity.Id,
                    Phone = string.IsNullOrWhiteSpace(x.Email) ? x.Phone : "",
                    x.Email,
                    Status = "启用",
                    Codes = x.FundCodes is null ? null : string.Join(',', x.FundCodes)
                })
            });

            // 上传
            locator = page.Frames[^1].Locator("a").Filter(new LocatorFilterOptions { HasText = "批量导入" });
            await locator.ClickAsync();

            frame = page.Frames[^1]; // 使用 name 属性

            if (frame == null)
                return (false, "无法找到 iframe");

            //   等待 iframe 加载完成
            await frame.WaitForLoadStateAsync();

            //  在 iframe 内等待文件输入框 #f_file
            var fileInput = await frame.WaitForSelectorAsync("#f_file", new FrameWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 2000
            });

            if (fileInput == null)
                return (false, "在 iframe 中未找到 #f_file 元素");

            //  上传文件（使用本地绝对路径） 
            await fileInput.SetInputFilesAsync(gpath);

            //  可选：等待后续操作完成（如上传进度、提示等）
            await page.WaitForTimeoutAsync(1000);

            // 提交
            await frame.Locator("#submitButton").ClickAsync();

            string targetText = "导入结束";
            ILocator? foundLocator = null;
            IFrame? targetFrame = null;

            // 最多等待30秒，轮询所有 frame 查找目标元素
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(30);

            while (DateTime.UtcNow - startTime < timeout)
            {
                foreach (var f in page.Frames)
                {
                    // 跳过 detached 的 frame
                    if (f.IsDetached) continue;

                    locator = f.GetByText(targetText, new() { Exact = false });

                    try
                    {
                        // 检查元素是否可见（Playwright 会自动等待元素出现）
                        await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 1000 });

                        // 找到了！
                        foundLocator = locator;
                        targetFrame = f;
                        break;
                    }
                    catch (TimeoutException)
                    {
                        // 元素未在当前 frame 中出现，继续
                        continue;
                    }
                }

                if (foundLocator != null) break;

                // 等待一小段时间再重试
                await Task.Delay(500);
            }

            if (foundLocator == null)
            {
                return (false, "未获取到返回信息");
            }

            string fullText = await foundLocator.InnerTextAsync();


            var match = Regex.Match(fullText, @"导入成功.?(\d+).?条.*导入失败.?(\d+).?条");
            if (match.Success)
            {
                int successCount = int.Parse(match.Groups[1].Value);
                int failCount = int.Parse(match.Groups[2].Value);
            }

            // 获取错误信息
            var downloadLink = targetFrame.Locator("a", new FrameLocatorOptions { HasText = "下载错误" });
            if (await downloadLink.IsVisibleAsync())
            {
                // ⭐ 开始监听下载事件
                var downloadTask = page.WaitForDownloadAsync();

                // 点击触发下载
                await downloadLink.ClickAsync();

                // 等待下载完成
                var download = await downloadTask;
                string errxlsx = Path.GetFullPath(@"temp\pfid_cus_error.xlsx");
                await download.SaveAsAsync(errxlsx);

                // 解析错误
                var errors = ParseError(errxlsx);
                foreach (var item in fff)
                {
                    item.Error = errors.TryGetValue(item.PfidAccount!, out var error) ? error : null;
                }

                db.GetCollection<PfidAccountInfo>().Upsert(fff);
            }

            // 删除
            File.Delete("temp\\pfidinv.xlsx");
            if (match.Success)
                return (true, $"同步完成，错误{match.Groups[2].Value}");
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }


        return default;
    }

     

    private static bool IsLike(string full, string star)
    {
        var arr = star.Split('*');

        return full.StartsWith(arr[0]) && full.EndsWith(arr[^1]);
    }


    public static Dictionary<string, string> ParseError(string path)
    {
        var rows = MiniExcel.Query(path, false, "投资者信息", ExcelType.XLSX);
        Dictionary<string, string> dic = [];
        foreach (var row in rows.Skip(1))
        {
            var id = row.A;
            if (string.IsNullOrWhiteSpace(id)) break;

            var err = row.K;
            dic.Add(id, err);
        }
        return dic;
    }

}

/// <summary>
/// pfid 账户
/// </summary>
public class PfidAccountInfo
{
    public int Id { get; set; }

    public string? PfidAccount { get; set; }

    public required string Name { get; set; }

    public required Identity Identity { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public List<string>? FundCodes { get; set; }

    public string? Error { get; set; }
    public AmacInvestorType Type { get; internal set; }
}


public class PfidAccountJson
{
    [JsonPropertyName("accountName")]
    public string? Account { get; set; }

    [JsonPropertyName("investorName")]
    public string? Name { get; set; }

    [JsonPropertyName("idNumber")]
    public string? Identity { get; set; }
}