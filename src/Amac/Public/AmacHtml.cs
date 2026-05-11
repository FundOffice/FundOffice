using AngleSharp;
using AngleSharp.Dom;
using FMO.Logging;
using FMO.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FMO.AMAC;

public class AmacHtml
{
    public static async Task<bool> CrawleManagerInfo(Manager manager, List<FundBasicInfo> list)
    {
        try
        {
            using var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
            using var document = await context.OpenAsync(new Url($"https://gs.amac.org.cn/amac-infodisc/res/pof/manager/{manager.AmacId}.html"));

            if (document.Body?.TextContent.Contains("机构信息") is not true)
            {
                LogEx.Error("获取的网页内容异常：缺少机构信息");
                return false;
            }

            var sections = document.QuerySelectorAll("div.section");

            foreach (var section in sections)
            {
                var titleEl = section.QuerySelector("div.common-tit span");
                var title = titleEl?.TextContent.Trim();
                if (string.IsNullOrEmpty(title)) continue;

                switch (title)
                {
                    case "机构信息":
                        ParseManagerSection(section, manager);
                        break;
                    case "产品信息":
                        ParseProductSection(section, list);
                        break;
                }
            }

            return true;
        }
        catch (Exception e)
        {
            LogEx.Error($"CrawleManagerInfo {e}");
            return false;
        }
    }


    public static async Task<FundBasicInfo[]> CrawleManagerInfo(string managerId)
    {
        try
        {
            using var context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
            using var document = await context.OpenAsync(new Url($"https://gs.amac.org.cn/amac-infodisc/res/pof/manager/{managerId}.html"));

            if (document.Body?.TextContent.Contains("机构信息") is not true)
            {
                LogEx.Error("获取的网页内容异常：缺少机构信息");
                return [];
            }

            var sections = document.QuerySelectorAll("div.section");
            
            List<FundBasicInfo> list = [];
            foreach (var section in sections)
            {
                var titleEl = section.QuerySelector("div.common-tit span");
                var title = titleEl?.TextContent.Trim();
                if (string.IsNullOrEmpty(title)) continue;

                switch (title)
                { 
                    case "产品信息":
                        ParseProductSection(section, list);
                        break;
                }
            }

            return list.ToArray();
        }
        catch (Exception e)
        {
            LogEx.Error($"CrawleManagerInfo {e}");
            return [];
        }
    }





    #region 局部解析函数


    static void ParseManagerSection(IElement section, Manager mgr)
    {
        var rows = section.QuerySelectorAll("tr");
        foreach (var row in rows)
        {
            var titleCell = row.QuerySelector("td.title");
            if (titleCell == null) continue;

            // 清理键名：去除换行、多余空格及内部浮动Div文本干扰
            var key = Regex.Replace(titleCell.TextContent, @"\s+", " ").Trim();

            var valueCell = row.QuerySelector("td:not(.title)");
            if (valueCell == null) continue;
            var value = Regex.Replace(valueCell.TextContent, @"\s+", " ").Trim();

            switch (key)
            {
                case "基金管理人全称(中文)":
                    mgr.Name = value;
                    break;
                case "基金管理人全称(英文)":
                    mgr.EnglishName = string.IsNullOrWhiteSpace(value) ? null : value;
                    break;
                case "组织机构代码":
                    mgr.Identity = new Identity { Id = value, Type = IDType.UnifiedSocialCreditCode };
                    break;
                case "登记时间":
                    if (DateOnly.TryParse(value, out var regDate)) mgr.RegisterDate = regDate;
                    break;
                case "成立时间":
                    if (DateOnly.TryParse(value, out var setupDate)) mgr.SetupDate = setupDate;
                    break;
                case "注册地址":
                    mgr.RegisterAddress = value;
                    break;
                case "办公地址":
                    mgr.OfficeAddress = value;
                    break;
                case var k when k.Contains("注册资本"):
                    var regCap = ParseCapital(value);
                    mgr.RegisterCapital = regCap;
                    mgr.RegisterCapitalAmac = regCap;
                    break;
                case var k when k.Contains("实缴资本"):
                    var realCap = ParseCapital(value);
                    mgr.RealCapital = realCap;
                    mgr.RealCapitalAmac = realCap;
                    break;
                case "管理规模区间":
                    mgr.ScaleRange = value;
                    break;
                //case "业务类型":
                //    mgr.Description = value.Replace(" ", ", ").Trim();
                //    mgr.BusinessScope = mgr.Description; // 同步至基类经营范围字段
                //    break;
                case "机构信息最后更新时间":

                    break;
            }
        }

        // 检测信用/特殊提示标识（AMAC 通常以特定 class 或文本显示）
        //mgr.HasCreditTips = section.QuerySelector("[class*='credit'], [title*='信用'], .icon-credit") != null || section.TextContent.Contains("信用提示");
        //mgr.HasSpecialTips = section.QuerySelector("[class*='special'], [title*='异常'], [title*='警示'], .icon-warning") != null || section.TextContent.Contains("特殊提示");
    }


    static void ParseProductSection(IElement section, List<FundBasicInfo> fundList)
    {
        // 1. 获取外层表格行（包含分类标题和嵌套表格）
        var outerRows = section.QuerySelectorAll("table.table > tbody > tr");
        if (!outerRows.Any()) return;

        bool isPreRule = false;
        bool isAdvisor = false;
        var pendingFunds = new List<(bool isPre, bool isAdv, IElement row)>();

        foreach (var outerRow in outerRows)
        {
            // 2. 识别分类标题，切换状态标记
            var titleCell = outerRow.QuerySelector("td.title");
            if (titleCell != null)
            {
                var titleText = titleCell.TextContent.Trim();
                if (titleText.Contains("暂行办法实施前成立的基金")) { isPreRule = true; isAdvisor = false; }
                else if (titleText.Contains("暂行办法实施后成立的基金")) { isPreRule = false; isAdvisor = false; }
                else if (titleText.Contains("投资顾问类产品")) { isPreRule = false; isAdvisor = true; }
                else continue; // 跳过首行“管理人整体开立率”或其他无关行
            }

            // 3. 定位当前分类下的嵌套数据表
            var nestedTable = outerRow.QuerySelector("table.list-table");
            if (nestedTable == null) continue;

            // 4. 提取嵌套表 tbody 中的基金数据行
            var fundRows = nestedTable.QuerySelectorAll("tbody > tr");
            foreach (var fundRow in fundRows)
            {
                // 防御：必须包含基金链接才是有效数据行（自动过滤表头/空行）
                var link = fundRow.QuerySelector("a[href*=\"fund/\"]");
                if (link == null) continue;

                pendingFunds.Add((isPreRule, isAdvisor, fundRow));
            }
        }

        if (pendingFunds.Count == 0) return;

        // 5. 进度分配与数据提取
        double unit = 70.0 / pendingFunds.Count;
        foreach (var (isPre, isAdv, row) in pendingFunds)
        {
            var cells = row.QuerySelectorAll("td");
            // 标准列映射：[0]序号 [1]名称/链接 [2]月报 [3]季报 [4]半年报 [5]年报 [6]开立率
            if (cells.Length < 7) continue;

            var link = cells[1].QuerySelector("a[href*=\"fund/\"]");
            if (link == null) continue;

            // 规范化 URL (将 ../fund/xxx.html 转为 /fund/xxx.html)
            var rawUrl = link.GetAttribute("href") ?? "";
            var normalizedUrl = rawUrl.Replace("../", "/").TrimStart('.');

            fundList.Add(new FundBasicInfo
            {
                Name = link.TextContent.Trim(),
                Url = normalizedUrl,
                IsPreRule = isPre,
                IsAdvisor = isAdv,
                Monthly = GetUndisclosedCount(cells[2]),
                Quarterly = GetUndisclosedCount(cells[3]),
                SemiAnnally = GetUndisclosedCount(cells[4]),
                Annally = GetUndisclosedCount(cells[5]),
                InvestorAccountRate = ExtractDouble(cells[6].TextContent)
            });
        }
    }


    /// <summary>
    /// 解析注册资本/实缴资本（自动处理千分位逗号、万元单位转换）
    /// </summary>
    static decimal ParseCapital(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0m;
        var clean = Regex.Replace(val, @"[^\d.]", "");
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            // AMAC 页面明确标注单位为“万元”，若业务模型期望单位为“元”，此处自动换算
            return val.Contains("万元") ? d * 10000m : d;
        }
        return 0m;
    }

    /// <summary>
    /// 精准提取单元格中“未披露X条”的数值
    /// </summary>
    static int GetUndisclosedCount(IElement cell)
    {
        // TextContent 会包含 br 和 span 的所有文本，如："应披露0条\n未披露0条"
        var text = cell.TextContent ?? string.Empty;
        // 匹配“未披露”关键字后的数字，兼容空格/换行/有无“条”字
        var match = Regex.Match(text, @"未披露\s*(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) ? count : 0;
    }

    // 安全提取浮点数（兼容百分比符号、中文单位，解析失败返回 0.0）
    static double ExtractDouble(string text) => Regex.Match(text, @"[\d\.]+%") is var m && m.Success ? double.Parse(m.Value[..^1]) : 0;

    #endregion
}