using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


[assembly: InternalsVisibleTo("TestMeiShi")]


namespace FMO.ESigning.MeiShi;


public class MeiShiConfig : ISigningConfig
{
    public string Id => "meishi";

    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// 有效性
    /// </summary>
    public bool IsValid { get; set; }

    public bool IsEnable { get; set; }
}



public class MeiShiAssit : ISigning
{

    HttpClient client;

    bool isLogin;

    public string Id => "meishi";


    public string? Token { get; set; }

    public bool IsValid { get; set; } = true;



    private Dictionary<string, int> _signTypeDict = new();

    public MeiShiAssit()
    {
        client = new();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36 Edg/142.0.0.0");
    }

    public async Task<bool> Login()
    {
        // 校验
        using var db = DbHelper.Platform();
        var config = db.GetCollection<ISigningConfig>().FindById(Id) as MeiShiConfig;

        if (config is null || string.IsNullOrWhiteSpace(config.UserName) || string.IsNullOrWhiteSpace(config.Password))
        {
            IsValid = false;
            this.SetStatus(false);
            LogEx.Error("登录MeiShi错误：用户名或密码为空");
            return false;
        }


        // ===== 第一个请求：SSO 登录 =====
        var request1 = new HttpRequestMessage(HttpMethod.Post, "https://sso.simu800.com/ssocenter/login/doLogin");

        // 设置 Headers（不含 Content-Type，它属于 Content）
        request1.Headers.Add("accept", "application/json, text/plain, */*");
        request1.Headers.Add("accept-language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        request1.Headers.Add("origin", "https://sso.simu800.com");
        request1.Headers.Add("priority", "u=1, i");
        //request1.Headers.Add("referer", "https://sso.simu800.com/ssoweb/user/login?v=1764310000298");
        request1.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"142\", \"Microsoft Edge\";v=\"142\", \"Not_A Brand\";v=\"99\"");
        request1.Headers.Add("sec-ch-ua-mobile", "?0");
        request1.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
        request1.Headers.Add("sec-fetch-dest", "empty");
        request1.Headers.Add("sec-fetch-mode", "cors");
        request1.Headers.Add("sec-fetch-site", "same-origin");
        // 忽略无效头: "token;" （如需 token，请用 request1.Headers.Add("token", "xxx")）
        request1.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36 Edg/142.0.0.0");


        // 构造 POST 请求体
        var jsonContent = @$"{{""encryptData"":""{LoginEncryptor.EncryptLogin(config.UserName, config.Password)}""}}";

        request1.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        request1.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

        // 发送 POST 请求
        var response = await client.SendAsync(request1);

        var loginjson = await response.Content.ReadAsStringAsync();

        if (loginjson.Contains("密码错误"))
        {
            IsValid = false;
            this.SetStatus(false);
            LogEx.Warning("登录MeiShi错误：用户名或密码错误");
            return false;
        }

        var result = JsonSerializer.Deserialize<LoginResultJson>(loginjson);
        if (!result!.success)
        {
            IsValid = false;
            LogEx.Error($"登录MeiShi错误：{result.message}");
            return false;
        }

        // ===== 从 response1 提取 Set-Cookie =====
        var updatedCookies = new Dictionary<string, string>(); // 以初始 cookie 为基础

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (string setCookie in setCookieHeaders)
            {
                // 使用正则提取 "name=value" 部分（忽略 ; 后的属性）
                var match = Regex.Match(setCookie, @"^([^=]+)=((?:[^;]|(?<=\\);)*)");
                if (match.Success)
                {
                    string name = match.Groups[1].Value.Trim();
                    string value = match.Groups[2].Value.Trim();
                    if (!string.IsNullOrEmpty(value)) // 防止删除 cookie（value 为空时应删除，但此处简化）
                        updatedCookies[name] = value;
                    else
                        updatedCookies.Remove(name); // 可选：处理 cookie 删除
                }
            }
        }


        // ===== 第二个请求：VIP OAuth 登录 =====
        var request2 = new HttpRequestMessage(HttpMethod.Post, "https://vipfunds.simu800.com/vip-manager/managerUser/managerLoginOauth");

        // 设置 Headers
        request2.Headers.Add("accept", "application/json, text/plain, */*");
        request2.Headers.Add("accept-language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        request2.Headers.Add("origin", "https://vipfunds.simu800.com");
        request2.Headers.Add("priority", "u=1, i");
        //request2.Headers.Add("referer", "https://vipfunds.simu800.com/vipmanager/singleSignOn?loginSucUri=https://vipfunds.simu800.com/vipmanager/panel&auth_channel=null&v=1764310139517");
        request2.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"142\", \"Microsoft Edge\";v=\"142\", \"Not_A Brand\";v=\"99\"");
        request2.Headers.Add("sec-ch-ua-mobile", "?0");
        request2.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
        request2.Headers.Add("sec-fetch-dest", "empty");
        request2.Headers.Add("sec-fetch-mode", "cors");
        request2.Headers.Add("sec-fetch-site", "same-origin");
        request2.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36 Edg/142.0.0.0");
        // 构建更新后的 Cookie 字符串（用于 request2）
        try
        {
            string cookieString2 = string.Join("; ", updatedCookies.Select(kv => $"{kv.Key}={kv.Value}"));
            request2.Headers.Add("Cookie", cookieString2);
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            if (setCookieHeaders?.Any() ?? false)
                LogEx.Information(string.Join('\n', setCookieHeaders));
        }

        var jsonBody = "{}";
        request2.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        // 发送 POST 请求 
        response = await client.SendAsync(request2);

        var json = await response.Content.ReadAsStringAsync();

        var m = Regex.Match(json, "\"token\"\\s*:\\s*\"([^\"]+)\"");

        Token = m.Success ? m.Groups[1].Value : null;
        return m.Success;
    }


    private async Task<string> Query(DateTime from, DateTime end)
    {

        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/customer/query");
        request.Headers.Add("tokenid", Token);
        var param = new { createTimeBegin = from.TimeStampByMilliseconds(), pageNum = 1, pageSize = 500, customizeValues = new { } };
        request.Content = new StringContent(JsonSerializer.Serialize(param), Encoding.UTF8, "application/json");

        var response = client.Send(request);

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<Investor[]> QueryCustomerAsync(DateTime from = default, DateTime end = default)
    {
        if (!IsValid) return [];
        if (!isLogin) isLogin = await Login();
        if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return []; }


        if (end == default) end = DateTime.Now;


        var cont = await Query(from, end);
        SigningLoger.LogRun(this, nameof(QueryCustomerAsync), $"{from}-{end}", cont);

        if (Regex.IsMatch(cont, "token已失效|重新登录"))
        {
            isLogin = false;
            isLogin = await Login();
            if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return []; }

            cont = await Query(from, end);
        }

        var root = JsonSerializer.Deserialize<RootJson>(cont);
        if (root?.code != 1008)
        {
            LogEx.Error($"MeiShi QueryCustomerAsync {root?.message}");
            return [];
        }
        var customers = root?.data?["list"].Deserialize<CustomerJson[]>();
        var formated = customers?.Where(x => x is not null)?.Select(x => x.To())?.ToArray() ?? [];

        return formated;
    }


    public async Task<InvestorQualification[]> QueryQualificationAsync(DateTime from, DateTime end)
    {
        if (!IsValid) return [];
        if (!isLogin) isLogin = await Login();
        if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return []; }

        if (end == default) end = DateTime.Now;

        // 已存在的资格认证记录
        using var db = DbHelper.Base();
        var cusIdMap = db.GetCollection<Investor>().Query().Where(x => x.Identity != null).Select(x => new { x.Id, No = x.Identity!.Id }).ToArray().ToDictionary(x => x.No, x => x.Id);

        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/identify/flow/getPage");
        request.Headers.Add("tokenid", Token);

        var param = new { investorTypeList = (int[])[1, 2], pageNum = 1, pageSize = 500, status = 1, identifyStartTime = from.TimeStampByMilliseconds(), identifyEndTime = end.TimeStampByMilliseconds() };
        request.Content = new StringContent(JsonSerializer.Serialize(param), Encoding.UTF8, "application/json");

        var response = client.Send(request);

        var json = await response.Content.ReadAsStringAsync();
        SigningLoger.LogRun(this, nameof(QueryQualificationAsync), $"{from}-{end}", json);

        if (Regex.IsMatch(json, "token已失效|重新登录"))
        {
            isLogin = false;
            return [];
        }

        var root = JsonSerializer.Deserialize<RootJson>(json);

        var qjsons = root?.data?["list"]?.Deserialize<QualficationJson[]>();

        if (qjsons is null) return [];

        List<InvestorQualification> qs = new();
        foreach (var item in qjsons)
        {
            var cusName = item.CustomerName;
            var idNo = item.CardNumber;
            var vDate = DateTime.Parse(item.IdentifyTime!);

            if (!cusIdMap.TryGetValue(idNo!, out var invId))
            {
                LogEx.Warning($"同步MeiShi投资者资格认证时，未找到对应投资人：{cusName}（{idNo}）");
                continue;
            }

            // 检查是否有
            InvestorQualification q = new();
            q.InvestorId = invId;
            q.InvestorName = cusName;
            q.Date = DateOnly.FromDateTime(vDate);
            q.Result = item.InvestorType switch { 2 => QualifiedInvestorType.Professional, _ => QualifiedInvestorType.Normal };
            q.IdentityCode = idNo;
            q.Source = Id;
            q.ExternalId = item.IdentifyFlowId.ToString();

            qs.Add(q);
        }
        return qs.ToArray();
    }


    public async Task<bool> QueryQualificationAsync(InvestorQualification q)
    {
        try
        {
            // 获取详细数据
            HttpRequestMessage request = new();
            request.Method = HttpMethod.Get;
            request.Headers.Add("tokenid", Token);
            request.RequestUri = new Uri($"https://vipfunds.simu800.com/vip-manager/identify/flow/getDetail?identifyFlowId={q.ExternalId}&codeValue=1070&t={DateTime.Now.TimeStampByMilliseconds()}");

            var response = client.Send(request);

            var json = await response.Content.ReadAsStringAsync();
            SigningLoger.LogRun(this, nameof(QueryQualificationAsync), $"{q.Id}", json);

            var root = JsonSerializer.Deserialize<RootJson>(json);

            var detail = root!.data.Deserialize<QualficationSignFilesRoot>()!;

            var cusName = detail.CustomerName;
            var vDate = DateTime.TryParse(detail.IdentifyTime, out var t) ? t : default;
            q.InvestorName = cusName;
            q.Date = DateOnly.FromDateTime(vDate);
            q.Result = detail.InvestorType switch { 2 => QualifiedInvestorType.Professional, _ => QualifiedInvestorType.Normal };

            if (detail.CustomerType == 3)
                q.ProofType = QualificationFileType.Product;

            var proofs = new MultiFile();

            foreach (var att in detail!.SignAttachments!)
            {
                var fname = $"{cusName}-{vDate:yyyy.MM.dd}-{att.documentName}";


                // 基本信息表
                if (att.documentType == 109 && att.signedPdfUrl is not null)
                {
                    var (stream, fn) = await Download(att.signedPdfUrl, fname);
                    q.InfomationSheet = new SimpleFile(FileMeta.Create(stream, fn));
                }

                // 承诺书
                else if (att.documentType == 120 && att.signedPdfUrl is not null)
                {
                    var (stream, fn) = await Download(att.signedPdfUrl, fname);
                    q.CommitmentLetter = new SimpleFile(FileMeta.Create(stream, fn));
                }

                // 普通&专业投资者告知书
                else if (att.documentType == 110 && att.signedPdfUrl is not null)
                {
                    var (stream, fn) = await Download(att.signedPdfUrl, fname);
                    q.Notice = new SimpleFile(FileMeta.Create(stream, fn));
                }
                // 税收居民身份声明
                else if (att.documentType == 108 && att.signedPdfUrl is not null)
                {
                    var (stream, fn) = await Download(att.signedPdfUrl, fname);
                    q.TaxDeclaration = new SimpleFile(FileMeta.Create(stream, fn));
                }
                // 投资经历
                else if (att.documentType == 112 && att.signedPdfUrl is not null)
                {
                    var (stream, fn) = await Download(att.signedPdfUrl, fname);
                    q.ProofOfExperience = new SimpleFile(FileMeta.Create(stream, fn));
                }

                // 个人
                else if (detail.CustomerType == 1)
                {
                    // 身份证正面
                    if (att.documentType == 203)
                    {
                        // 获取文件
                        await UpdateIdFile(q.InvestorId, att);
                    }

                    // 员工证明
                    else if (att.documentType == 201)
                    {
                        for (int i = 0; i < att.documentUrls!.Length; i++)
                        {
                            var stream = await client.GetStreamAsync(att.documentUrls[i]);
                            fname = $"{cusName}-{vDate:yyyy.MM.dd}-{att.documentName}{i + 1}{GetFileExtentionFromUrl(att.documentUrls[i])}";
                            proofs.Files.Add(FileMeta.Create(stream, fname));
                        }

                        q.ProofType = QualificationFileType.Employee;
                    }

                    // 金融资产
                    else if (att.documentType == 111)
                    {
                        for (int i = 0; i < att.documentUrls!.Length; i++)
                        {
                            var stream = await client.GetStreamAsync(att.documentUrls[i]);
                            fname = $"{cusName}-{vDate:yyyy.MM.dd}-{att.documentName}{i + 1}{GetFileExtentionFromUrl(att.documentUrls[i])}";
                            proofs.Files.Add(FileMeta.Create(stream, fname));
                        }

                        q.ProofType = QualificationFileType.Financial;
                    }
                    else // 其他证明文件
                    {
                        for (int i = 0; i < att.documentUrls!.Length; i++)
                        {
                            var stream = await client.GetStreamAsync(att.documentUrls[i]);
                            fname = $"{cusName}-{vDate:yyyy.MM.dd}-{att.documentName}{i + 1}{GetFileExtentionFromUrl(att.documentUrls[i])}";
                            proofs.Files.Add(FileMeta.Create(stream, fname));
                        }
                    }
                }
                else // 机构/// 产品
                {
                    // 金融机构证明
                    if (detail.CustomerType == 2 && att.documentType == 113)
                    {
                        q.ProofType = QualificationFileType.FinancialInstitution;
                    }

                    // 营业执照
                    if (att.documentType == 114 && detail.CustomerType == 2)
                    {
                        var stream = await client.GetStreamAsync(att.documentUrl);

                        using var db = DbHelper.Base();
                        var cert = db.GetCollection<InvestorCertifications>().FindById(q.InvestorId) ?? new() { Id = q.InvestorId };
                        cert.Files.Add(FileMeta.Create(stream, $"营业执照{GetFileExtentionFromUrl(att.documentUrl!)}"));

                        db.GetCollection<InvestorCertifications>().Upsert(cert);
                    }
                    // 备案函
                    else if (att.documentType == 119 && detail.CustomerType == 3)
                    {
                        var stream = await client.GetStreamAsync(att.documentUrl);

                        using var db = DbHelper.Base();
                        var cert = db.GetCollection<InvestorCertifications>().FindById(q.InvestorId) ?? new() { Id = q.InvestorId };
                        FileMeta fm = FileMeta.Create(stream, $"备案函{GetFileExtentionFromUrl(att.documentUrl!)}");
                        cert.Files.Add(fm);

                        db.GetCollection<InvestorCertifications>().Upsert(cert);

                        proofs.Files.Add(fm);
                    }
                    // 经办/法人
                    else if (att.documentType == 152 && att.signedPdfUrl is not null)
                    {
                        var (stream, fn) = await Download(att.signedPdfUrl, fname);
                        q.Agent = new SimpleFile(FileMeta.Create(stream, fn));
                        q.AgentIsLeagal = Regex.IsMatch(att.documentName!, "法人|法定代表人");
                    }
                    else if (att.documentType == 116 && att.signedPdfUrl is not null)
                    {
                        var (stream, fn) = await Download(att.signedPdfUrl, fname);
                        q.Authorization = new SimpleFile(FileMeta.Create(stream, fn));
                    }
                    else
                    {
                        for (int i = 0; i < att.documentUrls!.Length; i++)
                        {
                            var stream = await client.GetStreamAsync(att.documentUrls[i]);
                            fname = $"{cusName}-{vDate:yyyy.MM.dd}-{att.documentName}{i + 1}{GetFileExtentionFromUrl(att.documentUrls[i])}";
                            proofs.Files.Add(FileMeta.Create(stream, fname));
                        }
                    }
                }
            }
            q.CertificationFiles = proofs;
            q.Check();
            return true;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return false;
        }
    }


    public async Task<TransferOrder[]> QueryOrderAsync(DateTime from = default, DateTime end = default)
    {
        //if (end == default) end = DateTime.Now.AddDays(1);

        if (!IsValid) return [];
        if (!isLogin) isLogin = await Login();
        if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return []; }


        List<OrderInfoJson> infoJsons = new();
        for (int i = 0; i < 20; i++)
        {
            HttpRequestMessage request = new();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/manager/signFlowController/querySignFlowAll");
            request.Headers.Add("tokenid", Token);

            object param = end != default ? new { queryType = 0, pageNum = 1, pageSize = 500, signFlowStartDateBegin = from.TimeStampByMilliseconds() } :
                    new { queryType = 0, pageNum = 1, pageSize = 500, signFlowStartDateBegin = from.TimeStampByMilliseconds() };
            request.Content = new StringContent(JsonSerializer.Serialize(param), Encoding.UTF8, "application/json");

            var response = client.Send(request);

            var cont = await response.Content.ReadAsStringAsync();
            SigningLoger.LogRun(this, nameof(QueryOrderAsync), $"{from}-{end}", cont);
            if (Regex.IsMatch(cont, "token已失效|重新登录"))
            {
                isLogin = false;
                return [];
            }

            var root = JsonSerializer.Deserialize<RootJson>(cont);
            if (root is null) return [];

            var data = root.data.Deserialize<OrdersRootJson>();

            if (data?.List?.Length > 0)
                infoJsons.AddRange(data.List);

            // 多页处理
            if (data is null || data.Pages == data.PageNum)
                break;
        }

        foreach (var item in infoJsons)
            _signTypeDict[item.SignFlowId.ToString()] = item.SignFlowSourceType ?? 0;

        return infoJsons.Select(x => x.To()).ToArray();
    }

    public async Task<bool> QueryOrderAsyncA(TransferOrder order)
    {
        try
        {
            if (!IsValid) return false;
            if (!isLogin) isLogin = await Login();
            if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return false; }

            // 获取详细数据
            HttpRequestMessage request = new();
            request.Method = HttpMethod.Get;
            request.Headers.Add("tokenid", Token);
            var uid = order.Type switch { TransferOrderType.FirstTrade => 2070, TransferOrderType.Buy => 3020, _ => 4040 };
            request.RequestUri = new Uri($"https://vipfunds.simu800.com/vip-manager/manager/signFlowController/querySignFlowInfo?flowId={order.ExternalId}&codeValue={uid}&t={DateTime.Now.TimeStampByMilliseconds()}");

            var response = client.Send(request);

            var json = await response.Content.ReadAsStringAsync();
            SigningLoger.LogRun(this, nameof(QueryOrderAsync), $"{order.ExternalId}", json);
            if (Regex.IsMatch(json, "token已失效|重新登录"))
            {
                isLogin = false;
                return false;
            }

            var root = JsonSerializer.Deserialize<RootJson>(json);

            if (root is null) return false;

            var data = root.data.Deserialize<OrderFilesRootJson>();
            if (data is null) return false;

            foreach (var f in data.Files)
            {
                var fname = $"{order.InvestorName}-{order.Date:yyyy.MM.dd}-{f.DocumentName}";
                var (stream, fn) = await Download(f.SealedUrl!, fname);

                if (f.DocumentName.Contains("申请") || f.CodeType == 125)
                    order.OrderSheet = new SimpleFile(FileMeta.Create(stream, fn));
                else if (f.DocumentName.Contains("合同") || f.CodeType == 123)
                    order.Contract = new SimpleFile(FileMeta.Create(stream, fn));
                else if (f.DocumentName.Contains("风险揭示") || f.CodeType == 122)
                    order.RiskDiscloure = new SimpleFile(FileMeta.Create(stream, fn));
                else if (f.DocumentName.Contains("告知书") || f.CodeType == 121)
                    order.RiskPair = new SimpleFile(FileMeta.Create(stream, fn));
            }

            if (!string.IsNullOrWhiteSpace(data.DoubleRecordingUrl))
            {
                var fname = $"{order.InvestorName}-{order.Date:yyyy.MM.dd}-双录";
                var (stream, fn) = await Download(data.DoubleRecordingUrl, fname);
                order.Videotape = new SimpleFile(FileMeta.Create(stream, fn));
            }

            return true;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return false;
        }
    }
    public async Task<bool> QueryOrderAsyncB(TransferOrder order)
    {
        try
        {
            if (!IsValid) return false;
            if (!isLogin) isLogin = await Login();
            if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return false; }

            // 获取详细数据
            HttpRequestMessage request = new();
            request.Method = HttpMethod.Get;
            request.Headers.Add("tokenid", Token);
            //var uid = order.Type switch { TransferOrderType.FirstTrade or TransferOrderType.Buy => 2070, _ => 4040 };
            request.RequestUri = new Uri($"https://vipfunds.simu800.com/vip-manager/manager/signFlowOffline/getSignFlowOffline?signFlowId={order.ExternalId}&t={DateTime.Now.TimeStampByMilliseconds()}");

            var response = client.Send(request);

            var json = await response.Content.ReadAsStringAsync();
            SigningLoger.LogRun(this, nameof(QueryOrderAsync), $"{order.ExternalId}", json);
            if (Regex.IsMatch(json, "token已失效|重新登录"))
            {
                isLogin = false;
                return false;
            }

            var root = JsonSerializer.Deserialize<RootJson>(json);

            if (root is null) return false;

            var data = root.data.Deserialize<OrderFilesRootBJson>();
            if (data is null) return false;

            foreach (var f in data.Files)
            {
                var fname = $"{order.InvestorName}-{order.Date:yyyy.MM.dd}-{f.DocumentName}";
                var (stream, fn) = await Download(f.SealedUrl!, fname);

                if (f.CodeType == 125)
                    order.OrderSheet = new SimpleFile(FileMeta.Create(stream, fn));
                else if (f.CodeType == 123)//(f.DocumentName.Contains("合同"))
                    order.Contract = new SimpleFile(FileMeta.Create(stream, fn));
                else if (f.CodeType == 122)//(f.DocumentName.Contains("风险揭示"))
                    order.RiskDiscloure = new SimpleFile(FileMeta.Create(stream, fn));
                else if (f.CodeType == 121)//if (f.DocumentName.Contains("告知书"))
                    order.RiskPair = new SimpleFile(FileMeta.Create(stream, fn));
                else if (f.CodeType == 126)//(f.DocumentName.Contains("双录"))
                    order.Videotape = new SimpleFile(FileMeta.Create(stream, fn));
            }

            return true;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return false;
        }
    }

    public async Task<bool> QueryOrderAsync(TransferOrder order)
    {
        if (_signTypeDict.TryGetValue(order.ExternalId!, out var sr) && sr == 2)
            return await QueryOrderAsyncB(order);

        return await QueryOrderAsyncA(order);
    }

    public async Task<EsigningFundInfo[]> QueryFundInfo()
    {
        if (!IsValid) return [];
        if (!isLogin) isLogin = await Login();
        if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return []; }


        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/product/queryByProductName");
        request.Headers.Add("tokenid", Token);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = client.Send(request);

        var cont = await response.Content.ReadAsStringAsync();

        SigningLoger.LogRun(this, nameof(QueryFundInfo), "", cont);
        if (Regex.IsMatch(cont, "token已失效|重新登录"))
        {
            isLogin = false;
            return [];
        }

        var root = JsonSerializer.Deserialize<RootJson>(cont);
        if (root is null) return [];

        var data = root.data.Deserialize<FundInfoJson[]>();

        return data?.Select(x => new EsigningFundInfo { Id = x.ProductId.ToString(), Name = x.ProductFullName, Code = x.FundRecordNumber, Class = x.FundRecordNumber != x.PbInternalProductCode ? x.PbInternalProductCode : "" }).ToArray() ?? [];
    }



    internal async Task<string> UploadFile(string filePath, int codeType, string id = "")
    {
        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/attachments/uploadFile");
        request.Headers.Add("tokenid", Token);

        using var formData = new MultipartFormDataContent("----WebKitFormBoundary3XaysDrvZo0L8reE");
        formData.Add(new StringContent("6"), "source");
        formData.Add(new StringContent("undefined"), "productId");
        formData.Add(new StringContent(codeType.ToString()), "codeType");
        formData.Add(new StringContent(id), "id");

        // 3. 添加二进制文件（file字段，对应(二进制)）
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var fileContent = new StreamContent(fileStream);
        // 通用文件类型，可根据实际文件修改（如image/png、application/pdf）
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        var fileName = Path.GetFileName(filePath);
        // 第三个参数是文件名，必须正确
        formData.Add(fileContent, "file", fileName);

        request.Content = formData;

        var response = client.Send(request);

        var cont = await response.Content.ReadAsStringAsync();

        SigningLoger.LogRun(this, nameof(QueryFundInfo), "", cont);
        if (Regex.IsMatch(cont, "token已失效|重新登录"))
        {
            isLogin = false;
            return "";
        }

        return cont;
    }

    public async Task<bool> UploadDisclosureFile(string fundName, string fundCode, string shareClass, DateTime time, string announceName, string file)
    {
        if (!IsValid) return false;
        if (!isLogin) isLogin = await Login();
        if (!isLogin) { LogEx.Error("MeiShi Login Failed"); return false; }


        // 获取对应产品
        var funds = await QueryFundInfo();
        var fund = funds.FirstOrDefault(x => x.Name == fundName && x.Code == fundCode);
        if (fund?.Id is null)
            return false;


        // 上传文件
        var fileJson = await UploadFile(file, 131);

        if (!fileJson.Contains("1008"))
            return false;

        // 创建公告
       return await CreateDisclosure(fund.Id, fileJson, file, time, announceName);

    }

    private async Task<bool> CreateDisclosure(string fundId, string fileJson, string filePath, DateTime time, string announceName)
    {
        var root = JsonSerializer.Deserialize<RootJson>(fileJson);
        if (root is null) return false;

        var fileData = root.data.Deserialize<FileUploadDataDto>();
        if (fileData is null) return false;

        string uid = $"rc-upload-{DateTimeHelper.TimeStampByMilliseconds(DateTime.Now)}-1";

        var fo = new UploadFileInfo
        {
            Uid = uid,
            Name = fileData.FileName!,
            Size = new FileInfo(filePath).Length,
            Type = "application/pdf",
            Status = "done",
            OriginFileObj = new() { Uid = uid },
            Percent = 100,
            Response = root,
            Xhr = new()
        };


        var obj = new FundReportUploadRequest
        {
            DocumentName = announceName,
            DocumentType = 3,
            FileAuthority = new List<int> { 3 },
            DisclosureConditions = 0,
            PublishStatus = 1,
            SendEmail = 0,
            NoticeStatus = 0,
            ProductIdList = $"{fundId}",
            StartStatus = 1,
            AttachmentsId = fileData.AttachmentsId,
            PublishTime = DateTimeHelper.TimeStampByMilliseconds(time),
            FileName = "",
            FileUrl = "",
            MrpReportLibraryLogId = "",
            SourceFile = 1,
            ProductContactMap = new Dictionary<string, object>(),

            // 文件信息（按你的JSON赋值）
            File = new FileBag
            {
                File = fo,
                FileList = [fo]
            }
        };


        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/productFile/create");
        request.Headers.Add("tokenid", Token);
        request.Content = new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

        var response = client.Send(request);

        var cont = await response.Content.ReadAsStringAsync();

        SigningLoger.LogRun(this, nameof(QueryFundInfo), "", cont);
        if (Regex.IsMatch(cont, "token已失效|重新登录"))
        {
            isLogin = false;
            return false;
        }

        root = JsonSerializer.Deserialize<RootJson>(cont);
        if (root is null) return false;
         
        return root.code == 1008;
    }

    private static string? GetFileExtentionFromUrl(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            return Path.GetExtension(uri.LocalPath);
        }
        catch
        {
            return null;
        }
    }


    private async Task UpdateIdFile(int investorId, AttatchmentInfo att)
    {
        List<Stream> streams = new();
        foreach (var url in att.documentUrls!)
        {
            var stream = await client.GetStreamAsync(url);
            streams.Add(stream);
        }

        using var merged = MergeCardPhoto(streams);
        if (merged is null)
            return;

        using var db = DbHelper.Base();
        var cert = db.GetCollection<InvestorCertifications>().FindById(investorId) ?? new() { Id = investorId };
        cert.Files.Add(FileMeta.Create(merged!, $"身份证正反面.jpg"));

        db.GetCollection<InvestorCertifications>().Upsert(cert);


    }

    private BitmapSource LoadBitmapFromFile(Stream stream)
    {
        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    public MemoryStream? MergeCardPhoto(List<Stream> streams)
    {
        try
        {
            if (streams.Count != 2)
                return null;

            // 加载图像
            BitmapSource bitmap1 = LoadBitmapFromFile(streams[0]);
            BitmapSource bitmap2 = LoadBitmapFromFile(streams[1]);

            if (bitmap1 == null || bitmap2 == null)
            {
                LogEx.Error("合并投资人证件：无法加载图片，请检查文件格式");
                return null;
            }

            // 计算总宽度和最大高度
            int totalWidth = bitmap1.PixelWidth + bitmap2.PixelWidth;
            int maxHeight = Math.Max(bitmap1.PixelHeight, bitmap2.PixelHeight);

            // 创建绘图目标
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.DrawImage(bitmap1, new Rect(0, 0, bitmap1.PixelWidth, bitmap1.PixelHeight));
                context.DrawImage(bitmap2, new Rect(bitmap1.PixelWidth, 0, bitmap2.PixelWidth, bitmap2.PixelHeight));
            }

            // 渲染为位图
            RenderTargetBitmap rtb = new RenderTargetBitmap(
                totalWidth, maxHeight,
                96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            // 保存为 JPG 文件
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 90; // 设置画质
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            var ms = new MemoryStream();
            encoder.Save(ms);

            return ms;
        }
        catch (Exception ex)
        {
            LogEx.Error($"合并投资人证件出错 {ex}");
            return null;
        }
    }


    public async Task<(Stream stream, string fname)> Download(string url, string name)
    {
        if (string.IsNullOrWhiteSpace(url)) return default;

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var buf = await response.Content.ReadAsByteArrayAsync();
        return (new MemoryStream(buf), name + GetFileExtentionFromUrl(url));
    }

    public void OnConfig(ISigningConfig config)
    {

    }

}


file static class LoginEncryptor
{
    private const string Key = "4B19127F45A2DAF7"; // 16 字节，AES-128

    public static string EncryptLoginData(object loginData)
    {
        // 1. 序列化为 JSON（无空格，与 JS 的 JSON.stringify 一致）
        var json = JsonSerializer.Serialize(loginData);

        // 2. UTF-8 编码
        byte[] plainBytes = Encoding.UTF8.GetBytes(json);

        // 3. AES-128-ECB + PKCS7 填充
        using (var aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(Key);
            aes.Mode = CipherMode.ECB;          // ECB 模式
            aes.Padding = PaddingMode.PKCS7;    // PKCS7 填充

            using (var encryptor = aes.CreateEncryptor())
            {
                byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                // 4. Base64 编码（与 CryptoJS.toString() 一致）
                return Convert.ToBase64String(encrypted);
            }
        }
    }

    // 便捷方法：直接传入字段
    public static string EncryptLogin(string userName, string password, string loginType = "1", int passwordType = 1)
    {
        var data = new
        {
            loginType,
            userName,
            password,
            passwordType,
            authorize = new { } // 空对象
        };
        return EncryptLoginData(data);
    }


}