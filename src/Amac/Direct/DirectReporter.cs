
//#define TEST_PFID

using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;


namespace FMO.AMAC.Direct;


public enum DirectFileType
{
    Unk,

    [Description("私募月报")] PB0001,


    [Description("私募季报")] PB0002,


    [Description("私募年报")] PB0003,


    [Description("私募半年报")] PB0004,



    [Description("私募产品运行表")] RS0001,

    [Description("私募产品清算报告")] RS0002,

    [Description("私募基金年度财务监测报告")] RS0010,

    [Description("量化私募基金运行报表")] RS0030,

    [Description("规模以上证券类管理人报表")] RS0031,

    [Description("不动产私募投资基金监测报表")] RS0050,

    /// <summary>
    /// RS0051, //不动产私募投资基金监测报表（托管人）
    /// </summary>
    ///[Description("不动产私募投资基金监测报表")] RS0051,  
}



public class DirectReporter
{

#if TEST_PFID
    public const string OperationUploadUrl = "http://amrs-test.amac.org.cn:8280/pmg/v1/report/direct-file.do";
    public const string DisclosureUploadUrl = "http://pfid-test.amac.org.cn:8480/pof/v1/report/direct-file.do";


    public const string OperationResultUrl = "http://amrs-test.amac.org.cn:8280/pmg/v1/report/direct-query.do";
    public const string DisclosureResultUrl = "http://pfid-test.amac.org.cn:8480/pof/v1/report/direct-query.do";

    public const string OperationSubmitUrl = "http://amrs-test.amac.org.cn:8280/pmg/v1/report/direct-submit.do";
    public const string DisclosureSubmitUrl = "http://pfid-test.amac.org.cn:8480/pof/v1/report/direct-submit.do";

#elif TEST_PFID2

    public const string OperationUploadUrl = "http://amrs-stage.amac.org.cn:8180/pmg/v1/report/direct-file.do";
    public const string DisclosureUploadUrl = "http://pfid-stage.amac.org.cn:8380/pof/v1/report/direct-file.do";


    public const string OperationResultUrl = "http://amrs-stage.amac.org.cn:8180/pmg/v1/report/direct-query.do";
    public const string DisclosureResultUrl = "http://pfid-stage.amac.org.cn:8380/pof/v1/report/direct-query.do";

    public const string OperationSubmitUrl = "http://amrs-stage.amac.org.cn:8180/pmg/v1/report/direct-submit.do";
    public const string DisclosureSubmitUrl = "http://pfid-stage.amac.org.cn:8380/pof/v1/report/direct-submit.do";

#else
    public const string OperationUploadUrl = "https://amrs.amac.org.cn/pmg/v1/report/direct-file.do";
    public const string DisclosureUploadUrl = "https://pfid.amac.org.cn/pof/v1/report/direct-file.do";


    public const string OperationResultUrl = "https://amrs.amac.org.cn/pmg/v1/report/direct-query.do";
    public const string DisclosureResultUrl = "https://pfid.amac.org.cn/pof/v1/report/direct-query.do";

    public const string OperationSubmitUrl = "https://amrs.amac.org.cn/pmg/v1/report/direct-submit.do";
    public const string DisclosureSubmitUrl = "https://pfid.amac.org.cn/pof/v1/report/direct-submit.do";

#endif

    // 全局唯一静态锁（所有函数共用）
    private static readonly SemaphoreSlim _slimLock = new(1, 1);

    // 静态上次执行时间
    private static DateTime _lastExecuteTime = DateTime.MinValue;

    // 3秒限制
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(3);

    public static async Task<AmacProcessResult> UploadReport(FundPeriodicReport report, AmacReportAccount acc) => await UploadReport(report, x => x.Excel?.File, acc);
    public static async Task<AmacProcessResult> UploadReport(FundQuarterlyUpdate report, AmacReportAccount acc) => await UploadReport(report, x => x.Operation?.File, acc);

    public static async Task<AmacProcessResult> UploadReport<T>(T report, Func<T, FileMeta?> file, AmacReportAccount acc) where T : IPeriodical
    {
        var type = report.Type switch
        {
            FundReportType.MonthlyReport => DirectFileType.PB0001,
            FundReportType.QuarterlyReport => DirectFileType.PB0002,
            FundReportType.SemiAnnualReport => DirectFileType.PB0004,
            FundReportType.AnnualReport => DirectFileType.PB0003,
            FundReportType.QuarterlyUpdate => DirectFileType.RS0001,
            _ => DirectFileType.Unk
        };

        if (report.FundCode is null) return new AmacProcessResult { Id = report.Id, FileType = type, UploadError = "基金备案编码为空" };
        if (type == DirectFileType.Unk) return new AmacProcessResult { Id = report.Id, FileType = type, UploadError = "未知的报告类型" };
        if (file(report) is not FileMeta fm || !fm.Exists) return new AmacProcessResult { Id = report.Id, FileType = type, UploadError = "报告文件不存在" };

        var date = new DateOnly(report.PeriodEnd.Year, report.PeriodEnd.Month, 1).AddMonths(1).AddDays(-1);
        // 生成zip
        var path = Path.GetTempFileName();
        try
        {
            using (var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry($"CN_{report.FundCode}_{type}_{date:yyyy-MM-dd}.xlsx");
                using (var entryStream = entry.Open())
                {
                    using var fs = fm.OpenRead();
                    if (fs is null) return new AmacProcessResult { Id = report.Id, FileType = type, UploadError = "无法读取文件" };

                    fs.CopyTo(entryStream);
                    entryStream.Flush();
                }

                if (report.Type == FundReportType.AnnualReport && report is FundPeriodicReport fp && fp.Sealed?.File is FileMeta fmm)
                {
                    entry = archive.CreateEntry($"01_{report.FundCode}_年报.pdf");
                    using (var es = entry.Open())
                    {
                        using (var ffs = fmm.OpenRead())
                        {
                            if (ffs is null) return new AmacProcessResult { Id = report.Id, FileType = type, UploadError = "无法读取年报附件" };
                            ffs.CopyTo(es);
                            es.Flush();
                        }
                    }
                }
            }

            using var db = DbHelper.Base();
            var manager = db.GetCollection<Manager>().Query().First();
            var result = await UploadFile(type, path, date, acc, manager.Name, report.FundCode);
            var r = new AmacProcessResult { Id = report.Id, FileType = type, Handle = result!.Handle, UploadCode = result.ProcessCode switch { "00" or "100" => 0, var n => int.Parse(n!) }, UploadError = result.ProcessMessage };

            db.GetCollection<AmacProcessResult>().Upsert(r);
            return r;
        }
        catch (Exception ex) { LogEx.Error(ex); return new AmacProcessResult { Id = report.Id, FileType = type, UploadError = ex.Message }; }
        finally { File.Delete(path); }
    }


    /// <summary>
    /// 参数验证在调用前完成
    /// </summary>
    /// <param name="fileType"></param>
    /// <param name="filePath"></param>
    /// <param name="reportEndDate"></param>
    /// <param name="acc"></param>
    /// <param name="managerName"></param>
    /// <param name="entityCode"></param>
    /// <returns></returns>
    public static async Task<ProcessResponse?> UploadFile(DirectFileType fileType, string filePath, DateOnly reportEndDate, AmacReportAccount acc, string managerName, string entityCode)
    {
        try
        {
            // 异步加锁（不会阻塞线程）
            await _slimLock.WaitAsync();

            var now = DateTime.Now;
            var waitTime = _interval - (now - _lastExecuteTime);

            // 异步等待剩余时间（安全、不卡）
            if (waitTime > TimeSpan.Zero)
                await Task.Delay(waitTime);


            string UserName = acc.Name;
            string DirectPwd = acc.Password;
            string PublicKey = acc.Key;


            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "imgfornote");

            // 生成认证头部
            var pwd = Sm3Utils.Encrypt32(DirectPwd);
            var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var sign = Sm3Utils.Encrypt(UserName + salt + pwd);


            HttpRequestMessage request = new HttpRequestMessage { Method = HttpMethod.Post };
            request.Headers.Add("userName", UserName);
            request.Headers.Add("salt", salt);
            request.Headers.Add("sign", sign);
            request.Headers.Add("User-Agent", "imgfornote");



            // 读取文件内容
            var fileBytes = File.ReadAllBytes(filePath);
            var base64File = Convert.ToBase64String(fileBytes);

            // 计算校验和
            var checksum = Sm3Utils.Encrypt(fileBytes);
            checksum = Sm2Utils.Encrypt(PublicKey, checksum!);

            // 构建请求体
            var requestContent = new
            {
                entityCode = entityCode,
                reportType = $"{fileType}",
                reportEndDate = $"{reportEndDate:yyyy-MM-dd}",
                xbrlFileName = $"CN_{entityCode}_{fileType}_{reportEndDate:yyyy-MM-dd}.zip",
                subCompany = managerName,
                body = base64File,
                checksum = checksum,
            };

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
                WriteIndented = false,
            };
            var json = JsonSerializer.Serialize(requestContent, jsonOptions);

            // 发送请求
            request.RequestUri = new Uri(fileType < DirectFileType.RS0001 ? DisclosureUploadUrl : OperationUploadUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            //await PrintHttpRequestMessageAsync(request);

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            Debug.WriteLine(responseContent);

            // 解析响应
            var result = JsonSerializer.Deserialize<ProcessResponse>(responseContent);

            // 更新最后执行时间
            _lastExecuteTime = DateTime.Now;
            return result;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            return null;
        }
        finally
        {
            // 释放锁
            _slimLock.Release();
        }
    }


    //public static async Task<IList<ValidationInfo>> QueryResult(IPeriodical periodical, AmacReportAccount acc)
    //{
    //    using var db = DbHelper.Base();
    //    var id = db.GetCollection<AmacDirectHandle>().FindById(periodical.Id);
    //    var results = await QueryResult(id, acc);
    //    db.GetCollection<AmacDirectHandle>().Update(id with { ResultInfo = results });
    //    return results;
    //}

    public static async Task QueryResult(AmacProcessResult handle, AmacReportAccount acc)
    {
        try
        {
            // 异步加锁（不会阻塞线程）
            await _slimLock.WaitAsync();

            var now = DateTime.Now;
            var waitTime = _interval - (now - _lastExecuteTime);

            // 异步等待剩余时间（安全、不卡）
            if (waitTime > TimeSpan.Zero)
                await Task.Delay(waitTime);


            string UserName = acc.Name;
            string DirectPwd = acc.Password;
            string PublicKey = acc.Key;

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "imgfornote");

            // 生成认证头部
            var pwd = Sm3Utils.Encrypt32(DirectPwd);
            var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var sign = Sm3Utils.Encrypt(UserName + salt + pwd);


            HttpRequestMessage request = new HttpRequestMessage { Method = HttpMethod.Post };
            request.Headers.Add("userName", UserName);
            request.Headers.Add("salt", salt);
            request.Headers.Add("sign", sign);
            request.Headers.Add("User-Agent", "imgfornote");

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键：不转义中文
                WriteIndented = false // 是否格式化（可选）
            };
            var json = JsonSerializer.Serialize(new { handle = new string[] { handle.Handle } }, jsonOptions);

            // 发送请求
            request.RequestUri = new Uri(handle.FileType < DirectFileType.RS0001 ? DisclosureResultUrl : OperationResultUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");


            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<List<ValidationResultItem>>(responseContent);

            if (result?.FirstOrDefault() is ValidationResultItem root)
            {
                handle.ValidateCode = int.Parse(root.processCode);

                if (root.processCode == "00")
                    handle.ResultInfo = [new ValidationInfo { Level = "Success", Message = "" }];
                else
                    handle.ResultInfo = root.verifyMessage?.children?.Where(x => x.children is not null)?.
                       SelectMany(x => x.children.Select(y => new ValidationInfo { Level = y.deepLevel, Message = y.description }))?.ToArray() ?? [];
            }
            else
                handle.ResultInfo = [new ValidationInfo { Level = "Error", Message = "未获取到返回信息" }];

            using var db = DbHelper.Base();
            db.GetCollection<AmacProcessResult>().Upsert(handle);

            // 更新最后执行时间
            _lastExecuteTime = DateTime.Now;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }
        finally
        {
            // 释放锁
            _slimLock.Release();
        }
    }


    public static async Task Submit(AmacProcessResult handle, string company, AmacReportAccount acc)
    {
        try
        {
            // 异步加锁（不会阻塞线程）
            await _slimLock.WaitAsync();

            var now = DateTime.Now;
            var waitTime = _interval - (now - _lastExecuteTime);

            // 异步等待剩余时间（安全、不卡）
            if (waitTime > TimeSpan.Zero)
                await Task.Delay(waitTime);


            string UserName = acc.Name;
            string DirectPwd = acc.Password;
            string PublicKey = acc.Key;

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "imgfornote");

            // 生成认证头部
            var pwd = Sm3Utils.Encrypt32(DirectPwd);
            var salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var sign = Sm3Utils.Encrypt(UserName + salt + pwd);


            HttpRequestMessage request = new HttpRequestMessage { Method = HttpMethod.Post };
            request.Headers.TryAddWithoutValidation("userName", UserName);
            request.Headers.Add("salt", salt);
            request.Headers.Add("sign", sign);
            request.Headers.Add("User-Agent", "imgfornote");

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键：不转义中文
                WriteIndented = false // 是否格式化（可选）
            };
            var json = JsonSerializer.Serialize(new { handle = handle.Handle, subCompany = company }, jsonOptions);

            // 发送请求
            request.RequestUri = new Uri(handle.FileType < DirectFileType.RS0001 ? DisclosureSubmitUrl : OperationSubmitUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");


            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            var pr = JsonSerializer.Deserialize<ProcessResponse>(responseContent);
            if (pr is null)
            {
                handle.SubmitError = "Json Error";
                return;
            }
            handle.SubmitCode = int.Parse(pr.ProcessCode!);
            handle.SubmitError = handle.SubmitCode == 0 ? null : pr.ProcessMessage;
            using var db = DbHelper.Base();
            db.GetCollection<AmacProcessResult>().Upsert(handle);

            // 更新最后执行时间
            _lastExecuteTime = DateTime.Now;
        }
        catch (Exception e)
        {
            LogEx.Error(e);
        }
        finally
        {
            // 释放锁
            _slimLock.Release();
        }
    }


    // <summary>
    /// 异步打印 HttpRequestMessage 的详细信息（用于调试）
    /// </summary>
    public static async Task PrintHttpRequestMessageAsync(HttpRequestMessage request)
    {
        Debug.WriteLine("============== HTTP REQUEST ==============");

        // 1. 请求方法和 URL
        Debug.WriteLine($"Method: {request.Method}");
        Debug.WriteLine($"URL: {request.RequestUri}");

        // 2. Headers
        Debug.WriteLine("Headers:");
        foreach (var header in request.Headers)
        {
            Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        }

        // 如果有 Content，也打印 Content-Type 和 Content Headers
        if (request.Content != null)
        {
            var contentType = request.Content.Headers.ContentType;
            if (contentType != null)
            {
                Debug.WriteLine($"  Content-Type: {contentType}");
            }
        }

        // 3. Body
        Debug.WriteLine("Body:");
        if (request.Content != null)
        {
            // 读取 body（注意：读取后需要重新设置，否则发送时可能为空）
            var body = await request.Content.ReadAsStringAsync();
            Debug.WriteLine(body);
        }
        else
        {
            Debug.WriteLine("<empty>");
        }

        Debug.WriteLine("==========================================");
    }


    public static string GetStatusMessage(string? code)
    {
        return code switch
        {
            "00" => "直连报送成功",
            "01" => "鉴权错误",
            "02" => "直连机构不存在",
            "03" => "报告主体未设置或设置有误",
            "04" => "机构未启用直连报送",
            "05" => "未支持的报告类型",
            "06" => "错误的报告截止日",
            "07" => "无法自动创建报告",
            "08" => "业务数据格式错误，无法解析",
            "10" => "报告已提交，无法重复提交，请到协会端处理",
            "11" => "数据校验错误，请修改后重报。可到协会端查看，定位错误数据。",
            "12" => "数据校验警告提示，请修改后重报，或确认无误后再点击确认报送。",
            "21" => "报告文件校验码验证错误",
            "22" => "上报过于频繁",
            "23" => "XBRL文件名错误",
            "24" => "报送确认时间太短",
            "90" => "直连报送已超时，请重报",
            "91" => "直连接口调用被临时禁用",
            "99" => "程序异常：XXXX，请联系系统管理员，建议先协会端手工处理。",
            "100" => "直连报送正在处理中",
            "101" => "操作机构名称必填写",
            _ => $"未知状态码：{code}"
        };
    }
}

public record AmacDirectHandle(int Id, DirectFileType FileType, string Handle, IList<ValidationInfo>? ResultInfo = null, bool Submit = false);


public class AmacProcessResult
{
    /// <summary>
    /// 同 report id
    /// </summary>
    public int Id { get; set; }

    public DirectFileType FileType { get; set; }

    public int UploadCode { get; set; } = -1;

    public string? UploadError { get; set; }

    public string? Handle { get; set; }

    public int ValidateCode { get; set; } = -1;

    public IList<ValidationInfo>? ResultInfo { get; set; }

    public string? SubmitError { get; set; }
    public int SubmitCode { get; set; } = -1;
}