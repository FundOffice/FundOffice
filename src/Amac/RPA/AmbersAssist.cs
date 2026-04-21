using CommunityToolkit.Mvvm.Messaging;
using FMO.Logging;
using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace FMO.IO.AMAC;

public static class AmbersAssist
{


    public static async Task<(IPlaywright pw, IBrowser browser, IPage page)> Prepare(bool load = false)
    {
        var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = false });

        var fi = new FileInfo(@"config\ambers.ck");
        var context = load && fi.Exists && (DateTime.Now - fi.LastWriteTime).TotalHours < 1 ? await browser.NewContextAsync(new BrowserNewContextOptions { StorageStatePath = fi.FullName }) : await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync("https://ambers.amac.org.cn/");


        return (pw, browser, page);
    }


    public static async Task<bool> Login(IPage page, string username, string password)
    {
        await page.FillAsync("#accountName", username);
        await page.FillAsync("#password", password);
        await page.ClickAsync("#btn-login1");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 安全登录弹窗
        var loc = page.Locator("#mb_btn_ok");
        if (await loc.CountAsync() == 1 && await loc.IsVisibleAsync())
            await loc.ClickAsync();

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            await page.Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = Path.GetFullPath(@"config\ambers.ck") });
        });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法


        loc = page.Locator("#log_yh1").GetByText("密码错误");
        return !await loc.IsVisibleAsync();
    }



    public static async Task<bool> UploadQuartlyInvestor(params FundQuarterlyUpdate[] reports)
    {
        Dictionary<string, string?> dic = null!;
        Dictionary<int, QuarterlyUpdateStatus> statuses = null!;
        AmacAccount acc = null!;
        using (var db = DbHelper.Base())
        {
            acc = db.GetCollection<AmacAccount>().FindById("ambers");
            if (string.IsNullOrWhiteSpace(acc?.Name) || string.IsNullOrWhiteSpace(acc?.Password))
            {
                LogEx.Error("AMAC账号信息不完整，请检查数据库");
                return false;
            }

            dic = db.GetCollection<Fund>().Query().Where(x => x.Code != null).Select(x => new { x.Code, x.AmacID }).ToArray().ToDictionary(x => x.Code!, x => x.AmacID);
            statuses = db.GetCollection<QuarterlyUpdateStatus>().Query().Where(Query.In("Id", reports.Select(x => new BsonValue(x.Id)))).ToArray().ToDictionary(x => x.Id, x => x);
        }

        var (pw, browser, page) = await Prepare(true);

        try
        {
            // 检查登录
            await Task.Delay(2000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);


            // 登录
            var loginResult = await IsLogin(page);
            if (!loginResult)
                loginResult = await Login(page, acc.Name, acc.Password);

            if (!loginResult)
            {
                LogEx.Error("AMAC登录失败，请检查账号信息");
                return false;
            }

            await Task.Delay(2000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return false;
        }

        try
        {
            await page.GotoAsync("https://ambers.amac.org.cn/web/app.html#/product/quarterUpdate");
            await Task.Delay(2000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var data = await CrawlAllFundReportAsync(page);


            var query = from p in data
                        join r in reports on p.ProductCode equals r.FundCode
                        where p is not null
                        select new
                        {
                            Id = r.Id,
                            Name = p.ProductName,
                            Code = r.FundCode,
                            FundId = r.FundId,
                            IsInvestorFilled = p.IsInvestorFilled,
                            IsSubmitted = p.IsSubmited,
                            IsOperationFilled = p.IsOperationFilled,
                            File = r.Investor,
                            Date = r.PeriodEnd
                        };


            // 更新状态表
            foreach (var item in query)
            {
                if (!statuses.ContainsKey(item.Id))
                    statuses.Add(item.Id, new QuarterlyUpdateStatus
                    {
                        Id = item.Id,
                        FundId = item.FundId,
                        PeriodEnd = item.Date,
                        IsSubmitted = item.IsSubmitted,
                        InvestorFill = new FillSection { IsFilled = item.IsInvestorFilled },
                        OperationFill = new FillSection { IsFilled = item.IsOperationFilled }
                    });
                else
                {
                    statuses[item.Id].IsSubmitted = item.IsSubmitted;
                    statuses[item.Id].InvestorFill.IsFilled = item.IsInvestorFilled;
                    statuses[item.Id].OperationFill.IsFilled = item.IsOperationFilled;
                }

                // 推送UI
            }

            // 遍历需要更新的报告
            foreach (var item in query)
            {
                if (item.IsSubmitted) continue;

                if (!item.IsInvestorFilled)
                {
                    if (!dic.TryGetValue(item.Code, out var id) || id!.Length < 10)
                    {
                        LogEx.Warning($"{item.Code} 上报季度更新时没有找到id，请先在基金总览页点击更新");
                        continue;
                    }
                    if (item.File?.File is null)
                    {
                        LogEx.Warning($"{item.Code} 上报季度更新时，投资者信息表不存在");
                        continue;
                    }

                    // 跳转到投资者信息编辑页
                    await page.GotoAsync($"https://ambers.amac.org.cn/web/app.html#/investorInfoEdit/{id}/{item.Date.Year}/{item.Date.Month / 3}");

                    if (!statuses.ContainsKey(item.Id))
                        statuses.Add(item.Id, new QuarterlyUpdateStatus { Id = item.Id, FundId = item.FundId, PeriodEnd = item.Date });

                    // 填报投资者信息表
                    await FillInvestorPage(page, item.FundId, item.Name, item.Date, statuses[item.Id]);

                    // 保存status
                    using (var db = DbHelper.Base())
                    {
                        db.GetCollection<QuarterlyUpdateStatus>().Upsert(statuses[item.Id]);
                    }

                    // 返回
                    try { await page.Locator("button:has-text('返回')").ClickAsync(); }
                    catch { }

                }

                if (!item.IsOperationFilled)
                {

                }

                if (statuses[item.Id].InvestorFill.IsFilled && statuses[item.Id].OperationFill.IsFilled)
                {
                    // 提交
                    try
                    {
                        await SubmitQuarterlyUpdateAsync(page, item.Code);

                        statuses[item.Id].IsSubmitted = true;
                    }
                    catch { }
                    // 保存status
                    using (var db = DbHelper.Base())
                    {
                        db.GetCollection<QuarterlyUpdateStatus>().Upsert(statuses[item.Id]);
                    }
                }




                //var sheet = $"temp\\{item.File.File.Name}";
                //File.Copy(item.File.File.GetFullPath(), sheet, true);

                //var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == item.FundId && x.ConfirmedDate < item.Date).ToArray();

                //// 排除已全部赎回的
                //var groupd = ta.GroupBy(x => x.InvestorId).Select(x => (id: x.Key, share: x.Sum(y => y.ShareChange()), saler: x.First().Agency)).Where(x => x.share > 0).ToDictionary(x => x.id, x => x);
                //var ids = groupd.Keys.Select(x => new BsonValue(x));
                //var investors = db.GetCollection<Investor>().Find(Query.In("_id", new BsonArray(ids))).ToList();
                //var fundName = db.FindFund(item.Code)?.Name ?? $"未知产品{item.Code}";

                //// 打包风揭
                //var packs = PackDiscloseSheets(investors, item.FundId, fundName, item.Date);

                //await UploadInvestorSheetAndZip(page, sheet, packs);





            }


            return true;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return false;
        }
        finally
        {
            pw.Dispose();
        }

    }


    private static async Task<bool> IsLogin(IPage page)
    {
        var locator = page.Locator("#mgr_register");

        try
        {
            // 等待元素 存在 + 可见 + 未被遮挡
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000 // 3秒超时，可改
            });

            // 额外校验：确保没有被遮挡（关键）
            var isEnabled = await locator.IsEnabledAsync();
            var isHidden = await locator.IsHiddenAsync();

            return !isHidden && isEnabled;
        }
        catch
        {
            // 超时/找不到/被隐藏/被遮挡 → 都会进这里
            return false;
        }
    }

    /// <summary>
    /// 打包风险揭示书
    /// </summary>
    private static string[] PackDiscloseSheets(List<Investor> data, int FundId, string FundName, DateOnly PeriodEnd)
    {
        using var db = DbHelper.Base();
        var orders = db.GetCollection<TransferOrder>().Find(x => x.FundId == FundId && x.Date < PeriodEnd).OrderByDescending(x => x.Date).ToArray();

        var ids = data.Select(x => x.Id).ToList();

        var d = orders.Where(x => x.RiskDiscloure?.File is not null).GroupBy(x => x.InvestorId).
            Where(x => ids.Contains(x.Key)).Select(x => x.First()).Select(x => (x.InvestorId, File: x.RiskDiscloure!.File!)).ToList();

        if (d.Count != data.Count)
        {
            LogEx.Warning($"{FundName} 生成季度更新风险揭示书数据不匹配，应有{data.Count}，实际{d.Count}");
            WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "风险揭示书数量不全"));
        }
        return ZipSplitter.CreateSplitZip(d.Select(x => x.File).ToArray(), "temp", $"{FundName}_风险揭示书_{PeriodEnd:yyyyMMdd}", 20 * 1024 * 1024);
    }

    /// <summary>
    /// 删除现有的投资人数据
    /// </summary>
    /// <param name="page"></param>
    /// <param name="delayMs"></param>
    /// <returns></returns>
    private static async Task BatchDeleteAllWithScrollAsync(IPage page, int delayMs = 200)
    {
        // 精准定位你的删除按钮
        var deleteLocator = page.Locator("table >> a[ng-click^='remove(investor)']:has-text('删除')");
        var count = await deleteLocator.CountAsync();

        if (count == 0)
        {
            return;
        }


        // 必须倒序删除！！！
        for (int i = count - 1; i >= 0; i--)
        {
            try
            {
                var delBtn = deleteLocator.Nth(i);

                // ====================== 关键：自动滚动到元素 ======================
                await delBtn.ScrollIntoViewIfNeededAsync();
                await page.WaitForTimeoutAsync(200); // 滚动后小停顿，更稳定

                // 等待按钮可见可用
                await delBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

                // 点击删除
                await delBtn.ClickAsync();

                // 如果有确认弹窗，打开下面这段，注释上面 Click
                /*
                await page.RunAndWaitForDialogAsync(async () =>
                {
                    await delBtn.ClickAsync();
                }, dialog =>
                {
                    dialog.AcceptAsync();
                    return Task.CompletedTask;
                });
                */

                Console.WriteLine($"已删除第 {i + 1} 条");
                await page.WaitForTimeoutAsync(delayMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除第 {i + 1} 条失败：{ex.Message}");
            }
        }

        Console.WriteLine("✅ 批量删除全部完成！");
    }


    /// <summary>
    /// 填报投资人页
    /// </summary>
    /// <param name="page"></param>
    /// <param name="fundId"></param>
    /// <returns></returns>
    private static async Task<bool> FillInvestorPage(IPage page, int FundId, string FundName, DateOnly PeriodEnd, QuarterlyUpdateStatus status)
    {
        try
        {
            // 检查是否是填报状态
            var importBtn = page.GetByRole(AriaRole.Button, new() { Name = "模板导入" });
            try { await importBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 2000 }); }
            catch // 如果没有找到上传按钮，说明不是填报状态，可能是已提交/未到填报时间/其他异常，直接返回
            {
                status.InvestorFill.IsFilled = true;
                return false;
            }

            status.InvestorFill.ClearErrors();
            // 检查是否有投资人数据
            var deleteLocator = page.Locator("table >> a[ng-click^='remove(investor)']:has-text('删除')");
            if (await deleteLocator.CountAsync() > 0)
            {
                // 有数据，手动填报
                status.InvestorFill.LastUpdated = DateTime.Now;
                status.InvestorFill.IsFilled = false;
                status.InvestorFill.AddError("已在系统中检测到投资人数据，自动填报失败，请手动填报");

                return false;
            }

            // 检查并生成投资者信息表
            string? sheet = null;
            string[] zip = [];
            using (var db = DbHelper.Base())
            {
                var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == FundId && x.ConfirmedDate < PeriodEnd).ToArray();

                // 排除已全部赎回的
                var groupd = ta.GroupBy(x => x.InvestorId).Select(x => (id: x.Key, share: x.Sum(y => y.ShareChange()), saler: x.First().Agency)).Where(x => x.share > 0).ToDictionary(x => x.id, x => x);
                var ids = groupd.Keys.Select(x => new BsonValue(x));
                var data = db.GetCollection<Investor>().Find(Query.In("_id", new BsonArray(ids))).ToList();
                var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster).Name;

                // 数据校验
                var nv = db.GetDailyCollection(FundId).Find(x => x.Date <= PeriodEnd).LastOrDefault();
                if (nv is null || nv.Share != groupd.Sum(x => x.Value.share))
                {
                    status.InvestorFill.LastUpdated = DateTime.Now;
                    status.InvestorFill.IsFilled = false;
                    status.InvestorFill.AddError($"基金份额异常，生成的投资者信息表可能不正确！！");
                    return false;
                }

                // 生成投资者信息表
                sheet = GenerateInvestorSheet(FundId, PeriodEnd, data, manager, groupd);
                if (sheet is null)
                {
                    status.InvestorFill.LastUpdated = DateTime.Now;
                    status.InvestorFill.IsFilled = false;
                    status.InvestorFill.AddError($"投资者信息表生成失败，无法上传");
                    return false;
                }

                var orders = db.GetCollection<TransferOrder>().Find(x => x.FundId == FundId && x.Date < PeriodEnd).OrderByDescending(x => x.Date).ToArray();
                var cids = data.Select(x => x.Id).ToList();
                var d = orders.Where(x => x.RiskDiscloure?.File is not null).GroupBy(x => x.InvestorId).
                    Where(x => cids.Contains(x.Key)).Select(x => x.First()).Select(x => (x.InvestorId, File: x.RiskDiscloure!.File!)).ToList();

                if (d.Count != data.Count)
                    status.InvestorFill.AddError("风险揭示书数量不全");

                zip = ZipSplitter.CreateSplitZip(d.Select(x => x.File).ToArray(), "temp", $"{FundName}_风险揭示书_{PeriodEnd:yyyyMMdd}", 20 * 1024 * 1024);

                // 检查文件大小
                if (new FileInfo(zip[0]).Length < 100 * 1024)
                {
                    status.InvestorFill.LastUpdated = DateTime.Now;
                    status.InvestorFill.IsFilled = false;
                    status.InvestorFill.AddError($"风险揭示书文件异常，大小仅 {new FileInfo(zip[0]).Length / 1024} KB，请检查文件后重试");
                    return false;
                }
            }


            // 👇 核心：点击按钮 + 拦截文件选择器
            // <button class="btn btn-primary" type="button" ng-click="importExl()">模板导入</button>
            var fileChooser = await page.RunAndWaitForFileChooserAsync(async () =>
            {
                await page.Locator("button:has-text('模板导入')").ClickAsync();
            });

            // 给选择器设置文件（自动绕过系统弹窗）
            await fileChooser.SetFilesAsync(Path.GetFullPath(sheet));


            // 删除文件
            File.Delete(sheet);

            importBtn = page.Locator("#importBtn");
            await importBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await importBtn.ClickAsync();

            var errmsg = page.Locator("div.tab-content.m-info-table >> div.erroMess-content");
            if (await errmsg.IsVisibleAsync())
            {
                // 上传失败
                status.InvestorFill.LastUpdated = DateTime.Now;
                status.InvestorFill.IsFilled = false;
                status.InvestorFill.AddError($"上传失败，有错误\n{await errmsg.InnerTextAsync()}");
                return false;
            }


            if(!await CheckSubmitSuccessAndCloseModalAsync(page, "导入数据成功"))
            {
                status.InvestorFill.LastUpdated = DateTime.Now;
                status.InvestorFill.IsFilled = false;
                status.InvestorFill.AddError($"上传成功，但是没有提交成功，请查看log");

                return false;
            }

            // 风揭
            if (zip.Length > 0)
            {
                var riskFileList = page.Locator("ul.data-list li:visible");
                var deleteButtons = riskFileList.Locator("button", new() { HasText = "删除" });

                if (await deleteButtons.CountAsync() > 0)
                {
                    var deleteCount = await deleteButtons.CountAsync();
                    for (int i = 0; i < deleteCount; i++)
                    {
                        // 每次都重新获取，避免DOM刷新导致元素失效
                        await deleteButtons.First.ClickAsync();
                        await page.WaitForTimeoutAsync(500);
                    }
                }

                // ==============================================
                // 步骤3：上传多个风险揭示书文件（zip/pdf等）
                // ==============================================
                var riskUploadInput = page.Locator("#valid_PA008");
                await riskUploadInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 5000 });

                // 批量上传
                await riskUploadInput.SetInputFilesAsync(zip);
                await page.WaitForTimeoutAsync(2000);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // 提交
                var submitBtn = page.Locator("button:has-text('填报完成')");
                await submitBtn.ClickAsync();

                status.InvestorFill.LastUpdated = DateTime.Now;
                status.InvestorFill.IsFilled = true;
                return false;
            }
        }
        catch (Exception e)
        {
            // 上传失败
            status.InvestorFill.LastUpdated = DateTime.Now;
            status.InvestorFill.IsFilled = false;
            status.InvestorFill.Error = $"上传失败，程序异常{e.Message}";
            LogEx.Error(e);
            return false;
        }

        return true;
    }


    private static string? GenerateInvestorSheet(int FundId, DateOnly PeriodEnd, List<Investor> data, string manager, Dictionary<int, (int id, decimal share, string? saler)> groupd)
    {
        try
        {
            var path = @$"files\tpl\ambers_investor.xlsx";

            if (!File.Exists(path))
            {
                WeakReferenceMessenger.Default.Send(new ToastMessage(LogLevel.Warning, "投资人表模板不存在，无法生成"));
                return null;
            }


            // 写入
            var outp = @$"temp\{FundId}_investor_{PeriodEnd:yyyyMMdd}.xlsx";

            var obj = new
            {
                i = data.Select(x => new
                {
                    Type = x.Type.ToAmacString(),
                    Name = x.Name,
                    IDType = x.Identity!.Type.ToAmacString(),
                    IDType2 = x.Identity?.Other,
                    ID = x.Identity?.Id,
                    Share = (groupd[x.Id].share / 10000).ToString(),
                    Saler = groupd[x.Id].saler?.Contains("直销") ?? true ? manager : groupd[x.Id].saler
                })
            };

            Tpl.Generate(outp, path, obj);
            return outp;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return null;
        }
    }




    /// <summary>
    /// 自动翻页采集基金报表数据（直到找到过期数据/无下一页）
    /// </summary>
    public static async Task<List<PrivateFundQuarterReport>> CrawlAllFundReportAsync(IPage page)
    {
        var allData = new List<PrivateFundQuarterReport>();

        while (true)
        {
            // 等待表格加载
            await page.WaitForSelectorAsync("tbody tr.ng-scope", new() { Timeout = 5000 });

            // 解析当前页
            var currentPageData = await ParseFundReportTableAsync(page);
            allData.AddRange(currentPageData);

            // 判断：本页是否【全部都是未来截止日】→ 需要翻页
            bool allFuture = currentPageData.All(x => x.ReportDeadline > DateTime.Now);
            if (!allFuture)
                break;


            // ============== 定位【下一页】 ==============
            var nextPageItem = page.Locator("li:has(a:contains('下一页'))");

            // ============== 判断是否禁用（最后一页） ==============
            bool isDisabled = (await nextPageItem.GetAttributeAsync("class"))?.Contains("disabled") ?? true;

            if (isDisabled)
                break;

            // ============== 点击下一页 ==============
            await nextPageItem.Locator("a").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(800); // 等待 Angular 渲染
        }

        // 只保留最近一批
        var last = allData.Max(x => x.ReportDeadline);

        return allData.Where(x => x.ReportDeadline == last).ToList();
    }

    /// <summary>
    /// 解析AMAC私募产品季度报告表格
    /// </summary>
    /// <param name="page">Playwright Page对象</param>
    /// <returns>解析后的表格数据列表</returns>
    public static async Task<List<PrivateFundQuarterReport>> ParseFundReportTableAsync(IPage page)
    {
        var reportList = new List<PrivateFundQuarterReport>();
        var rows = await page.Locator("tbody tr.ng-scope").AllAsync();

        foreach (var row in rows)
        {
            var cells = await row.Locator("td").AllAsync();
            if (cells.Count < 11) continue;

            var report = new PrivateFundQuarterReport();

            // 1. 序号
            report.SerialNumber = int.TryParse(await cells[0].TextContentAsync(), out int sn) ? sn : 0;
            // 2. 产品名称
            report.ProductName = (await cells[1].TextContentAsync())?.Trim() ?? string.Empty;
            // 3. 产品编码
            report.ProductCode = (await cells[2].TextContentAsync())?.Trim() ?? string.Empty;

            // 4. 投资者信息更新：状态(bool) + 链接
            var investorCell = cells[3];
            var investorLink = investorCell.Locator("a");
            report.InvestorUpdateUrl = await investorLink.GetAttributeAsync("href") ?? string.Empty;
            var investorText = await investorLink.TextContentAsync() ?? string.Empty;
            report.IsInvestorFilled = investorText.Contains("已填报");

            // 5. 运行信息更新：状态(bool) + 链接
            var operationCell = cells[4];
            var operationLink = operationCell.Locator("a");
            report.OperationUpdateUrl = await operationLink.GetAttributeAsync("href") ?? string.Empty;
            var operationText = await operationLink.TextContentAsync() ?? string.Empty;
            report.IsOperationFilled = operationText.Contains("已填报");

            // 6. 报告基准日
            report.ReportBaseDate = (await cells[5].TextContentAsync())?.Trim() ?? string.Empty;
            // 7. 报送截止日
            report.ReportDeadline = DateTime.TryParse(await cells[6].TextContentAsync(), out DateTime deadline)
                ? deadline
                : DateTime.MinValue;
            // 8. 提交次数
            report.SubmitCount = int.TryParse(await cells[7].TextContentAsync(), out int count) ? count : 0;
            // 9. 倒计时
            report.CountdownDays = (await cells[8].TextContentAsync())?.Trim() ?? string.Empty;
            // 10. 填报日期
            report.FillDate = (await cells[9].TextContentAsync())?.Trim() ?? string.Empty;
            // 11. 提交状态
            report.IsSubmited = (await cells[10].TextContentAsync())?.Trim() switch { "已提交" => true, _ => false };
            // 12. 操作按钮
            var operationBtn = cells[11].Locator("a");
            report.OperationText = await operationBtn.CountAsync() > 0
                ? (await operationBtn.TextContentAsync())?.Trim() ?? string.Empty
                : string.Empty;

            reportList.Add(report);
        }
        return reportList;
    }

    /// <summary>
    /// 根据产品编码提交季度报告：先直接查找，找不到则自动搜索后再提交
    /// </summary>
    /// <param name="page">Playwright 页面</param>
    /// <param name="code">产品编码（第三列匹配）</param>
    /// <exception cref="KeyNotFoundException">始终未找到产品时抛出</exception>
    /// <exception cref="InvalidOperationException">找到产品但无提交按钮时抛出</exception>
    public static async Task<bool> SubmitQuarterlyUpdateAsync(IPage page, string code)
    {
        // 1. 等待表格加载
        await page.WaitForSelectorAsync("tbody tr.ng-scope", new() { Timeout = 5000 });

        // 2. 先尝试直接在当前页查找并提交
        bool submitted = await TrySubmitInCurrentPageAsync(page, code);
        if (submitted)
            return true;

        // 3. 当前页没找到 → 执行搜索
        await SearchByKeywordInQuartlyUpdatePageAsync(page, code);

        // 4. 搜索完成后，再次尝试提交
        submitted = await TrySubmitInCurrentPageAsync(page, code);
        if (submitted)
            return true;

        return false;
    }

    /// <summary>
    /// 在当前页尝试查找产品并提交
    /// </summary>
    private static async Task<bool> TrySubmitInCurrentPageAsync(IPage page, string code)
    {
        try
        {
            var rows = page.Locator("tbody tr.ng-scope");
            var count = await rows.CountAsync();

            for (int i = 0; i < count; i++)
            {
                var row = rows.Nth(i);
                var codeCell = row.Locator("td >> nth=2"); // 第三列：产品编码
                var currentCode = (await codeCell.TextContentAsync())?.Trim() ?? string.Empty;

                // 获取当前行所有单元格
                var cells = await row.Locator("td").AllAsync();
                if (cells.Count < 3) continue; // 至少有第三列

                // 第三列产品编码
                if (!currentCode.Equals(code, StringComparison.OrdinalIgnoreCase))
                    continue;

                // ==============================================
                // ✅ 正确写法：先获取列数 → 再取最后一列（你要的逻辑）
                // ==============================================
                int columnCount = cells.Count;
                var lastColumn = cells[columnCount - 1]; // 最后一列

                // 点击最后一列里的 a 标签按钮
                var submitBtn = lastColumn.Locator("a");
                if (await submitBtn.CountAsync() > 0)
                {
                    await submitBtn.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await Task.Delay(800);


                    return await CheckSubmitSuccessAndCloseModalAsync(page, "提交成功");

                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// 使用页面搜索框根据关键字（产品编码）搜索
    /// </summary>
    private static async Task SearchByKeywordInQuartlyUpdatePageAsync(IPage page, string keyword)
    {
        try
        {
            // 清空搜索框 → 输入编码 → 点击查询
            var keywordInput = page.Locator("#keyword");
            await keywordInput.FillAsync(string.Empty);
            await keywordInput.FillAsync(keyword);

            // 点击查询按钮
            var searchBtn = page.Locator("button.btn-query");
            await searchBtn.ClickAsync();

            // 等待搜索结果加载
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.WaitForSelectorAsync("tbody tr.ng-scope", new() { Timeout = 8000 });
            await Task.Delay(800);
        }
        catch (Exception ex)
        {
            //throw new InvalidOperationException("搜索操作失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 验证提交成功弹窗，并点击确认关闭
    /// </summary>
    private static async Task<bool> CheckSubmitSuccessAndCloseModalAsync(IPage page, string regex)
    {
        try
        {
            // 等待弹窗出现（3秒超时）
            var modal = page.Locator("div.modal-dialog");
            if (await modal.CountAsync() == 0)
                return true;

            await modal.WaitForAsync(new() { Timeout = 3000 });

            // 检查是否出现“提交成功”文本
            var message = modal.Locator("div.modal-body .alert");
            var messageText = (await message.TextContentAsync())?.Trim() ?? string.Empty;

            if (!Regex.IsMatch(messageText, regex, RegexOptions.IgnoreCase))
            {
                LogEx.Error($"Ambers 弹窗文本不匹配正则：{regex}，实际文本：{messageText}");
                return false;
            }

            // 点击【确认】按钮关闭弹窗
            var confirmBtn = modal.Locator("div.modal-footer button.btn-primary");
            await confirmBtn.ClickAsync();

            // 等待弹窗消失
            await confirmBtn.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 2000 });
            return true;
        }
        catch (Exception ex)
        {
            LogEx.Error(ex);
            return false;
        }
    }
}




/// <summary>
/// 私募证券投资基金季度报告表格实体
/// </summary>
public class PrivateFundQuarterReport
{
    /// <summary>
    /// 序号
    /// </summary>
    public int SerialNumber { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    public string ProductName { get; set; }

    /// <summary>
    /// 产品编码
    /// </summary>
    public string ProductCode { get; set; }

    /// <summary>
    /// 投资者信息更新是否已填报（bool）
    /// </summary>
    public bool IsInvestorFilled { get; set; }

    /// <summary>
    /// 投资者信息更新 填报链接（未填报时保存地址）
    /// </summary>
    public string? InvestorUpdateUrl { get; set; }

    /// <summary>
    /// 运行信息更新是否已填报（bool）
    /// </summary>
    public bool IsOperationFilled { get; set; }

    /// <summary>
    /// 运行信息更新 填报链接（未填报时保存地址）
    /// </summary>
    public string? OperationUpdateUrl { get; set; }

    /// <summary>
    /// 报告基准日
    /// </summary>
    public string ReportBaseDate { get; set; }

    /// <summary>
    /// 报送截止日
    /// </summary>
    public DateTime ReportDeadline { get; set; }

    /// <summary>
    /// 提交次数
    /// </summary>
    public int SubmitCount { get; set; }

    /// <summary>
    /// 倒计时（天）
    /// </summary>
    public string CountdownDays { get; set; }

    /// <summary>
    /// 操作（填报/提交/重报按钮文本）
    /// </summary>
    public string OperationText { get; set; }

    /// <summary>
    /// 提交状态（已提交/未提交）
    /// </summary>
    public bool IsSubmited { get; set; }

    /// <summary>
    /// 填报日期（运行信息更新填报时间）
    /// </summary>
    public string FillDate { get; set; }
}