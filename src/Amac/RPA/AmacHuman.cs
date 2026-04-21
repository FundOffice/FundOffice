using FMO.IO.AMAC.JsonModels;
using FMO.Models;
using Microsoft.Playwright;
using Serilog;
using System.Text.Json;


namespace FMO.IO.AMAC;

public static class AmacHuman
{
    public static async Task<(AmacReturn Code, Participant[] Data)> GetParticipants(string user, string password)
    {
        using var pw = await Playwright.CreateAsync();
#if DEBUG
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = false });
#else 
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge" });
#endif   
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(10 * 1000);
        await page.GotoAsync("https://human.amac.org.cn/");

        // 关闭弹窗
        try
        {
            //var locator = page.Locator("div.dialog-wrapper");
            //if (await locator.CountAsync() > 0)
            //    await locator.ClickAsync();

            var locator = page.GetByRole(AriaRole.Button, new() { Name = "关闭" });

            foreach (var item in await locator.AllAsync())
                await item.ClickAsync();

        }
        catch (Exception ex)
        {
            Log.Error($"获取管理人成员，关闭弹窗出错{ex}");
        }

        // 设置账户密码

        try
        {
            await page.Locator("#accountTypeSelect").SelectOptionAsync(new[] { "1" });
            await page.Locator("#user").FillAsync(user);
            await page.Locator("#psw").FillAsync(password);

            await page.WaitForTimeoutAsync(1000);
            await page.GetByRole(AriaRole.Button, new() { Name = "登录" }).ClickAsync();

            var locator = page.GetByText("密码错误", new() { Exact = false });

            if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync())
            {
                return (AmacReturn.AccountError, []);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"获取管理人成员，登录错误{ex}");
            return (AmacReturn.Browser, []);
        }

        // 判断是否登录成功
        try
        {
            var locator = page.GetByText("系统管理", new() { Exact = false });
            await locator.WaitForAsync(new LocatorWaitForOptions { Timeout = 20 * 1000 });
            if (await locator.CountAsync() == 0)
            {
                Log.Error($"获取管理人成员，无法验证登录结果");
                return (AmacReturn.Browser, []);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"获取管理人成员，无法验证登录结果{ex}");
            return (AmacReturn.Browser, []);
        }

        // 
        List<Participant> participants = new();
        try
        {
            await page.GotoAsync("https://human.amac.org.cn/web/#/registerManager");
            var locator = page.GetByText("每页行数").Locator("..").Locator("div >> div[role='button']");
            var cnt = await locator.CountAsync();
            await locator.ScrollIntoViewIfNeededAsync();
            await locator.ClickAsync();

            locator = page.Locator("li[role='option'][data-value='100']");

            do
            {
                IResponse? response = null;
                await page.RunAndWaitForResponseAsync(async () => await locator.ClickAsync(), resp =>
                {
                    if (!resp.Url.StartsWith("https://human.amac.org.cn/web/api/web-user/")) return false;
                    response = resp;
                    return true;
                });


                if (response is null) return (AmacReturn.InvalidResponse, []);

                var json = await response.TextAsync();
                var obj = JsonSerializer.Deserialize<EmployeeRoot>(json);

                if (obj?.list is null) return (AmacReturn.InvalidResponse, []);

                participants.AddRange(obj.list.Select(x => new Participant
                {
                    Name = x.username,
                    Email = x.email,
                    Phone = x.mobile,
                    CertCode = x.certCode,
                    Post = x.post,
                    Identity = new Identity { Id = x.idNumber!, Type = x.idType switch { "1" => IDType.IdentityCard, _ => IDType.Unknown } },
                }));

                locator = page.GetByText("每页行数").Locator(" .. >> .. >> div >> button");
                cnt = await locator.CountAsync();
            }
            while (!await locator.Last.IsDisabledAsync());
        }
        catch (Exception e)
        {

        }


        return (AmacReturn.Success, participants.ToArray());
    }
}
