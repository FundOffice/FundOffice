using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using Microsoft.Playwright;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;


namespace FMO.IO.AMAC;


/// <summary>
/// 程序初始化的第2步
/// </summary>
public class InitStep2Info
{
    /// <summary>
    /// 进度
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// 管理规模
    /// </summary>
    public string? CurrentScale { get; set; }

    public int? PreRuleCount { get; set; }

    public int? NormalCount { get; set; }

    public int? AdviseCount { get; set; }
}


public enum AmacReturn
{
    Success,

    NoAccount,

    AccountError,
    Browser,
    InvalidResponse,

}

public static class AmacAssist
{
    public const string MessageToken = "AMAC.CrawleManagerInfo";


    /// <summary>
    /// 通过关键字获取在AMAC中的管理人名称
    /// </summary>
    /// <param name="search"></param>
    /// <returns></returns>
    public static async Task<ManagerInfo[]?> GetInstitutionInfoFromAmac(string search)
    {

        try
        {
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0");

            var resp = await client.GetAsync("https://gs.amac.org.cn/amac-infodisc/res/pof/manager/managerList.html");
            if (!resp.IsSuccessStatusCode)
            {
                Log.Warning("请检查网络");
                return null;
            }

            StringContent content = new StringContent("{\"keyword\":\"sssss\",\"regiProvinceFsc\":\"province\",\"offiProvinceFsc\":\"province\",\"establishDate\":{\"from\":\"1900-01-01\",\"to\":\"9999-01-01\"},\"registerDate\":{\"from\":\"1900-01-01\",\"to\":\"9999-01-01\"}}".
                Replace("sssss", search), Encoding.UTF8, "application/json");


            var request = new HttpRequestMessage();
            request.RequestUri = new Uri("https://gs.amac.org.cn/amac-infodisc/api/pof/manager/query?&page=0&size=20");
            request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br, zstd");
            request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            request.Headers.Add("Origin", "https://gs.amac.org.cn");
            request.Headers.Add("Referer", "https://gs.amac.org.cn/amac-infodisc/res/pof/manager/managerList.html");
            request.Method = HttpMethod.Post;
            request.Content = content;


            resp = await client.SendAsync(request); ;// await client.PostAsync("https://gs.amac.org.cn/amac-infodisc/api/pof/manager/query?&page=0&size=20", content);
            if (resp.IsSuccessStatusCode)
            {
                var s = await resp.Content.ReadAsStringAsync();
                s = Regex.Replace(s, "</*em>", "");

                var root = System.Text.Json.JsonSerializer.Deserialize<QueryManagers>(s);// await resp.Content.ReadFromJsonAsync<AmacQuery.QueryManagers>();

                return root?.Content.ToArray();
            }

            Log.Error($"GetInstitutionInfoFromAmac Net Error {resp.StatusCode}");
            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, $"GetInstitutionInfoFromAmac");

            return null;
        }
    }

    public static async ValueTask<Dictionary<string, string>> CrawlFundInfo(string url, IPage page)
    {
        //
        if (string.IsNullOrWhiteSpace(url) || page is null)
            return new Dictionary<string, string>();

        await page.GotoAsync(url);

        var trs = page.Locator("//td[contains(text(),'基金名称')]/../../tr");
        var dict = new Dictionary<string, string>();
        foreach (var item in await trs.AllAsync())
        {
            var nlc = item.Locator("td");
            var cnt = await nlc.CountAsync();
            if (cnt > 3) continue;

            var ls = new List<string>();
            foreach (var value in await item.Locator("td").AllAsync())
                ls.Add(await value.InnerTextAsync());

            if (ls.Count > 1)
                dict.Add(ls[0], ls[1]);
        }

        return dict;
    }


    /// <summary>
    /// 从基金公示信息同步数据 
    /// </summary>
    /// <param name="fund"></param>
    /// <param name="client"></param>
    /// <returns></returns>
    public static async Task<bool> SyncFundInfoAsync(Fund fund, HttpClient client)
    {
        //
        if (string.IsNullOrWhiteSpace(fund.Url))
            return false;

        var content = await client.GetStringAsync(fund.Url);

        var idx = content.IndexOf("基金名称");
        if (idx < 0)
        {
            Log.Error("获取基金公示信息错误 1");
            return default;
        }

        var s = content.LastIndexOf("tbody", idx);
        var e = content.IndexOf("tbody", idx);
        if (e <= s)
        {
            Log.Error("获取基金公示信息错误 2");
            return default;
        }

        var table = content.Substring(s - 1, e - s + 7);

        var ms = Regex.Matches(table, "<tr>.*?</tr>", RegexOptions.Singleline);

        foreach (Match m in ms)
        {
            var tds = Regex.Matches(m.Value, "<td.*?>.*?</td>", RegexOptions.Singleline).Select(x => x.Value).ToArray();

            if (tds.Length < 2)
            {
                Log.Error("获取基金公示信息错误 3");
                return default;
            }

            var match = Regex.Match(tds[0], ">.*?<");
            if (!match.Success)
            {
                Log.Error("获取基金公示信息错误 4");
                return default;
            }

            var field = match.Value[1..^1];

            match = Regex.Match(tds[1], "(?s)>.*?<");
            if (!match.Success)
            {
                Log.Error("获取基金公示信息错误 5");
                return default;
            }
            var value = match.Value[1..^1].Trim();


            Fill(fund, field, value);
        }

        fund.PublicDisclosureSynchronizeTime = DateTime.Now;
        return true;
    }

    private static void Fill(Fund fund, string field, string value)
    {


        switch (field)
        {
            case string s when s.Contains("基金名称"):
                if (value != fund.Name)
                {
                    fund.Name = value;
                    fund.ShortName = Fund.GetDefaultShortName(value);
                }
                //   throw new Exception($"从基金公示信息同步数据错误，基金名称不匹配[{fund.Name.Value}]，公示为[{value}] {fund.Url}");
                break;
            case string s when s.Contains("成立时间"):
                if (DateOnly.TryParse(value, out DateOnly d))
                    fund.SetupDate = d;
                else throw new Exception($"未识别的日期格式：{value}");
                break;
            case string s when s.Contains("备案时间"):
                if (DateOnly.TryParse(value, out d))
                    fund.AuditDate = d;
                else throw new Exception($"未识别的日期格式：{value}");
                break;

            case string s when s.Contains("基金编号"):
                fund.Code = value.Trim();
                break;

            case string s when s.Contains("基金类型"):
                fund.Type = EnumHelper.FromDescription<FundType>(value.Trim());
                break;

            case string s when s.Contains("管理类型"):
                fund.ManageType = EnumHelper.FromDescription<ManageType>(value.Trim());
                break;

            case string s when s.Contains("托管人名称"):
                fund.Trustee = value.Trim();
                break;

            case string s when s.Contains("运作状态"):
                // 当开始清算，但协会还没更新时，不要覆盖状态
                if (EnumHelper.FromDescription<FundStatus>(value.Trim()) is FundStatus fundStatus && !(fundStatus == FundStatus.Normal && fund.Status == FundStatus.StartLiquidation))
                    fund.Status = fundStatus;
                
                break;


            case string s when s.Contains("基金信息最后更新时间"):
                if (DateTime.TryParse(value, out DateTime dt))
                    fund.LastUpdate = dt;
                break;


            default:
                break;
        }

    }

    private static async Task<FundBasicInfo[]> ExtractFund(IPage page, InitStep2Info info)
    {
        var lc = page.Locator("//*[contains(text(),'产品信息')]/../..");

        // type a
        var lf = lc.Locator("//td[text()='暂行办法实施前成立的基金']/..").Locator("tbody > tr");
        var lf2 = lc.Locator("//td[text()='暂行办法实施后成立的基金']/..").Locator("tbody > tr");
        var lf3 = lc.Locator("//td[text()='投资顾问类产品']/..").Locator("tbody > tr");

        info.PreRuleCount = await lf.CountAsync();
        info.NormalCount = await lf2.CountAsync();
        info.AdviseCount = await lf3.CountAsync();

        int cnt = info.PreRuleCount.Value + info.NormalCount.Value + info.AdviseCount.Value;
        double step = 50.0 / Math.Max(1, cnt);
        info.Progress = 50;
        WeakReferenceMessenger.Default.Send(info, MessageToken);
        info.PreRuleCount = null;
        info.NormalCount = null;
        info.NormalCount = null;

        List<FundBasicInfo> list = new List<FundBasicInfo>();


        foreach (var item in await lf.AllAsync())
        {
            LocatorInnerTextOptions options = new LocatorInnerTextOptions { Timeout = 100 };
            var name = await item.Locator("td > a").First.InnerTextAsync(options);
            if (string.IsNullOrWhiteSpace(name))
            {
                Log.Error($"CrawleManagerInfo. Fund Name is Empty");
                return Array.Empty<FundBasicInfo>();
            }
            var url = await item.Locator("td > a").First.GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(url))
            {
                Log.Error($"CrawleManagerInfo. Fund Url is Empty");
                return Array.Empty<FundBasicInfo>();
            }

            list.Add(new FundBasicInfo { Name = name, Url = url[2..], IsPreRule = true });

            info.Progress += step;
            WeakReferenceMessenger.Default.Send(info, MessageToken);
        }
        foreach (var item in await lf2.AllAsync())
        {
            LocatorInnerTextOptions options = new LocatorInnerTextOptions { Timeout = 100 };
            var name = await item.Locator("td > a").First.InnerTextAsync(options);
            if (string.IsNullOrWhiteSpace(name))
            {
                Log.Error($"CrawleManagerInfo. Fund Name is Empty");
                return Array.Empty<FundBasicInfo>();
            }
            var url = await item.Locator("td > a").First.GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(url))
            {
                Log.Error($"CrawleManagerInfo. Fund Url is Empty");
                return Array.Empty<FundBasicInfo>();
            }

            list.Add(new FundBasicInfo { Name = name, Url = url[2..], IsPreRule = false });
            info.Progress += step;
            WeakReferenceMessenger.Default.Send(info, MessageToken);
        }
        foreach (var item in await lf3.AllAsync())
        {
            LocatorInnerTextOptions options = new LocatorInnerTextOptions { Timeout = 100 };
            var name = await item.Locator("td > a").First.InnerTextAsync(options);
            if (string.IsNullOrWhiteSpace(name))
            {
                Log.Error($"CrawleManagerInfo. Fund Name is Empty");
                return Array.Empty<FundBasicInfo>();
            }
            var url = await item.Locator("td > a").First.GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(url))
            {
                Log.Error($"CrawleManagerInfo. Fund Url is Empty");
                return Array.Empty<FundBasicInfo>();
            }

            list.Add(new FundBasicInfo { Name = name, Url = url[2..], IsAdvisor = true });
            info.Progress += step;
            WeakReferenceMessenger.Default.Send(info, MessageToken);
        }


        info.Progress = 100;
        WeakReferenceMessenger.Default.Send(info, MessageToken);
        return list.ToArray();
    }



    public static async Task<bool> CrawleManagerInfo(Manager manager, List<FundBasicInfo> list, HttpClient client)
    {
        try
        {
            var response = await client.GetAsync($"https://gs.amac.org.cn/amac-infodisc/res/pof/manager/{manager.AmacId}.html");

            InitStep2Info info = new();
            info.Progress = 20;

            // 检查 section
            // 获取所有table
            var content = await response.Content.ReadAsStringAsync();


            // <div class="info-body">
            var m = Regex.Match(content, "<div class=\"section\"[\\s\\S]*?关闭");
            if (!content.Contains("机构信息") || !m.Success)
            {
                Log.Error("获取的网页内容异常");
                return false;
            }

            // 获取section
            var array = m.Value.Split("<div class=\"section\">", StringSplitOptions.RemoveEmptyEntries);
            var result = new List<(string t, string[][] d)>();
            foreach (var sec in array)
            {
                m = Regex.Match(sec, @"<div\s+class=""common-tit""[^>]*>\s*<span>(.*?)</span>", RegexOptions.Singleline);

                var title = m.Groups[1].Value;

                // 获取所有tr
                var ms = Regex.Matches(sec, "<tr>.*?</tr>", RegexOptions.Singleline);
                var data = ms.Select(x => Regex.Matches(x.Value, @"<td[^>]*?>(.*?)</td>", RegexOptions.Singleline).Select(y => y.Groups[1].Value).
                    Select(y => Regex.Replace(y, @"<[^>]*?>", string.Empty)).Select(x => x.Replace("&nbsp;", " ").Trim()).ToArray()).Where(x => x.Length > 1).ToArray();

                result.Add((title, data));
            }

            // 解析
            foreach (var sec in result)
            {
                switch (sec.t)
                {
                    case "机构信息":
                        manager.Identity = new Identity { Id = sec.d.FirstOrDefault(x => x[0].Contains("组织机构代码"))![1], Type = IDType.UnifiedSocialCreditCode };
                        //v = sec.d.FirstOrDefault(x => x[0].Contains("登记编号"));
                        //if (v is null) return false;
                        //manager.RegisterNo = v[1];

                        manager.RegisterCapital = decimal.Parse(sec.d.FirstOrDefault(x => x[0].Contains("注册资本"))![1]);
                        manager.RealCapital = decimal.Parse(sec.d.FirstOrDefault(x => x[0].Contains("实缴资本"))![1]);
                        manager.Advisorable = sec.d.FirstOrDefault(x => x[0].Contains("提供投资建议"))?[1].Contains("是") ?? false;
                        manager.ScaleRange = sec.d.FirstOrDefault(x => x[0].Contains("管理规模区间"))![1];
                        info.Progress = 30;
                        info.CurrentScale = manager.ScaleRange;
                        WeakReferenceMessenger.Default.Send(info, MessageToken);
                        break;
                    default:
                        break;
                }

            }

            // 解析产品
            var fstr = array.Last(x => x.Contains("产品信息"));
            var msf = Regex.Matches(fstr, @"(?s)<tr>(?:(?!</?tr).)*<tr>(?:(?!</?tr).)*</tr>(?:(?!</?tr).)*</tr>");

            var flist = new List<(bool a, bool b, IEnumerable<string> c)>();
            var trs = HtmlParser.GetTags(fstr, "tr");
            foreach (var tr in trs)
            {
                bool ispre = tr.Contains("暂行办法实施前成立的基金");
                bool isadv = tr.Contains("投资顾问类产品");

                var funds = HtmlParser.GetTags(tr[5..], "tr");
                flist.Add((ispre, isadv, funds.Skip(1)));
            }

            double unit = 70.0 / flist.Sum(x => x.c.Count());

            foreach (var item in flist)
            {
                foreach (var f in item.c)
                {
                    var tds = HtmlParser.GetTags(f, "td");
                    m = Regex.Match(tds[0], @"(/fund/\w+.html).*?>([^<]+)");

                    list.Add(new FundBasicInfo
                    {
                        Name = m.Groups[2].Value,
                        Url = m.Groups[1].Value,
                        IsPreRule = item.a,
                        IsAdvisor = item.b
                    });

                    info.Progress += unit;
                    WeakReferenceMessenger.Default.Send(info, MessageToken);
                }

            }

            return true;
        }
        catch (Exception e)
        {
            Log.Error($"CrawleManagerInfo {e}");
            return false;
        }
    }

    public static async Task<FundBasicInfo[]> CrawleManagerInfo(Manager manager)
    {
        try
        {
            using IPlaywright pw = await Playwright.CreateAsync();
            var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge" });
            var page = await browser.NewPageAsync();
            await page.GotoAsync($"https://gs.amac.org.cn/amac-infodisc/res/pof/manager/{manager.AmacId}.html");

            InitStep2Info info = new();
            info.Progress = 20;
            WeakReferenceMessenger.Default.Send(info, MessageToken);


            var sec = page.Locator(".section");
            var basic = page.Locator(".section[0]");
            var fvs = await page.Locator(".section").First.Locator(".table > tbody > tr").AllAsync();

            var dict = new Dictionary<string, string>();
            LocatorInnerTextOptions options = new LocatorInnerTextOptions { Timeout = 100 };

            foreach (var item in fvs)
            {
                var nlc = item.Locator("td");
                var cnt = await nlc.CountAsync();
                if (cnt > 3) continue;

                dict.Add(await nlc.Nth(0).InnerTextAsync(), await nlc.Nth(1).InnerTextAsync());
            }

            var id = dict.First(x => x.Key.Contains("组织机构代码")).Value;
            var regno = dict.First(x => x.Key.Contains("登记编号")).Value;

            //Manager manager = new Manager() { Name = name, AmacId = amacid, RegisterNo = regno, Id = id };

            manager.Identity = new Identity { Id = id, Type = IDType.UnifiedSocialCreditCode };
            manager.RegisterCapital = decimal.Parse(dict.FirstOrDefault(x => x.Key.Contains("注册资本")).Value);
            manager.RealCapital = decimal.Parse(dict.FirstOrDefault(x => x.Key.Contains("实缴资本")).Value);
            manager.Advisorable = dict.FirstOrDefault(x => x.Key.Contains("提供投资建议")).Value?.Contains("是") ?? false;
            manager.ScaleRange = dict.FirstOrDefault(x => x.Key.Contains("管理规模区间")).Value;
            info.Progress = 30;
            info.CurrentScale = manager.ScaleRange;
            WeakReferenceMessenger.Default.Send(info, MessageToken);
            info.CurrentScale = null;

            /////////////////////////////////////////////////////////////////////////////

            var nus = await ExtractFund(page, info);

            return nus;
        }
        catch (Exception e)
        {
            Log.Error(e, $"CrawleManagerInfo. Fund Url is Empty");
            return Array.Empty<FundBasicInfo>();
        }
    }




}


public static class HtmlParser
{
    /// <summary>
    /// 获取 HTML 中匹配标签名的内容（不依赖外部库）
    /// </summary>
    /// <param name="html">HTML 字符串</param>
    /// <param name="tagName">标签名（如 "div"、"tr"，不包含尖括号）</param>
    /// <param name="includeOuter">是否包含标签本身</param>
    /// <returns>匹配的标签内容列表</returns>
    public static List<string> GetTags(string html, string tagName, bool includeOuter = true)
    {
        var results = new List<string>();
        int index = 0;
        tagName = tagName.ToLower();

        while (index < html.Length)
        {
            // 查找开始标签 <tagName
            int startTagIndex = FindTagStart(html, tagName, index);
            if (startTagIndex == -1) break;

            // 解析标签属性，找到结束符 >
            int tagEndIndex = FindTagEnd(html, startTagIndex);
            if (tagEndIndex == -1) break;

            bool isSelfClosing = html[tagEndIndex - 1] == '/';
            string fullStartTag = html.Substring(startTagIndex, tagEndIndex - startTagIndex + 1);

            if (isSelfClosing)
            {
                // 自闭合标签 <tagName ... />
                if (includeOuter)
                    results.Add(fullStartTag);
                index = tagEndIndex + 1;
                continue;
            }

            // 查找对应的结束标签 </tagName>
            int endTagIndex = FindMatchingEndTag(html, tagName, tagEndIndex + 1);
            if (endTagIndex == -1)
            {
                // 未找到匹配的结束标签，继续查找下一个开始标签
                index = startTagIndex + 1;
                continue;
            }

            // 提取标签内容
            int contentStart = tagEndIndex + 1;
            int contentEnd = endTagIndex - 2; // 排除 </

            if (includeOuter)
            {
                results.Add(html.Substring(startTagIndex, endTagIndex + tagName.Length + 3 - startTagIndex));
            }
            else
            {
                results.Add(html.Substring(contentStart, contentEnd - contentStart + 1));
            }

            index = endTagIndex + tagName.Length + 3; // 跳过 </tagName>
        }

        return results;
    }

    // 查找标签开始位置 <tagName
    private static int FindTagStart(string html, string tagName, int startIndex)
    {
        int index = html.IndexOf('<', startIndex);
        while (index != -1)
        {
            if (index + tagName.Length + 1 >= html.Length) return -1;

            // 检查是否为目标标签（忽略大小写）
            bool isMatch = true;
            for (int i = 0; i < tagName.Length; i++)
            {
                if (char.ToLower(html[index + 1 + i]) != tagName[i])
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                // 确认标签名后是空格、属性或 >
                char nextChar = html[index + 1 + tagName.Length];
                if (nextChar == ' ' || nextChar == '>' || nextChar == '/')
                    return index;
            }

            index = html.IndexOf('<', index + 1);
        }
        return -1;
    }

    // 查找标签结束符 >
    private static int FindTagEnd(string html, int startIndex)
    {
        int index = html.IndexOf('>', startIndex);
        return index;
    }

    // 查找匹配的结束标签 </tagName>
    private static int FindMatchingEndTag(string html, string tagName, int startIndex)
    {
        int openCount = 1; // 当前嵌套层级
        int index = startIndex;

        while (index < html.Length)
        {
            // 查找下一个 <
            int nextOpen = html.IndexOf('<', index);
            if (nextOpen == -1) return -1;

            // 检查是否为结束标签 </tagName>
            if (nextOpen + tagName.Length + 3 <= html.Length &&
                html[nextOpen + 1] == '/' &&
                StringEqualsIgnoreCase(html, nextOpen + 2, tagName))
            {
                openCount--;
                if (openCount == 0)
                    return nextOpen;
            }
            // 检查是否为开始标签 <tagName
            else if (nextOpen + tagName.Length + 1 <= html.Length &&
                     StringEqualsIgnoreCase(html, nextOpen + 1, tagName))
            {
                char nextChar = html[nextOpen + 1 + tagName.Length];
                if (nextChar == ' ' || nextChar == '>' || nextChar == '/')
                {
                    // 确认不是结束标签
                    if (nextChar != '/' || (nextChar == '/' && nextOpen + tagName.Length + 2 < html.Length && html[nextOpen + tagName.Length + 2] != '>'))
                    {
                        openCount++;
                    }
                }
            }

            index = nextOpen + 1;
        }

        return -1;
    }

    // 忽略大小写比较字符串
    private static bool StringEqualsIgnoreCase(string source, int startIndex, string value)
    {
        if (startIndex + value.Length > source.Length) return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (char.ToLower(source[startIndex + i]) != value[i])
                return false;
        }
        return true;
    }
}


public struct LearnRecord
{
    public string Name { get; set; }

    public string IdentityNo { get; set; }

    public string ClassId { get; set; }

    public string ClassName { get; set; }

}
