using FMO.AMAC.Direct;
using FMO.IO.AMAC;
using FMO.Logging;
using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using LiteDB;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace FMO.Disclosure;

public class QuarterlyUpdateChannel : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.QuarterlyUpdate;

    public string Name => "季度更新";

    public string Description => "季度更新";

    public IWorkConfig? DefaultWorkConfig(DisclosureType type) => null;


    private IPlaywright? playwright;
    private IPage? page;

    private DateTime _lastAccess;

    private async Task<ErrorReturn> Prepare()
    {
        using var db = DbHelper.Base();

        var acc = db.GetCollection<AmacAccount>().FindById("ambers");
        if (string.IsNullOrWhiteSpace(acc?.Name) || string.IsNullOrWhiteSpace(acc?.Password))
            return new(false, "AMAC账号信息不完整，请检查数据库");


        if (playwright is null)
        {
            var d = await AmbersAssist.Prepare(true);
            playwright = d.pw;
            page = d.page;
        }

        StartIdleMonitor();

        try
        {
            if (page is null)
                return new(false, "无法启动RPA");

            // 检查登录
            await Task.Delay(2000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);


            // 登录
            var loginResult = await AmbersAssist.IsLogin(page);
            if (!loginResult)
                loginResult = await AmbersAssist.Login(page, acc.Name, acc.Password);

            if (!loginResult)
                return new(false, "AMAC登录失败，请检查账号信息");

            await Task.Delay(2000);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return new(true);
        }
        catch (Exception e)
        {
            return new(false, e.Message);
        }

    }

    // 闲置监测后台任务
    private void StartIdleMonitor()
    {
        // 用弃元消除 CS4014，这是后台任务，正确用法
        _ = Task.Run(async () =>
        {
            // 每 10 分钟检查一次
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

            try
            {
                while (await timer.WaitForNextTickAsync())
                {
                    // 无实例直接退出循环
                    if (playwright is null)
                        break;

                    if (DateTime.Now - _lastAccess > TimeSpan.FromMinutes(10))
                    {
                        LogEx.Information("RPA闲置超过10分钟，自动关闭");

                        // 线程安全清空
                        lock (this)
                        {
                            playwright?.Dispose();
                            playwright = null;
                            page = null;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogEx.Error(ex, "闲置监测任务异常");
            }
        });
    }

    public async Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig? config)
    {
        _lastAccess = DateTime.Now;

        if (Notice is not QuarterlyUpdate qu)
            return new(false, "不支持的披露类型");


        var r = await Prepare();
        if (!r.Successed || page is null)
            return r;


        // 获取更新状态
        try
        {
            await NavigateAndFilterPage(qu);

            // 等待表格加载
            var report = await GetFundUpdateStatus(page);
            if (report.IsSubmited) return new(true);

            if (!report.IsInvestorFilled)
                await FillInvesterSheet(page, qu);

            if (!report.IsOperationFilled)
                await FillOperation(qu);

            // 再次获取状态，确保最新
            await NavigateAndFilterPage(qu);
            report = await GetFundUpdateStatus(page);
            if (!report.IsInvestorFilled && !report.IsOperationFilled)
                return new(false, "投资人表和运营表都未填报成功，请检查RPA操作日志");
            if (!report.IsInvestorFilled)
                return new(false, "投资人表未填报成功，请检查RPA操作日志");
            if (!report.IsOperationFilled)
                return new(false, "运营表未填报成功，请检查RPA操作日志");

            if (!report.IsSubmited)
                return await TrySubmitInCurrentPageAsync(page);

            return new(true);
        }
        catch (Exception e)
        {
            return new(false, e.Message);
        }
    }

    private async Task NavigateAndFilterPage(QuarterlyUpdate qu)
    {
        await page!.GotoAsync("https://ambers.amac.org.cn/web/app.html#/product/quarterUpdate");
        await Task.Delay(2000);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 搜索
        await page.FillAsync("#keyword", qu.FundCode);
        await page.ClickAsync("button:contains('查询')");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task<ErrorReturn> FillOperation(QuarterlyUpdate qu)
    {
        // 上报运营表
        using var db = DbHelper.Base();
        var cc = db.GetCollection<IDisclosureChannelConfig>().FindById(Code) as QuarterlyUpdateChannelConfig;
        if (cc is null) return new(false, "配置不正确");
        return await AmacDirectReporter.DislosurePeriodical(qu, new AmacDirectAccount(cc.UserName, cc.Password, cc.Secret));
    }

    private async Task<ErrorReturn> FillInvesterSheet(IPage page, QuarterlyUpdate qu)
    {
        try
        {
            var locator = page.Locator("table.table-dashed.table-center >> tbody > tr:nth-child(1) > td:nth-child(4)");
            await locator.ClickAsync(new() { Timeout = 2000 });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // 检查是否是填报状态
            var importBtn = page.GetByRole(AriaRole.Button, new() { Name = "模板导入" });
            try { await importBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 2000 }); }
            catch // 如果没有找到上传按钮，说明不是填报状态，可能是已提交/未到填报时间/其他异常，直接返回
            {
                return new(false, "没有找到【模板导入】");
            }

            // 检查是否有投资人数据
            var deleteLocator = page.Locator("table >> a[ng-click^='remove(investor)']:has-text('删除')");
            if (await deleteLocator.CountAsync() > 0)
                return new(false, "已在系统中检测到投资人数据，自动填报失败，请手动填报");

            // 检查并生成投资者信息表
            var (ok, sheet, zip, error) = PrepareInvestorData(qu);
            if (!ok) return new(false, error);



            // 👇 核心：点击按钮 + 拦截文件选择器
            // <button class="btn btn-primary" type="button" ng-click="importExl()">模板导入</button>
            var fileChooser = await page.RunAndWaitForFileChooserAsync(async () =>
            {
                await page.Locator("button:has-text('模板导入')").ClickAsync();
            });

            // 给选择器设置文件（自动绕过系统弹窗）
            await fileChooser.SetFilesAsync(Path.GetFullPath(sheet!));


            importBtn = page.Locator("#importBtn");
            await importBtn.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await importBtn.ClickAsync();

            var errmsg = page.Locator("div.tab-content.m-info-table >> div.erroMess-content");
            if (await errmsg.IsVisibleAsync())
                return new(false, $"上传失败，有错误\n{await errmsg.InnerTextAsync()}");


            if (!await CheckSubmitSuccessAndCloseModalAsync(page, "导入数据成功"))
                return new(false, "上传成功，但是没有提交成功，请查看log");

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

            }
            return new(true);
        }
        catch (Exception e)
        {
            return new(false, e.Message);
        }
    }



    private async Task<PrivateFundQuarterReport> GetFundUpdateStatus(IPage page)
    {
        await page.WaitForSelectorAsync("tbody tr.ng-scope", new() { Timeout = 5000 });
        var firstRow = page.Locator("tbody tr.ng-scope").Nth(0);
        var cells = await firstRow.Locator("td").AllAsync();

        var investorCell = cells[3];
        var investorLink = investorCell.Locator("a");
        var operationBtn = cells[11].Locator("a");
        var investorText = await investorLink.TextContentAsync() ?? string.Empty; var operationCell = cells[4];
        var operationLink = operationCell.Locator("a");
        var operationText = await operationLink.TextContentAsync() ?? string.Empty;
        var report = new PrivateFundQuarterReport
        {
            // 1. 序号
            SerialNumber = int.TryParse(await cells[0].TextContentAsync(), out int sn) ? sn : 0,
            // 2. 产品名称
            ProductName = (await cells[1].TextContentAsync())?.Trim() ?? string.Empty,
            // 3. 产品编码
            ProductCode = (await cells[2].TextContentAsync())?.Trim() ?? string.Empty,

            // 4. 投资者信息更新：状态(bool) + 链接
            InvestorUpdateUrl = await investorLink.GetAttributeAsync("href") ?? string.Empty,
            IsInvestorFilled = investorText.Contains("已填报"),

            // 5. 运行信息更新：状态(bool) + 链接

            OperationUpdateUrl = await operationLink.GetAttributeAsync("href") ?? string.Empty,
            IsOperationFilled = operationText.Contains("已填报"),

            // 6. 报告基准日
            ReportBaseDate = (await cells[5].TextContentAsync())?.Trim() ?? string.Empty,
            // 7. 报送截止日
            ReportDeadline = DateTime.TryParse(await cells[6].TextContentAsync(), out DateTime deadline)
            ? deadline
            : DateTime.MinValue,
            // 8. 提交次数
            SubmitCount = int.TryParse(await cells[7].TextContentAsync(), out int count) ? count : 0,
            // 9. 倒计时
            CountdownDays = (await cells[8].TextContentAsync())?.Trim() ?? string.Empty,
            // 10. 填报日期
            FillDate = (await cells[9].TextContentAsync())?.Trim() ?? string.Empty,
            // 11. 提交状态
            IsSubmited = (await cells[10].TextContentAsync())?.Trim() switch { "已提交" => true, _ => false },
            // 12. 操作按钮
            OperationText = await operationBtn.CountAsync() > 0
            ? (await operationBtn.TextContentAsync())?.Trim() ?? string.Empty
            : string.Empty,
        };

        return report;
    }

    public bool IsSupported(DisclosureType type) => type == DisclosureType.QuarterlyUpdate;

    public bool RequireConfigWork(DisclosureType type) => false;

    public ErrorReturn VerifyNotice(IDisclosureNotice Notice)
    {
        return Notice is QuarterlyUpdate qu && (qu.Operation?.File?.Exists ?? false)
            ? new ErrorReturn(true, null) : new ErrorReturn(false, "季度更新必须包含运营表");
    }


    private (bool success, string? sheet, string[] zip, string? error) PrepareInvestorData(QuarterlyUpdate qu)
    {
        try
        {
            var path = @"ambers_investor.xlsx";

            var old = qu.Investor?.File;
            using var db = DbHelper.Base();
            var ta = db.GetCollection<TransferRecord>().Find(x => x.FundId == qu.FundId && x.ConfirmedDate < qu.ReportDate).ToArray();

            // 排除已全部赎回的
            var groupd = ta.GroupBy(x => x.InvestorId).Select(x => (id: x.Key, share: x.Sum(y => y.ShareChange()), saler: x.First().Agency)).Where(x => x.share > 0).ToDictionary(x => x.id, x => x);
            var ids = groupd.Keys.Select(x => new BsonValue(x));
            var data = db.GetCollection<Investor>().Find(Query.In("_id", new BsonArray(ids))).ToList();
            var manager = db.GetCollection<Manager>().FindOne(x => x.IsMaster).Name;

            // 数据校验
            var lastDay = Days.PrevTradingDay(qu.ReportDate.AddDays(1));

            var nv = db.GetDailyCollection(qu.FundId).FindOne(x => x.Date == lastDay);
            if (nv is null || nv.Share != groupd.Sum(x => x.Value.share))
                return new(false, null, [], "TA基金份额与估值表不一致，无法生成投资人表");

            // 写入
            var outp = @$"temp\investor_{qu.Id}.xlsx";

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

            if (!Tpl.GenerateByPredefined(outp, path, obj))
                return (false, null, [], "生成投资人表失败，可能是模板问题");

            string? missing = null;
            var orders = db.GetCollection<TransferOrder>().Find(x => x.FundId == qu.FundId && x.Date < qu.ReportDate).OrderByDescending(x => x.Date).ToArray();
            var cids = data.Select(x => x.Id).ToList();
            var d = orders.Where(x => x.RiskDiscloure?.File is not null).GroupBy(x => x.InvestorId).
                Where(x => cids.Contains(x.Key)).Select(x => x.First()).Select(x => (x.InvestorId, File: x.RiskDiscloure!.File!)).ToList();

            if (d.Count != data.Count)
                missing = string.Join(",", data.ExceptBy(d.Select(x => x.InvestorId), x => x.Id).Select(x => x.Name));

            var zip = ZipSplitter.CreateSplitZip(d.Select(x => x.File).ToArray(), "temp", $"{qu.FundName}_风险揭示书_{qu.ReportDate:yyyyMMdd}", 20 * 1024 * 1024);

            // 检查文件大小
            if (new FileInfo(zip[0]).Length < 100 * 1024)
                return new(false, outp, zip, $"风险揭示书文件异常，大小仅 {new FileInfo(zip[0]).Length / 1024} KB，请检查文件后重试");

            return new(true, outp, zip, missing);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return new(false, null, [], e.Message);
        }
    }


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

    private static async Task<ErrorReturn> TrySubmitInCurrentPageAsync(IPage page)
    {
        try
        {
            var submitBtn = page.Locator("table.table-dashed.table-center tbody tr:first-child td:last-child a");
            var rows = page.Locator("tbody tr.ng-scope");
            var count = await rows.CountAsync();


            await submitBtn.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(800);

            return new(await CheckSubmitSuccessAndCloseModalAsync(page, "提交成功"));
        }
        catch (Exception e)
        {
            return new(false, e.Message);
        }
    }
}
