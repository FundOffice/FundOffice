using FMO.Models;
using FMO.Trustee.JsonCMS;
using FMO.Utilities;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace FMO.Trustee;

public partial class CMS : TrusteeApiBase
{
    public const string _Identifier = "trustee_cms";

    public override string Identifier => "trustee_cms";

    public override string Title => "招商证券";

    public override string TestDomain => "http://210.21.237.135:27011/v1/request";

    public override string Domain => "https://tgjjapi.newone.com.cn/v1/request";



    public string? CompanyId { get; set; }

    public int? ServerType { get; set; }

    public string? LicenceKey { get; set; }

    public string? UserNo { get; set; }

    public byte[]? PFX { get; set; }

    public string? Password { get; set; }

    public X509Certificate2? Certificate { get; set; }


    private SubjectFundMapping[]? FundsInfo { get; set; }


    public override bool IsSuit(string? company) => string.IsNullOrWhiteSpace(company) ? false : Regex.IsMatch(company, $"招商证券|招商证券股份有限公司|{_Identifier}");



    public override async Task<ReturnWrap<SubjectFundMapping>> QuerySubjectFundMappings()
    {
        var data = await SyncWork<SubjectFundMapping, SubjectFundMappingJson>(1018, null, x => x.ToObject());

        if (data.Code == ReturnCode.Success && data.Data?.Length > 0)
            FundsInfo = data.Data.ToArray();
        return data;
    }



    public override async Task<ReturnWrap<TransferRequest>> QueryTransferRequests(DateOnly begin, DateOnly end)
    {
        var data = await SyncWork<TransferRequest, TransferRequestJson>(1006, new { beginDate = $"{begin:yyyyMMdd}", endDate = $"{end:yyyyMMdd}" }, x => x.ToObject());

        // 无数据返回
        if (data.Data?.Length == 0) return data;

        // 子产品 映射
        if (FundsInfo is null)
            await QuerySubjectFundMappings();

        if (data.Code == ReturnCode.Success && data.Data is not null)
        {
            foreach (var item in data.Data)
            {
                if (FundsInfo?.FirstOrDefault(x => x.FundCode == item.FundCode) is SubjectFundMapping sfm && sfm.MasterCode is not null)
                {
                    item.FundCode = sfm.MasterCode;
                    item.FundName = sfm.MasterName!;
                    if (!string.IsNullOrWhiteSpace(sfm.ShareClass))
                        item.ShareClass = sfm.ShareClass;
                }
            }
        }
        return data;
    }



    public override async Task<ReturnWrap<TransferRecord>> QueryTransferRecords(DateOnly begin, DateOnly end)
    {
        var data = await SyncWork<TransferRecord, TransferRecordJson>(1006, new { beginDate = $"{begin:yyyyMMdd}", endDate = $"{end:yyyyMMdd}" }, x => x.ToObject());

        // 子产品 映射
        if (FundsInfo is null)
            await QuerySubjectFundMappings();

        if (data.Code == ReturnCode.Success && data.Data is not null)
        {
            foreach (var item in data.Data)
            {
                if (FundsInfo?.FirstOrDefault(x => x.FundCode == item.FundCode) is SubjectFundMapping sfm && sfm.MasterCode is not null)
                {
                    item.FundCode = sfm.MasterCode;
                    item.FundName = sfm.MasterName;
                    if (!string.IsNullOrWhiteSpace(sfm.ShareClass))
                        item.ShareClass = sfm.ShareClass;
                }
            }
        }
        return data;
    }


    public override async Task<ReturnWrap<FundDailyFee>> QueryFundDailyFee(DateOnly begin, DateOnly end)
    {
        // 查询区间大于1个月，需要多次查询 
        var ts = Split(begin, end, 31);

        List<FundDailyFee> transactions = new();
        foreach (var (b, e) in ts)
        {
            var data = await SyncWork<FundDailyFee, FundDailyFeeJson>(1020, new { beginDate = $"{b:yyyyMMdd}", endDate = $"{e:yyyyMMdd}" }, x => x.ToObject());
            if (data.Code != ReturnCode.Success)
                return data;

            if (data.Data?.Length > 0)
                transactions.AddRange(data.Data);
        }
        return new(ReturnCode.Success, transactions.ToArray());
    }




    public override Task<ReturnWrap<BankTransaction>> QueryCustodialAccountTransction(DateOnly begin, DateOnly end)
    {
        throw new NotImplementedException();
    }


    public override async Task<ReturnWrap<FundBankBalance>> QueryRaisingBalance()
    {
        var data = await SyncWork<FundBankBalance, BankBalanceJson>(1001, null, x => x.ToObject());
        return data;
    }


    public override async Task<ReturnWrap<RaisingBankTransaction>> QueryRaisingAccountTransction(DateOnly begin, DateOnly end)
    {
        // 查询区间大于1个月，需要多次查询 
        var ts = Split(begin, end, 30);

        List<RaisingBankTransaction> transactions = new();

        foreach (var (b, e) in ts)
        {
            var data = await SyncWork<RaisingBankTransaction, RaisingBankTransactionJson>(1002, new { ksrq = $"{b:yyyyMMdd}", jsrq = $"{e:yyyyMMdd}" }, x => x.ToObject());
            if (data.Code != ReturnCode.Success)
                return data;

            if (data.Data?.Length > 0)
                transactions.AddRange(data.Data);
        }

        // 对齐到基金
        using var db = DbHelper.Base();
        foreach (var item in transactions)
            item.FundId = db.FindFund(item.FundCode)?.Id ?? 0;

        if (transactions.Any(x => x.FundId == 0))
            Serilog.Log.Error($"募资流水 {string.Join(',', transactions.Where(x => x.FundId == 0).Select(x => $"{x.AccountName}-{x.FundCode}"))} 未找到对应基金");

        return new(ReturnCode.Success, transactions.ToArray());
    }


    public override async Task<ReturnWrap<Investor>> QueryInvestors()
    {
        var data = await SyncWork<Investor, InvestorJson>(1008, null, x => x.ToObject());
        return data;
    }



    public override async Task<ReturnWrap<DailyValue>> QueryNetValue(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        var b = begin; var e = end;
        object param = fundCode?.Length > 0 ? new { beginDate = $"{b:yyyyMMdd}", endDate = $"{e:yyyyMMdd}", fundCode = fundCode } : new { beginDate = $"{b:yyyyMMdd}", endDate = $"{e:yyyyMMdd}" };

        var data = await SyncWork<NetValueJson, NetValueJson>(1005, param, x => x);

        // map
        if (data.Code != ReturnCode.Success || data.Data is null)
            return new ReturnWrap<DailyValue>(data.Code, []);

        using var db = DbHelper.Base();


        List<DailyValue> ret = new List<DailyValue>(data.Data.Length);
        foreach (var item in data.Data.GroupBy(x => x.FundCode))
        {
            var code = item.Key;
            if (db.FindFundByCode(code) is var (ff, c) && ff is Fund f)
            {
                ret.AddRange(item.Select(x => new DailyValue
                {
                    FundId = f.Id,
                    Class = c,
                    Date = DateOnly.ParseExact(x.NavDate, "yyyyMMdd"),
                    NetValue = ParseDecimal(x.Nav),
                    CumNetValue = ParseDecimal(x.AccumulativeNav),
                    Asset = ParseDecimal(x.TotalAsset),
                    Share = ParseDecimal(x.AssetVol),
                    NetAsset = ParseDecimal(x.AssetNav),
                    Source = DailySource.Custodian
                }));
            }
            else JsonBase.ReportJsonUnexpected(Identifier, "QueryNetValue", $"Fund Code = {code}");
        }

        return new ReturnWrap<DailyValue>(ReturnCode.Success, ret.ToArray());
    }

    //////////////////////////////////////////////////////////////////////////////////////////


    public override bool Prepare()
    {
        InitCertificate();

        if (string.IsNullOrWhiteSpace(CompanyId) || string.IsNullOrWhiteSpace(LicenceKey) || string.IsNullOrWhiteSpace(UserNo) || ServerType is null || Certificate is null)
            SetDisabled();

        return true;
    }


    protected override bool LoadConfigOverride(IAPIConfig config)
    {
        if (config is not APIConfig c) return false;

        CompanyId = c.CompanyId;
        LicenceKey = c.LicenceKey;
        ServerType = c.ServerType;
        UserNo = c.UserNo;
        PFX = c.PFX;
        Password = c.Password;

        return true;
    }

    protected override IAPIConfig SaveConfigOverride()
    {
        return new APIConfig { CompanyId = CompanyId, LicenceKey = LicenceKey, ServerType = ServerType, UserNo = UserNo, PFX = PFX, Password = Password };
    }



    public void InitCertificate()
    {
        if (PFX is not null && !string.IsNullOrWhiteSpace(Password))
        {
            try
            {
                Certificate = X509CertificateLoader.LoadPkcs12(PFX, Password, X509KeyStorageFlags.Exportable);
            }
            catch { }
        }
    }



    public async Task<string?> Query(int interfaceId, Dictionary<string, object> param)
    {
        // 创建request
        var request = Build(interfaceId, param);
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }

    protected async Task<ReturnWrap<TEntity>> SyncWork<TEntity, TJSON>(int interfaceId, object? param, Func<TJSON, TEntity> transfer, [CallerMemberName] string? caller = null) where TJSON : JsonBase
    {
        // 校验
        if (CheckBreforeSync() is ReturnCode rc && rc != ReturnCode.Success) return new(rc, null);

        // 非dict 转成dict 方便修改page
        Dictionary<string, object> formatedParams;
        if (param is null) formatedParams = new();
        if (param is Dictionary<string, object> pp) formatedParams = pp;
        else formatedParams = GenerateParams(param);

        List<TJSON> list = new();

        // 获取所有结果
        try
        {
            for (int i = 0; i < 19; i++) // 防止无限循环，最多99次 
            {

                var json = await Query(interfaceId, formatedParams);

                try
                {
                    if (json is null) return new(ReturnCode.EmptyResponse, null);
                    var ret = JsonSerializer.Deserialize<JsonRoot>(json);

                    var code = int.Parse(ret!.Code);

                    // 有错误
                    if (code != 10000)
                    {
                        Log(caller, json, ret.Msg);
                        return new(TransferReturnCode(code, ret.Msg), null);
                    }

                    // 调用成功，实际无数据
                    if (string.IsNullOrWhiteSpace(ret.Data))
                        break;// return new(ReturnCode.Success, []);

                    // 解析实际数据
                    var data = JsonSerializer.Deserialize<List<JsonElement>>(ret.Data);

                    // 记录返回的类型，用于debug
                    //CacheJson(caller, data!);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true, 
                    };
                    if (data is not null && data.Count > 0)
                        list.AddRange(data.Select(x =>
                        {
                            try { return x.Deserialize<TJSON>(options)!; }
                            catch (Exception ex)
                            {
                                // 记录具体哪个元素反序列化失败
                                JsonBase.ReportJsonUnexpected(Identifier, caller!, $"Failed to deserialize item Error: {ex.Message}: {x}.");
                                throw;
                            }
                        }));


                    // 数据获取是否齐全
                    var pi = JsonSerializer.Deserialize<PaginationInfo>(ret.Page!)!;
                    if (pi.PageNumber >= pi.PageCount)
                        break;

                    // 下一页
                    var page = (int)formatedParams["pageNumber"];
                    formatedParams["pageNumber"] = page + 1;
                }
                catch
                {
                    Log(caller, json, "Json Serialize Error");
                    return new(ReturnCode.JsonNotPairToEntity, null);
                }
            }
        }
        catch (Exception e)
        {
            Log(caller, null, e.Message);
            return new(ReturnCode.Unknown, null);
        }

        Log(caller, null, list.Count == 0 ? "OK [Empty]" : $"OK [{list[0].Id}-{list[^1].Id}]");

        try { var dd = list.Select(x => transfer(x)).ToArray(); return new(ReturnCode.Success, dd); }
        catch(Exception e) { Log(e.Message); return new (ReturnCode.ObjectTransformError, []); }
    }



    private HttpRequestMessage Build(int interfaceId, Dictionary<string, object> param)
    {
        string url = GetUrl("")!;
        var time = DateTime.Now;

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("uap-licence-key", LicenceKey);
        request.Headers.Add("uap-request-no", $"{interfaceId}");
        request.Headers.Add("uap-server-type", $"{ServerType}");
        request.Headers.Add("uap-user-no", UserNo);
        request.Headers.Add("uap-request-id", $"{time.Ticks}");

        var timestamp = time.ToString("yyyyMMddHHmmss");
        var sign = SignatureUtils.GetManagerSignature(CompanyId!, timestamp, Certificate!);


        var businessparam = JsonSerializer.Serialize(param);
        string post = $"companyid={HttpUtility.UrlEncode(CompanyId)}&timestamp={timestamp}&signature={HttpUtility.UrlEncode(sign)}&businessparam={HttpUtility.UrlEncode(businessparam)}";
        request.Content = new StringContent(post, Encoding.UTF8, "application/x-www-form-urlencoded");

        return request;
    }


    protected override ReturnCode CheckBreforeSync()
    {
        // 检验可用性 
        if (!IsValid) return ReturnCode.ConfigInvalid;

        if (string.IsNullOrWhiteSpace(LicenceKey) || string.IsNullOrWhiteSpace(UserNo) || Certificate is null || ServerType is null)
        {
            SetDisabled();
            return ReturnCode.ConfigInvalid;
        }

        return ReturnCode.Success;
    }


    private ReturnCode TransferReturnCode(int code, string message)
    {
        switch (code)
        {
            case 0000:
            case 10000:
                return ReturnCode.Success;

            case 10001://（请求参数无效） 
                return ReturnCode.ParameterInvalid;

            case 10002://（身份认证失败） 
                return ReturnCode.IdentitifyFailed;

            case 10003://（非法IP） 
                return ReturnCode.InvalidIP;

            case 10004://（接口不可用） 
                return ReturnCode.InterfaceUnavailable;


            case 10005:
                switch (message)
                {
                    case string s when s.Contains("不能超过一个月"):
                        return ReturnCode.CMS_DateRangeLimitOneMonth;
                    default:
                        return ReturnCode.Unknown;
                }
            default:
                return ReturnCode.Unknown;
        }
    }

    protected override Dictionary<string, object> GenerateParams(object? obj)
    {
        var param = base.GenerateParams(obj);
        param.Add("pageNumber", 1);
        param.Add("pageSize", 300);

        return param;
    }


    public class APIConfig : IAPIConfig
    {
        public string Id { get; } = "trustee_cms";


        public string? CompanyId { get; set; }

        public int? ServerType { get; set; }

        public string? LicenceKey { get; set; }

        public string? UserNo { get; set; }

        public byte[]? PFX { get; set; }

        public string? Password { get; set; }
    }


}
