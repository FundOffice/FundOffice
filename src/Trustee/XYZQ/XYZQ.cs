using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace FMO.Trustee;

public class XYZQ : TrusteeApiBase
{
    public const string _Identifier = "trustee_xyzq";

    public override string Identifier => _Identifier;

    public override string Title => "兴业证券";
    public override string TestDomain => "http://101.95.178.246:8089";

    public override string Domain => "https://glr.xyzq.cn";


    public string? ClientId { get; set; }


    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string? ClientSecret { get; set; }

    public string? AToken { get; private set; }
    public string? FToken { get; private set; }

    private DateTime TokenTime { get; set; }

    private IList<SubjectFundMapping>? FundMappings { get; set; }


    private IList<BankAccount>? RaisingAccount { get; set; }

    public override bool IsSuit(string? company) => string.IsNullOrWhiteSpace(company) ? false : Regex.IsMatch(company, $"兴业证券|兴业证券股份有限公司|{_Identifier}");


    public override bool Prepare()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password) || ClientSecret is null)
            SetStatus();

        return true;
    }


    public async Task<bool> GetToken()
    {
        // 有token 可以刷新
        if (!string.IsNullOrWhiteSpace(AToken) && !string.IsNullOrWhiteSpace(FToken) && await RefreshToken())
            return true;

        // 重新申请

        var url = GetUrl("/pposapi?servername=GetApiAuthCode");
        var param = new Dictionary<string, string>
        {
            ["client_id"] = ClientId!,
            ["username"] = UserName!,
            ["password"] = Password!,
            ["response_type"] = "code",
            ["redirect_uri"] = "http://101.95.178.246:8089/testppos"
        };
        var response = await _client.PostAsync(url, new FormUrlEncodedContent(param));

        /////////////////////
        var resjson = await response.Content.ReadAsStringAsync();
        var resobj = JsonNode.Parse(resjson);
        // 有有效token，直接返回
        if (resobj?["access_token"] is JsonNode ano)
        {
            AToken = ano.GetValue<string>();
            return true;
        }

        if (resobj?["code"] is not JsonNode node)
        {
            LogEx.Error($"XYZQ Auth Failed {resobj?["msg"]}");
            return false;
        }

        var auth = node.GetValue<string>();


        url = GetUrl($"/pposapi?servername=GetApiToken&client_id={ClientId}&client_secret={ClientSecret}&code={auth}&grant_type=authorization_code&redirect_uri=http://101.95.178.246:8089/testppos");

        response = await _client.PostAsync(url, new FormUrlEncodedContent([]));
        resjson = await response.Content.ReadAsStringAsync();
        resobj = JsonNode.Parse(resjson);

        var rnode = resobj?["refresh_token"];
        var tnode = resobj?["refresh_token"];
        if (tnode is null || rnode is null)
        {
            LogEx.Error($"XYZQ GetToken Failed {resobj?["msg"]}");
            return false;
        }
        AToken = tnode.GetValue<string>();
        FToken = rnode.GetValue<string>();
        TokenTime = DateTime.Now;

        return true;
    }


    public async Task<bool> RefreshToken()
    {
        var url = GetUrl("/pposapi?servername=RefreshToken");
        var param = new
        {
            client_id = ClientId,
            refresh_token = FToken,
            access_token = AToken
        };

        var json = JsonSerializer.Serialize(param);
        var response = await _client.PostAsync(url, new StringContent(json));
        var resjson = await response.Content.ReadAsStringAsync();
        var resobj = JsonNode.Parse(resjson);

        var rnode = resobj?["refreshToken"];
        var tnode = resobj?["accessToken"];
        if (tnode is null || rnode is null)
        {
            LogEx.Error($"XYZQ GetToken Failed {resobj?["msg"]}");
            return false;
        }
        AToken = tnode.GetValue<string>();
        FToken = rnode.GetValue<string>();
        TokenTime = DateTime.Now;
        return true;
    }

    protected override ReturnCode CheckBreforeSync()
    {
        // 检验可用性 
        if (!IsValid) return ReturnCode.ConfigInvalid;

        return ReturnCode.Success;
    }

    public override Task<ReturnWrap<BankTransaction>> QueryCustodialAccountTransction(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        throw new NotImplementedException();
    }

    public override Task<ReturnWrap<FundDailyFee>> QueryFundDailyFee(DateOnly begin, DateOnly end)
    {
        throw new NotImplementedException();
    }

    public override async Task<ReturnWrap<Investor>> QueryInvestors()
    {
        var server = "TaInverstorQuery";

        var result = await SyncWork<Investor, InvestorJson>(server, null, x => x.ToObject());
        return result;
    }

    public override Task<ReturnWrap<DailyValue>> QueryNetValue(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        throw new NotImplementedException();
    }

    public override Task<ReturnWrap<RaisingBankTransaction>> QueryRaisingAccountTransction(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        throw new NotImplementedException();
    }


    public async Task<ReturnWrap<BankAccount>> QueryRaisingAccount()
    {
        var server = "QueryAccountInfo";
        var result = await SyncWork<BankAccount, AccountInfoJson>(server, new { accounttype = "2" }, x => x.ToBank());
        if (result.Code == ReturnCode.Success && result.Data?.Count > 0)
            RaisingAccount = result.Data;
        return result;
    }

    /// <summary>
    /// 纯SB设计
    /// </summary>
    /// <returns></returns>
    public override async Task<ReturnWrap<FundBankBalance>> QueryRaisingBalance()
    {
        //if (RaisingAccount is null)
        //    await QueryRaisingAccount();

        //if (RaisingAccount is [])
        //    return new(ReturnCode.NoFund, []);

        var server = "GetRecruitmentHistoryBalance";
        var param = new { date = $"{DateTime.Now:yyyy-MM-dd}", fundcode = "", account = "" };
        var result = await SyncWork<FundBankBalance, FundBankBalanceJson>(server, param, x => x.ToObject());
        return result;
    }

    public override async Task<ReturnWrap<SubjectFundMapping>> QuerySubjectFundMappings()
    {
        int pageId = 1;

        if (!await EnsureToken()) return new(ReturnCode.XYZQ_CanNotGetToken, []);

        var url = GetUrl($"/pposapi?servername=productList&access_token={AToken}&version=V1.0&currentpage={pageId}&y=5000");

        HttpRequestMessage request = new(new HttpMethod("GET"), url);

        var map = await SyncWork<SubjectFundMapping, SubjectFundMappingJson>("productList", null, x => x.ToObject());

        FundMappings = map.Data ?? [];
        return new(ReturnCode.Success, []);
    }


    public async Task<bool> EnsureToken()
    {
        if (string.IsNullOrWhiteSpace(AToken) && !await GetToken())
        {
            SetStatus();
            return false;
        }
        return true;
    }


    public override async Task<ReturnWrap<TransferRecord>> QueryTransferRecords(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        if (fundCode is not null)
        {
            if (FundMappings is null)
                await QuerySubjectFundMappings();

            if (FundMappings is null || FundMappings.Count == 0)
                return new(ReturnCode.XYZQ_FundCode, []);

            fundCode = FundMappings.FirstOrDefault(x => x.AmacCode == fundCode)?.FundCode;
        }


        var result = await SyncWork<TransferRecord, RecordJson>("TaDataConfirmQuery", new { scdate = begin.ToString("yyyyMMdd"), ecdate = end.ToString("yyyyMMdd"), fundcode = fundCode }, x => x.ToObject());

        // code 
        if (result.Data is not null)
        {
            using var db = DbHelper.Base();
            var dic = db.GetCollection<Fund>().Query().Select(x => new { x.Code, x.Id }).ToList().ToDictionary(x => x.Code!, x => x.Id);
            foreach (var item in result.Data.GroupBy(x => x.FundCode))
            {
                var code = FundMappings?.FirstOrDefault(x => x.FundCode == item.Key)?.AmacCode;
                dic.TryGetValue(code!, out var id);
                foreach (var g in item)
                {
                    g.FundCode = code;
                    g.FundId = id;
                }
            }
        }

        return result;
    }

    public override async Task<ReturnWrap<TransferRequest>> QueryTransferRequests(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        if (FundMappings is null)
            await QuerySubjectFundMappings();

        if (FundMappings is null || FundMappings.Count == 0)
            return new(ReturnCode.XYZQ_NoFundIssued, []);

        if (fundCode is null)
            fundCode = string.Join(',', FundMappings.Select(x => x.FundCode));
        else fundCode = FundMappings.FirstOrDefault(x => x.AmacCode == fundCode)?.FundCode;

        if (fundCode is null)
            return new(ReturnCode.XYZQ_FundCode, []);

        var result = await SyncWork<TransferRequest, RequestJson>("QueryTradeApplication", new { startdate = begin.ToString("yyyyMMdd"), enddate = end.ToString("yyyyMMdd"), fundcode = fundCode }, x => x.ToObject());

        return result;
    }


    protected override Dictionary<string, object> GenerateParams(object? obj)
    {
        var param = base.GenerateParams(obj);
        param.Add("currentpage", 1);
        param.Add("rows", 5000);

        return param;
    }
    protected async Task<string> Query(string server, Dictionary<string, object> parameters)
    {
        // 创建request
        //parameters["servername"] = server;

        var part = string.Join('&', [$"servername={server}&access_token={AToken}&version=V1.0", .. parameters.Select(x => $"{x.Key}={HttpUtility.UrlEncode(x.Value.ToString())}")]);

        var url = GetUrl($"/pposapi?{part}");
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }


    protected async Task<ReturnWrap<TEntity>> SyncWork<TEntity, TJSON>(string server, object? param, Func<TJSON, TEntity> transfer, [CallerMemberName] string caller = "")
    {
        // 校验
        if (CheckBreforeSync() is ReturnCode rc && rc != ReturnCode.Success) return new(rc, null);

        if (!await EnsureToken()) return new(ReturnCode.XYZQ_CanNotGetToken, []);

        // 非dict 转成dict 方便修改page
        Dictionary<string, object> formatedParams;
        if (param is null) formatedParams = new();
        if (param is Dictionary<string, object> pp) formatedParams = pp;
        else formatedParams = GenerateParams(param);

        List<TJSON> list = new();

        try
        {
            for (int i = 0; i < 19; i++)
            {
#if DEBUG
                string? json = TrusteeApiBase.GetCache(Identifier, caller, formatedParams);
                if (json is null) { json = await Query(server, formatedParams); SetCache(Identifier, caller, formatedParams, json!); }
                else Debug.WriteLine($"{Identifier},{caller} Load From Cache");
#else
                var json = await Query(server, formatedParams);
#endif

                // 解析返回json 
                try
                {
                    var root = JsonSerializer.Deserialize<JsonRoot>(json);
                    if (root!.returnCode == "-3") // token失效
                    {
                        if (!await GetToken())
                        {
                            SetStatus();
                            return new(ReturnCode.IdentitifyFailed, []);
                        }
                        --i;
                        continue;
                    }

                    // 其它原因失败
                    if (root.returnCode != "1")
                    {
                        LogEx.Error($"{caller} {root.msg}");
                        return new(ReturnCode.Unknown, []);
                    }

                    if (root.resultDataSet is not null && root.resultDataSet.GetValueKind() == JsonValueKind.Array)
                        list.AddRange(root.resultDataSet.AsArray().Select(x =>
                        {
                            try { return x.Deserialize<TJSON>()!; }
                            catch (Exception ex)
                            {
                                // 记录具体哪个元素反序列化失败
                                JsonBase.ReportJsonUnexpected(Identifier, caller!, $"Failed to deserialize item Error: {ex.Message}: {x}.");
                                throw;
                            }
                        }));

                    if (root.resultPage.currentPage >= root.resultPage.totalPage) break;

                    // 下一页
                    var page = (int)formatedParams["currentpage"];
                    formatedParams["currentpage"] = page + 1;

                }
                catch (Exception e)
                {
                    LogEx.Error(e);
                    return new(ReturnCode.JsonNotPairToEntity, []);
                }
            }
        }
        catch (Exception e)
        {
            Log(caller, null, e.Message);
            return new(ReturnCode.Unknown, []);
        }

        if (list.Count > 0)
            CacheJson(caller, list);
        Log(caller, null, list.Count == 0 ? "OK [Empty]" : $"OK [{list.Count}]");

        try { var dd = list.Select(x => transfer(x)).ToArray(); return new(ReturnCode.Success, dd); }
        catch (Exception e) { Log(e.Message); return new(ReturnCode.ObjectTransformError, []); }
    }


    protected override bool LoadConfigOverride(IAPIConfig config)
    {
        if (config is not APIConfig c) return false;

        this.CloneFrom(c);

        return true;
    }

    protected override IAPIConfig SaveConfigOverride()
    {
        var c = new APIConfig();
        c.CloneFrom(this);
        return c;
    }

    protected override async Task<bool> VerifyConfigOverride()
    {
        try { return (await QuerySubjectFundMappings()).Code == ReturnCode.Success; } catch { return false; }
    }
}


internal class APIConfig : IAPIConfig
{
    public string Id { get; } = XYZQ._Identifier;


    public string? ClientId { get; set; }


    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string? ClientSecret { get; set; }

    public string? Token { get; set; }


}
