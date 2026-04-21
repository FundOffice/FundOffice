using FMO.Models;
using FMO.Trustee.JsonCSC;
using FMO.Utilities;
using LiteDB;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using static FMO.Trustee.SM4;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FMO.Trustee;

/// <summary>
/// 中信建投证券
/// </summary>
public partial class CSC : TrusteeApiBase
{
    public const string _Identifier = "trustee_csc";

    private readonly int SizePerPage = 1000;

    public override string Identifier => "trustee_csc";

    public override string Title => "中信建投证券";

    public override string TestDomain { get; } = "https://zsjg.csc108.com:443/rest";

    public override string Domain { get; } = "https://zsjg.csc108.com:443/rest";

    public string? APIKey { get; set; }

    public string? APISecret { get; set; }

    public string? EncryptKey { get; set; }


    private SM4? Encoder { get; set; }

    private SM4? Decoder { get; set; }


    // 产品列表
    private List<SubjectFundMapping> FundsInfo { get; set; } = new();


    public override bool IsSuit(string? company) => string.IsNullOrWhiteSpace(company) ? false : Regex.IsMatch(company, $"中信建投证券|中信建投证券股份有限公司|{_Identifier}");

    public override async Task<ReturnWrap<TransferRequest>> QueryTransferRequests(DateOnly begin, DateOnly end)
    {
        var part = "/institution/tgpt/erp/product/query/findAckTransList";
        var data = await SyncWork<TransferRequest, TransferRequestJson>(part, new { beginDate = begin.ToString("yyyyMMdd"), endDate = end.ToString("yyyyMMdd") }, x => x.ToObject());


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



    public override async Task<ReturnWrap<SubjectFundMapping>> QuerySubjectFundMappings()
    {
        var part = "/institution/tgpt/erp/file/productFile/findProductList";
        var result = await SyncWork<SubjectFundMapping, SubjectFundMappingJson>(part, null, x => x.ToObject());

        if (result.Data?.Length > 0)
            FundsInfo = [.. result.Data];

        return result;
    }


    public override async Task<ReturnWrap<TransferRecord>> QueryTransferRecords(DateOnly begin, DateOnly end)
    {
        var part = "/institution/tgpt/erp/product/query/findAckTransList";
        var data = await SyncWork<TransferRecord, TransferRecordJson>(part, new { beginDate = begin.ToString("yyyyMMdd"), endDate = end.ToString("yyyyMMdd") }, x => x.ToObject());


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


    public override Task<ReturnWrap<Investor>> QueryInvestors()
    {
        throw new NotImplementedException();
    }


    public override async Task<ReturnWrap<FundDailyFee>> QueryFundDailyFee(DateOnly begin, DateOnly end)
    {
        var part = "/institution/tgpt/erp/product/query/findDailyFeeList";

        // 需要传FundCode
        if (FundsInfo.Count == 0)
            await QuerySubjectFundMappings();

        List<FundDailyFee> list = [];
        foreach (var item in FundsInfo)
        {
            var result = await SyncWork<FundDailyFee, FundDailyFeeJson>(part, new { fundCode = item.FundCode, beginDate = begin.ToString("yyyyMMdd"), endDate = end.ToString("yyyyMMdd") }, x => x.ToObject());
            if (result.Code != ReturnCode.Success)
                return result;

            if (result.Data?.Length > 0)
                list.AddRange(result.Data);
        }

        return new(ReturnCode.Success, list.ToArray());
    }

    /// <summary>
    /// 中信建投不返回 本方户名和银行名
    /// 手动根据产品名设置
    /// </summary>
    /// <param name="begin"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    public override async Task<ReturnWrap<RaisingBankTransaction>> QueryRaisingAccountTransction(DateOnly begin, DateOnly end)
    {
        var part = "/institution/tgpt/erp/raise/query/findRaiseAccountDetailList";
        var result = await SyncWork<RaisingBankTransaction, RaisingBankTransactionJson>(part, new { beginDate = begin.ToString("yyyyMMdd"), endDate = end.ToString("yyyyMMdd") }, x => x.ToObject());


        return result;
    }


    /// <summary>
    /// 02	募集户 04	托管户
    /// </summary>
    /// <returns></returns>
    public override async Task<ReturnWrap<FundBankBalance>> QueryRaisingBalance()
    {
        var part = "/institution/tgpt/erp/product/query/findAccBalanceList";
        var result = await SyncWork<FundBankBalance, BankBalanceJson>(part, new { accType = "02" }, x => x.ToObject());


        return result;
    }





    public override Task<ReturnWrap<BankTransaction>> QueryCustodialAccountTransction(DateOnly begin, DateOnly end)
    {
        throw new NotImplementedException();
    }




    public override async Task<ReturnWrap<DailyValue>> QueryNetValue(DateOnly begin, DateOnly end, string? fundCode = null)
    {
        var part = "/institution/tgpt/erp/product/query/findNetValList";
        var b = begin; var e = end;
        object param = fundCode?.Length > 0 ? new { netDateBegin = $"{b:yyyyMMdd}", netDateEnd = $"{e:yyyyMMdd}", fundCode = fundCode } : new { netDateBegin = $"{b:yyyyMMdd}", netDateEnd = $"{e:yyyyMMdd}" };

        var data = await SyncWork<NetValueJson, NetValueJson>(part, param, x => x);

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
                    Class = x.FundLevel switch
                    {
                        "0" => c,
                        string s when int.TryParse(s, out int level) && level >= 1 && level <= 26 =>
                            ((char)('A' + level - 1)).ToString(),
                        string s => s
                    },
                    Date = DateOnly.ParseExact(x.NetDate, "yyyyMMdd"),
                    NetValue = ParseDecimal(x.NetAssetVal),
                    CumNetValue = ParseDecimal(x.TotalAssetVal),
                    Asset = ParseDecimal(x.AssetTotal),
                    Share = ParseDecimal(x.AssetShares),
                    NetAsset = ParseDecimal(x.AssetNet),
                    Source = DailySource.Custodian
                }));
            }
            else JsonBase.ReportJsonUnexpected(Identifier, "QueryNetValue", $"Fund Code = {code}");
        }

        return new ReturnWrap<DailyValue>(ReturnCode.Success, ret.ToArray());
    }





    protected override ReturnCode CheckBreforeSync()
    {
        // 检验可用性 
        if (!IsValid) return ReturnCode.ConfigInvalid;

        if (string.IsNullOrWhiteSpace(APIKey) || string.IsNullOrWhiteSpace(APISecret) || Encoder is null || Decoder is null)
        {
            SetDisabled();
            return ReturnCode.ConfigInvalid;
        }

        return ReturnCode.Success;
    }


    #region 配置
    protected override bool LoadConfigOverride(IAPIConfig config)
    {
        if (config is not APIConfig c)
            return false;

        APIKey = c.APIKey;
        APISecret = c.APISecret;
        EncryptKey = c.EncryptKey;
        return true;
    }

    public override bool Prepare()
    {
        if (string.IsNullOrWhiteSpace(EncryptKey))
            return false;

        byte[] keyBytes = Encoding.UTF8.GetBytes(EncryptKey);

        Encoder = new SM4();
        Encoder.sm4_setkey_enc(new SM4_Context { isPadding = true }, keyBytes);

        Decoder = new SM4();
        Decoder.sm4_setkey_dec(new SM4_Context { isPadding = true }, keyBytes);



        return true;
    }

    protected override IAPIConfig SaveConfigOverride()
    {
        return new APIConfig { EncryptKey = EncryptKey, APIKey = APIKey, APISecret = APISecret };
    }


    #endregion

    #region 通用处理 




    /// <summary>
    /// 有参
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="func"></param>
    /// <param name="param">object 或 Dictionary<string, object></param>
    /// <returns></returns>
    protected async Task<ReturnWrap<TEntity>> SyncWork<TEntity, TJSON>(string part, object? param, Func<TJSON, TEntity> transfer, [CallerMemberName] string? caller = null) where TJSON : JsonBase
    {
        // 校验
        if (CheckBreforeSync() is ReturnCode rc && rc != ReturnCode.Success) return new(rc, null);

        // 非dict 转成dict 方便修改page
        Dictionary<string, object> formatedParams;
        if (param is null) formatedParams = new();
        if (param is Dictionary<string, object> pp) formatedParams = pp;
        else formatedParams = GenerateParams(param);

        List<TJSON> list = new();

        int total = 0;
        // 获取所有结果
        try
        {
            for (int i = 0; i < 19; i++) // 防止无限循环，最多99次 
            {
                var json = await Query(part, formatedParams);
                LogRun(caller, formatedParams, json);

                try
                {
                    if (json is null) return new(ReturnCode.EmptyResponse, null);
                    var ret = JsonSerializer.Deserialize<RetJson>(json);

                    int.TryParse(ret!.Code, out var code);

                    // 有错误
                    if (code != 0)
                    {
                        Log(caller, json, ret.Msg);
                        return new(TransferReturnCode(code, ret.Msg), null);
                    }

                    // 解析实际数据
                    var data = JsonSerializer.Deserialize<RetJson<JsonElement>>(json);
                    //if(data.Data.RowCount != data.Data.Data.Length)
                    //    return new(ReturnCode., null);

                    // 记录返回的类型，用于debug
                    //CacheJson(caller, data!.Data.Data);

                    total += data!.Data.Data.Count;
                    list.AddRange(data.Data.Data.Select(x =>
                    {
                        try { return x.Deserialize<TJSON>()!; }
                        catch (Exception ex)
                        {
                            // 记录具体哪个元素反序列化失败
                            JsonBase.ReportJsonUnexpected(Identifier, caller!, $"Failed to deserialize item. Error: {ex.Message}: {x}");
                            throw;
                        }
                    }));


                    // 数据获取全
                    if (data!.Data.TotalCount >= total)
                        break;

                    // 下一页
                    var page = (int)formatedParams["reqPageno"];
                    formatedParams["reqPageno"] = page + 1;
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
        return new(ReturnCode.Success, list.Select(x => transfer(x)).ToArray());
    }


    protected override Dictionary<string, object> GenerateParams(object? obj)
    {
        var dic = base.GenerateParams(obj);

        if (!dic.Any(x => x.Key == "reqNum"))
            dic.Add("reqNum", SizePerPage);
        if (!dic.Any(x => x.Key == "regPageno"))
            dic.Add("regPageno", 1);

        return dic;
    }



    /// <summary>
    /// 主入口
    /// </summary>
    /// <param name="part"></param>
    /// <param name="param"></param>
    /// <returns>
    /// 响应码	响应信息
    ///10000	超时了
    ///10002	该服务不可用（说明服务没有启动）
    ///10003	签名不合法（sign签名有问题）
    ///10004	签名过期
    ///10005	无权访问
    ///10007	缺少参数apikey
    ///10019	参数body加密错误
    ///ERP响应码  响应信息
    ///0000	成功
    ///0001	业务处理失败
    ///1001	系统内部错误
    ///1002	第三方流水号为空
    ///1003	参数为空
    ///1005	参数不合法
    ///1006	业务校验不通过
    /// </returns>
    public async Task<string?> Query(string part, object? param)
    {
        // 创建request
        var request = Build(part, param);
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        //{"errcode":"10005","errmsg":"无权访问该服务:无api访问权限"}
        if (content.Contains("errcode"))
        {
            // 对齐到 RetJson
            content = content.Replace("errcode", "retCode");
            content = content.Replace("errmsg", "retMsg");
            return content;
        }

        // 解密
        var dec = Decoder!.Handle(Convert.FromBase64String(content));
        var json = Encoding.UTF8.GetString(dec!);
        return json;
    }

    /// <summary>
    /// 生成request
    /// </summary>
    /// <param name="part"></param>
    /// <param name="param"></param>
    /// <returns></returns>
    private HttpRequestMessage Build(string part, object? param)
    {
        var url = GetUrl(part)!;

        // json
        var paramsStr = param is null ? "" : JsonSerializer.Serialize(param);

        // 加密
        byte[]? encrypted = Encoder!.Handle(Encoding.UTF8.GetBytes(paramsStr));

        var body = encrypted is null ? "" : Convert.ToBase64String(encrypted);

        // time stamp
        long ts = (long)(DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;

        // url params
        var param2 = $"apikey={APIKey}gwenctype=3ts={ts}";

        var sign = CalcMD5(UrlEncoder.Default.Encode($"POST/rest{part}{param2}{APISecret}"));

        // real url
        var requrl = $"{url}?ts={ts}&apikey={APIKey}&sign={sign}&gwenctype=3";

        HttpRequestMessage request = new HttpRequestMessage();
        request.RequestUri = new Uri(requrl);
        request.Method = HttpMethod.Post;

        // 生成 msgId
        request.Headers.Add("msgId", Guid.NewGuid().ToString());

        // 设置 Content-Type 为 application/json 
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");


        return request;
    }


    private ReturnCode TransferReturnCode(int code, string message)
    {
        switch (code)
        {
            case 0000:
                return ReturnCode.Success;
            case 0001:
                return ReturnCode.CSC_BusinessFailed;
            case 1001:
                return ReturnCode.CSC_InternalServerError;
            case 1002:
                return ReturnCode.CSC_ThirdPartySerialNumberEmpty;
            case 1003:
                return ReturnCode.CSC_ParameterEmpty;
            case 1005:
                return ReturnCode.ParameterInvalid;
            case 1006:
                return ReturnCode.CSC_BusinessValidationFailed;
            case 10000:
                return ReturnCode.TimeOut;
            case 10002:
                return ReturnCode.InterfaceUnavailable;
            case 10003:
                return ReturnCode.CSC_IlligalSign;
            case 10004:
                return ReturnCode.CSC_SignExpired;
            case 10005:
                return ReturnCode.AccessDenied;
            case 10007:
                return ReturnCode.CSC_APIKEY;
            case 10019:
                return ReturnCode.CSC_BodyEncrypt;
            default:
                return ReturnCode.Unknown;
        }
    }




    /// <summary>
    /// 获取字符串的 MD5 值 和 常规的不一样
    /// sb.Append((b & 0xFF).ToString("x2"));
    /// </summary>
    private static string CalcMD5(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            // 将字节数组转换为十六进制字符串
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
                sb.Append((b & 0xFF).ToString("x2"));

            return sb.ToString();
        }
    }
    #endregion

    public class APIConfig : IAPIConfig
    {
        public string Id { get; } = "trustee_csc";

        public string? APIKey { get; set; }

        public string? APISecret { get; set; }

        public string? EncryptKey { get; set; }

    }


}
