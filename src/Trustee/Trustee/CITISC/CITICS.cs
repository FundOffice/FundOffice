using FMO.Models;
using FMO.Trustee.JsonCITICS;
using FMO.Utilities;
using LiteDB;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FMO.Trustee;

public partial class CITICS : TrusteeApiBase
{
    public const string _Identifier = "trustee_citics";


    SemaphoreSlim tokenSlim = new SemaphoreSlim(1);

    private readonly int SizePerPage = 500;

    public override string Identifier => "trustee_citics";

    public override string Title => "中信证券";

    public override string TestDomain { get; } = "https://apitest-iservice.citicsinfo.com";

    public override string Domain { get; } = "https://api.iservice.citics.com";

    public string? CustomerAuth { get; set; }

    public string? Token { get; set; }

    private DateTime? TokenTime { get; set; }

    public override bool IsSuit(string? company) => string.IsNullOrWhiteSpace(company) ? false : Regex.IsMatch(company, $"中信证券股份有限公司|中信证券|{_Identifier}");

    public override async Task<ReturnWrap<Investor>> QueryInvestors()
    {
        var part = "/v1/ta/queryCustInfoForApi";

        var r = await SyncWork<InvestorJson, InvestorJson>(part, null, x => x);

        // 需要保存基金账号到投资人映射，因为TA返回数据没有投资人信息
        if (r.Data?.Length > 0)
        {
            using var db = DbHelper.Platform();
            db.GetCollection<InvestorAccountMapping>(Identifier).Upsert(r.Data.Select(x => new InvestorAccountMapping { Id = x.FundAcco, Indentity = x.CertiNo, Name = x.CustName }));
        }

        return new(r.Code, r.Data?.Select(x => x.ToObject()).ToArray());
    }


    //public override async Task<TransferRecord[]?> SyncTransferRecords(DateOnly begin, DateOnly end)
    //{
    //    var data = await Query<TransferRecordJson>("/v1/ta/TradeConfirmationForApi", [("ackBeginDate", begin.ToString("yyyyMMdd")), ("ackEndDate", end.ToString("yyyyMMdd"))]);

    //    // 检查错误
    //    if (data.Code != 0) ;


    //    // 保存
    //    using var db = DbHelper.Platform();
    //    db.GetCollection<TransferRecordJson>(Identifier).Upsert(data.Data);

    //    // 所有的基金账号
    //    var accouts = data.Data.Select(x => x.TradeAcco).Distinct().ToList();




    //    return [];

    //}

    public override async Task<ReturnWrap<FundDailyFee>> QueryFundDailyFee(DateOnly begin, DateOnly end)
    {
        var part = "/v1/fm/queryFeeInfoForApi";

        var result = await SyncWork<FundDailyFee, FundDailyFeeJson>(part, new { startDate = $"{begin:yyyyMMdd}", endDate = $"{end:yyyyMMdd}" }, x => x.ToObject());

        return result;
    }


    public override async Task<ReturnWrap<TransferRequest>> QueryTransferRequests(DateOnly begin, DateOnly end)
    {
        var part = "/v2/ta/queryTradeApplyForApi";
        var result = await SyncWork<TransferRequestJson, TransferRequestJson>(part, new { ackBeginDate = $"{begin:yyyyMMdd}", ackEndDate = $"{end:yyyyMMdd}" }, x => x);


        // 后处理
        if (result.Data?.Length > 0)
        {
            List<TransferRequest> list = new();
            // 映射基金名、投资人
            using (var b = DbHelper.Base())
            {
                foreach (var item in result.Data)
                {
                    var r = item.ToObject();
                    if (b.FindFund(r.FundCode) is Fund f)
                    {
                        // 检查子份额 SABCDE  ABCDEA/B/C..
                        if (f.Code != r.FundCode)
                        {
                            r.FundName = f.Name + r.FundCode![5..];
                            r.FundCode = f.Code;
                        }
                        else
                            r.FundName = f.Name;
                    }
                    else Log($"CITICS QueryTransferRequests 未知的基金{item.FundCode}");

                    list.Add(r);
                }
            }


            return new(result.Code, list.ToArray());
        }

        return new(result.Code, null);
    }


    public override Task<ReturnWrap<SubjectFundMapping>> QuerySubjectFundMappings()
    {
        return Task.FromResult(new ReturnWrap<SubjectFundMapping>(ReturnCode.NotImplemented, null));
    }



    public override async Task<ReturnWrap<TransferRecord>> QueryTransferRecords(DateOnly begin, DateOnly end)
    {
        var part = "/v1/ta/TradeConfirmationForApi";
        var result = await SyncWork<TransferRecordJson, TransferRecordJson>(part, new { ackBeginDate = $"{begin:yyyyMMdd}", ackEndDate = $"{end:yyyyMMdd}" }, x => x);

        // 后处理
        if (result.Data?.Length > 0)
        {
            // 获取待处理账号列表
            var unk = result.Data.Select(x => x.FundAcco).Distinct().ToList();


            // 获取账户映射
            using var db = DbHelper.Platform();
            var map = db.GetCollection<InvestorAccountMapping>(Identifier).FindAll().ToArray();

            // 检查是否匹配
            var kn = map.Select(x => x.Id).ToArray();
            if (kn.Intersect(unk).Count() != unk.Count)
            {
                // 重新获取
                await QueryInvestors();
                map = db.GetCollection<InvestorAccountMapping>(Identifier).FindAll().ToArray();
            }

            List<TransferRecord> list = new();
            // 映射基金名、投资人
            using (var b = DbHelper.Base())
            {
                foreach (var item in result.Data)
                {
                    var r = item.ToObject();
                    if (b.FindFund(r.FundCode) is Fund f)
                    {
                        // 检查子份额 SABCDE  ABCDEA/B/C..
                        if (f.Code != r.FundCode)
                        {
                            r.FundName = f.Name + r.FundCode![5..];
                            r.FundCode = f.Code;
                        }
                        else
                            r.FundName = f.Name;
                    }

                    list.Add(r);

                    if (map.FirstOrDefault(x => x.Id == item.FundAcco) is InvestorAccountMapping m)
                    {
                        r.CustomerName = m.Name;
                        r.CustomerIdentity = m.Indentity;
                    }
                }
            }

            // 排除失败的
            return new(result.Code, list.Where(x => x.Source != "failed").ToArray());
        }

        return new(result.Code, null);
    }




    /// <summary>
    /// 募集户余额
    /// </summary>
    /// <param name="fundCode"></param>
    /// <returns></returns>
    public async Task<ReturnWrap<BankBalance>> QueryRaisingBalance(string fundCode)
    {
        var part = "/v1/fs/queryRaiseAccBalForApi";

        var r = await SyncWork<BankBalance, RaisingBalanceJson>(part, null, x => x.ToObject());

        return r;
    }

    public override async Task<ReturnWrap<FundBankBalance>> QueryRaisingBalance()
    {
        var part = "/v1/fs/queryRaiseAccBalForApi";

        var r = await SyncWork<FundBankBalance, RaisingBalanceJson>(part, null, x => x.ToObject());

        return r;
    }

    public override async Task<ReturnWrap<RaisingBankTransaction>> QueryRaisingAccountTransction(DateOnly begin, DateOnly end)
    {
        var part = "/v1/fs/queryRaiseAccFlowForApi";

        var ts = Split(begin, end, 90);
        List<RaisingBankTransaction> transactions = new();
        foreach (var (b, e) in ts)
        {
            var result = await SyncWork<RaisingBankTransaction, BankTransactionJson>(part, new { beginDate = $"{b:yyyyMMdd}", endDate = $"{e:yyyyMMdd}" }, x => x.ToObject());
            if (result.Code != ReturnCode.Success) return result;

            if (result.Data is not null)
                transactions.AddRange(result.Data);
        }

        // 对齐到基金
        using var db = DbHelper.Base();
        foreach (var item in transactions)
            item.FundId = db.FindFund(item.FundCode)?.Id ?? 0;

        if (transactions.Any(x => x.FundId == 0))
            Serilog.Log.Error($"募资流水 {string.Join(',', transactions.Where(x => x.FundId == 0).Select(x => $"{x.AccountName}-{x.FundCode}"))} 未找到对应基金");

        return new(ReturnCode.Success, transactions.ToArray());
    }

    public override async Task<ReturnWrap<BankTransaction>> QueryCustodialAccountTransction(DateOnly begin, DateOnly end = default)
    {
        if (end == default) end = begin;

        if (begin == DateOnly.FromDateTime(DateTime.Today))
        {
            //当日 
            var part = "/v1/cs/queryTgAccountCurrentFlowForApi";
            var result = await SyncWork<BankTransaction, CustodialTransactionJson>(part, null, x => x.ToObject());
            return result;
        }
        else
        {
            //历史 
            var part = "/v1/cs/queryTgAccountHistoryFlowForApi";
            var result = await SyncWork<BankTransaction, CustodialTransactionJson2>(part, null, x => x.ToObject());
            return result;
        }
    }
    public async Task<ReturnWrap<BankTransaction>> QueryCustodialAccountTransction(string code, DateOnly begin, DateOnly end = default)
    {
        if (end == default) end = begin;

        if (begin == DateOnly.FromDateTime(DateTime.Today))
        {
            //当日 
            var part = "/v1/cs/queryTgAccountCurrentFlowForApi";
            var result = await SyncWork<BankTransaction, CustodialTransactionJson>(part, new { pdCode = code }, x => x.ToObject());
            return result;
        }
        else
        {
            //历史 
            var part = "/v1/cs/queryTgAccountHistoryFlowForApi";
            var result = await SyncWork<BankTransaction, CustodialTransactionJson2>(part, new { pdCode = code }, x => x.ToObject());
            return result;
        }
    }



    /// <summary>
    /// 查询银行信息
    /// 流量控制策略：50次/天，1次/秒（生产环境）
    /// 保存到
    /// </summary>
    /// <returns></returns>
    public async Task<ReturnWrap<FundBankAccount>> QueryCustodialAccountInfo()
    {
        var part = "/v1/cs/queryTgAccountListForApi";
        var result = await SyncWork<FundBankAccount, CustodialAccountJson>(part, null, x => x.ToObject());

        if (result.Code == ReturnCode.Success && result.Data is not null)
        {
            Fund[] funds;
            using (var db = DbHelper.Base())
                funds = db.GetCollection<Fund>().FindAll().ToArray();

            foreach (var d in result.Data)
            {
                if (funds.FirstOrDefault(x => x.Code == d.FundCode) is Fund o)
                    d.FundId = o.Id;
            }


            // 更新
            using (var db = DbHelper.Platform())
            {
                var old = db.GetCollection<FundBankAccount>().FindAll().ToArray();
                // 同步id
                //foreach (var d in result.Data)
                //{
                //if (old.FirstOrDefault(x => x.Number == d.Number) is FundBankAccount o)
                //    d.Id = o.Id;
                //}
                db.GetCollection<FundBankAccount>().Upsert(result.Data);
            }
        }
        return result;
    }


    /// <summary>
    /// 获取虚拟净值
    /// </summary>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public async Task<ReturnWrap<VirtualNetValueJson>> QueryVirtualNetValue(DateOnly begin, DateOnly end/*, params string?[] codes*/)
    {
        var part = "v1/ta/queryVirtualTaProfitForApi";
        var result = await SyncWork<VirtualNetValueJson, VirtualNetValueJson>(part, null, x => x);
        return result;
    }


    /// <summary>
    /// 托管户银行实时余额查询接口
    /// </summary>
    /// <param name="fundCode"></param>
    /// <returns></returns>
    public async Task<ReturnWrap<BankBalance>> QueryCustodialBalance(string fundCode)
    {
        var part = "/v1/cs/queryTgAccountBalanceForApi";

        // 获取托管账户列表
        using var db = DbHelper.Platform();
        var acc = db.GetCollection<FundBankAccount>().Find(x => x.FundCode == fundCode && !x.IsCanceled).ToList();

        if (acc.Count == 0)
            return new(ReturnCode.ParameterInvalid, null);

        List<BankBalance> balances = new();

        foreach (var item in acc)
        {
            var r = await SyncWork<BankBalance, BankBalanceJson>(part, new { pdCode = fundCode, bankName = item.BankOfDeposit, account = item.Number }, x => x.ToObject());

            if (r.Code != ReturnCode.Success) return new(r.Code, null);

            if (r.Data is not null)
                balances.AddRange(r.Data);
        }



        return new(ReturnCode.Success, balances.ToArray());
    }


    /// <summary>
    /// 托管户银行实时余额查询接口
    /// </summary>
    /// <param name="fundCode"></param>
    /// <returns></returns>
    public async Task<ReturnWrap<BankBalance>> QueryCustodialBalance(string fundCode, string bank, string account)
    {
        var part = "/v1/cs/queryTgAccountBalanceForApi";

        var r = await SyncWork<BankBalance, BankBalanceJson>(part, new { pdCode = fundCode, bankName = bank, account = account }, x => x.ToObject());

        return r;
    }


    public async Task<ReturnWrap<TransferRecord>> QueryDistibution(DateOnly begin, DateOnly end)
    {
        var part = "/v1/ta/queryDividendForApi";
        var result = await SyncWork<TransferRecord, DistrubutionJson>(part, new { beginDate = $"{begin:yyyyMMdd}", endDate = $"{end:yyyyMMdd}" }, x => x.ToObject());

        return result;
    }


    public async Task<ReturnWrap<PerformanceJson>> QueryPerformance(DateOnly begin, DateOnly end)
    {
        var part = "/v1/ta/queryTaProfitForApi";
        var result = await SyncWork<PerformanceJson, PerformanceJson>(part, new { beginDate = $"{begin:yyyyMMdd}", endDate = $"{end:yyyyMMdd}" }, x => x);

        return result;
    }


    public override async Task<ReturnWrap<DailyValue>> QueryNetValue(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        var part = "/v1/fa/queryFundNetValForApi";

        // 查询区间大于1年，需要多次查询 
        var ts = Split(begin, end, 365);
        List<NetValueJson> list = new();

        foreach (var (b, e) in ts)
        {
            object param = fundCode?.Length > 0 ? new { netBeginDate = $"{b:yyyyMMdd}", netEndDate = $"{e:yyyyMMdd}", fundCode = fundCode } : new { netBeginDate = $"{b:yyyyMMdd}", netEndDate = $"{e:yyyyMMdd}" };

            var data = await SyncWork<NetValueJson, NetValueJson>(part, param, x => x);
            if (data.Code != ReturnCode.Success)
                return new ReturnWrap<DailyValue>(data.Code, []);

            if (data.Data?.Length > 0)
                list.AddRange(data.Data);
        }

        using var db = DbHelper.Base();


        List<DailyValue> ret = new List<DailyValue>(list.Count);
        foreach (var item in list.GroupBy(x => x.FundCode))
        {
            var code = item.Key;

            if (db.FindFundByCode(code) is var (ff, c) && ff is Fund f)
            {
                ret.AddRange(item.Select(x => new DailyValue
                {
                    FundId = f.Id,
                    Class = c,
                    Date = DateOnly.ParseExact(x.NetDate, "yyyyMMdd"),
                    NetValue = ParseDecimal(x.NetAssetVal),
                    CumNetValue = ParseDecimal(x.TotalAssetVal),
                    Asset = ParseDecimal(x.AssetValue),
                    Share = ParseDecimal(x.AssetShares),
                    NetAsset = ParseDecimal(x.AssetNet),
                    Source = DailySource.Custodian
                }));
            }
            else JsonBase.ReportJsonUnexpected(Identifier, "QueryNetValue", $"Fund Code = {code}");
        }

        return new ReturnWrap<DailyValue>(ReturnCode.Success, ret.ToArray());
    }


    //public   async Task<ReturnWrap<BankTransaction>> QueryShare(DateOnly begin, DateOnly end)
    //{
    //    var part = "/v1/fs/queryRaiseAccFlowForApi";
    //    var result = await SyncWork<BankTransaction, BankTransactionJson>(part, new { beginDate = $"{begin:yyyyMMdd}", endDate = $"{end:yyyyMMdd}" }, x => x.ToObject());

    //    return result;
    //}























    public async Task<string?> GetToken()
    {
        await tokenSlim.WaitAsync();
        try
        {
            if (TokenTime is not null && (DateTime.Now - TokenTime.Value).TotalHours < 18)
                return Token;

            var cc = await _client.GetStringAsync("https://www.baidu.com");

            string? requestUri = GetUrl("/v1/auth/getToken");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("consumerAuth", CustomerAuth);

            var response = await _client.SendAsync(request);


            var json = await response.Content.ReadAsStringAsync();

            var obj = JsonSerializer.Deserialize<ReturnJsonRoot<TokenJson>>(json);

            // 更新
            if (obj?.Data?.Token is string s)
            {
                Token = s;
                TokenTime = DateTime.Now;
                SaveConfig();
            }


            return obj?.Data?.Token;
        }
        catch (Exception e)
        {
            Log(nameof(GetToken), "", $"CheckCertificate {e}");
            return null;
        }
        finally { tokenSlim.Release(); }
    }

    #region Functional

    private async Task<string> Query(string part, Dictionary<string, object> param)
    {
        var request = Build(part, param);

        var respone = await _client.SendAsync(request);
        var content = await respone.Content.ReadAsStringAsync();

        return content;
    }


    protected async Task<ReturnWrap<TEntity>> SyncWork<TEntity, TJSON>(string part, object? param, Func<TJSON, TEntity> transfer, [CallerMemberName] string? caller = null) where TJSON : JsonBase
    {
        // 校验
        if (CheckBreforeSync() is ReturnCode rc && rc != ReturnCode.Success) return new(rc, null);

        // 检查token
        if (string.IsNullOrWhiteSpace(Token) || TokenTime is null || (DateTime.Now - TokenTime.Value).TotalHours > 18)
            await GetToken();


        // 非dict 转成dict 方便修改page
        Dictionary<string, object> formatedParams;
        if (param is Dictionary<string, object> pp) formatedParams = pp;
        else formatedParams = GenerateParams(param);

        List<TJSON> list = new();

        // 获取所有结果
        string json = "";
        try
        {
            for (int i = 0; i < 19; i++) // 防止无限循环，最多99次 
            {
                json = await Query(part, formatedParams);
                LogRun(caller, formatedParams, json);

                try
                {
                    if (json is null) return new(ReturnCode.EmptyResponse, null);

                    // token expired
                    if (json.Contains("Token expired", StringComparison.OrdinalIgnoreCase))
                    {
                        await GetToken();
                        continue;
                    }

                    var ret = JsonSerializer.Deserialize<RootJson>(json)!;

                    var code = ret.Code;
                    // 有错误
                    if (code != 0)
                    {
                        if (ret.Data is JsonObject obj && obj.ContainsKey("reason"))
                            ret.Msg = ret.Data["reason"]?.ToString();

                        Log(caller, json, ret.Msg);
                        return new(TransferReturnCode(code, ret.Msg), null);
                    }

                    if (ret.Data is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Contains("No Data", StringComparison.OrdinalIgnoreCase))
                        break;

                    var data = ret.Data.Deserialize<QueryRoot<JsonElement>>()!;

                    // 记录返回的类型，用于debug
                    //if (data.List is not null)
                    //    CacheJson(caller, data!.List);

                    if (data.List is not null)
                        list.AddRange(data.List.Select(x =>
                        {
                            try { return x.Deserialize<TJSON>()!; }
                            catch (Exception ex)
                            {
                                // 记录具体哪个元素反序列化失败
                                JsonBase.ReportJsonUnexpected(Identifier, caller!, $"Failed to deserialize item Error: {ex.Message}: {x}.");
                                throw;
                            }
                        }));

                    if (data.Total == 0) break;

                    var page = data.PageNum;// (int)formatedParams["pageNum"];

                    // 数据获取全 
                    if (page >= data.Pages)
                        break;

                    // 下一页
                    formatedParams["pageNum"] = page + 1;
                }
                catch (Exception e)
                {
                    Log(caller, json, $"Json Serialize Error {e.Message}");
                    return new(ReturnCode.JsonNotPairToEntity, null);
                }
            }
        }
        catch (Exception e)
        {
            Log(caller, json, e.Message);
            return new(ReturnCode.Unknown, null);
        }

        Log(caller, null, list.Count == 0 ? "OK [Empty]" : $"OK [{list[0].Id}-{list[^1].Id}]");
        return new(ReturnCode.Success, list.Select(x => transfer(x)).ToArray());
    }


    protected override bool LoadConfigOverride(IAPIConfig config)
    {
        if (config is not APIConfig c)
            return false;

        CustomerAuth = c.CustomerAuth;
        Token = c.Token;
        TokenTime = c.TokenTime;
        return true;
    }

    public override bool Prepare()
    {

        return true;
    }

    protected override IAPIConfig SaveConfigOverride()
    {
        return new APIConfig { CustomerAuth = CustomerAuth, Token = Token, TokenTime = TokenTime };
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="code"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private ReturnCode TransferReturnCode(int code, string? message = null)
    {
        switch (code)
        {
            case 0:
                return ReturnCode.Success;
            case 401:
                return ReturnCode.CITICS_NoTokenOrAuth;

            case 403:
                if (message?.Contains("credentials") ?? false)
                    return ReturnCode.CITICS_Credentials;
                else if (message?.Contains("consume this service") ?? false)
                    return ReturnCode.AccessDenied;
                return ReturnCode.CITICS_Token;

            case 404:
                return ReturnCode.InterfaceUnavailable;
            case 429:
                return ReturnCode.CITICS_Limited;

            default:
                if (message?.Contains("API rate limit exceeded", StringComparison.OrdinalIgnoreCase) ?? false)
                    return ReturnCode.TrafficLimit;
                return ReturnCode.Unknown;
        }
    }

    protected override Dictionary<string, object> GenerateParams(object? obj)
    {
        var dic = base.GenerateParams(obj);

        if (!dic.Any(x => x.Key == "pageSize"))
            dic.Add("pageSize", SizePerPage);
        if (!dic.Any(x => x.Key == "pageNum"))
            dic.Add("pageNum", 1);

        return dic;
    }


    protected HttpRequestMessage Build(string part, Dictionary<string, object> param)
    {
        var url = GetUrl(part)!;
        if (param.Count > 0)
            url = $"{url}?" + string.Join('&', param.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value?.ToString() ?? "")}"));


        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("consumerAuth", CustomerAuth);
        if (!string.IsNullOrWhiteSpace(Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        return request;
    }

    #endregion


    protected override ReturnCode CheckBreforeSync()
    {
        // 检验可用性 
        if (!IsValid) return ReturnCode.ConfigInvalid;

        if (string.IsNullOrWhiteSpace(CustomerAuth) /*|| string.IsNullOrWhiteSpace(Token)*/)
        {
            SetDisabled();
            return ReturnCode.ConfigInvalid;
        }

        return ReturnCode.Success;
    }

    public class APIConfig : IAPIConfig
    {
        public string Id { get; } = "trustee_citics";

        public string? CustomerAuth { get; set; }

        public string? Token { get; set; }

        public DateTime? TokenTime { get; set; }

    }

}
