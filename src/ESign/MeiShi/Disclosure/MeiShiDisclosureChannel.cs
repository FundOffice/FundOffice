using FMO.Disclosure;
using FMO.Logging;
using FMO.Models;
using FMO.Utilities;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;


namespace FMO.ESigning.MeiShi;

[DisclosureDefine(typeof(MeiShiChannelConfig), typeof(MeiShiChannelConfigViewModel))]
public partial class MeiShiAssit : IDisclosureChannel
{
    public string Code => DisclosureChannelCode.MeiShi;


    public string Name => "易私募";

    public string Description => "在易私募平台发布信批公告";



    public IWorkConfig? Build(DisclosureType disclosureType)
    {
        switch (disclosureType)
        {
            case DisclosureType.Monthly:
            case DisclosureType.Quarterly:
            case DisclosureType.SemiAnnually:
            case DisclosureType.Annually:
                return new MeiShiWorkConfig();
            default:
                return null;
        }
    }

    ErrorReturn IDisclosureChannel.VerifyNotice(IDisclosureNotice Notice)
    {
        switch (Notice)
        {
            case PeriodicalDisclosureNotice n:
                return (n.Pdf?.Exists ?? false) ? new(true) : new(false, "文件不存在");

            case TemporaryOpenNotice n:
                return n.OpenDay.Year > 1970 && (n.AllowPurchase || n.AllowRedemption) ? new(true) : new(false, "");

            case HugeRedemptionNotice n:
                return n.OpenDay.Year > 1970 && n.RealRatio > 0 && n.DefinedRatio > 0 ? new(true) : new(false, "赎回比例必须大于0");

            case FundSetupNotice n:
                return n.SetupDay.Year > 1970 ? new(true) : new(false, "成立日期不合法");

            case FundSacleWarningNotice n:
                if (n.WarningType == ScaleWarningType.None) return new(false, "预警类型不合法");
                return n.TouchDate.Year > 1970 ? new(true) : new(false, "触发日期不合法");

            case ITemporaryDisclosureNotice n:
                return (n.Pdf?.Exists ?? false) ? new(true) : new(false, "文件不存在");

            default:
                return new(false, "不支持的通知类型");
        }
    }

    public async Task<ErrorReturn> Disclosure(IDisclosureNotice Notice, IWorkConfig? config)
    {
        switch (Notice)
        {
            case PeriodicalDisclosureNotice n:
                if (n.Pdf?.Exists != true) return new(false, "文件不存在");
                return await UploadDisclosureFile(n.FundName, n.FundCode, "", n.PublishDate.ToDateTime(n.PublishTime), n.Name, n.Pdf.File!);

            ///
            case TemporaryOpenNotice n:
                return await Disclosure(n, config as MeiShiWorkConfig);

            case HugeRedemptionNotice n:
                return await Disclosure(n, config as MeiShiWorkConfig);

            case FundSetupNotice n:
                return await Disclosure(n, config as MeiShiWorkConfig);

            case FundSacleWarningNotice n:
                return await Disclosure(n, config as MeiShiWorkConfig);

            case ITemporaryDisclosureNotice n and IFundDisclosureNotice f:
                if (n.Pdf?.Exists != true) return new(false, "文件不存在");
                return await UploadDisclosureFile(f.FundName, f.FundCode, "", f.PublishDate.ToDateTime(f.PublishTime), f.Name, n.Pdf.File!);

            default:
                return new(false, "不支持的通知类型");
        }
    }

    public bool IsSupported(DisclosureType type) => type != DisclosureType.QuarterlyUpdate;

    public IWorkConfig? DefaultWorkConfig(DisclosureType type)
    {
        return new MeiShiWorkConfig
        {
            Notify = true,
            Seal = true
        };
    }

    public bool RequireConfigWork(DisclosureType type)
    {
        return type switch
        {
            DisclosureType.TemporaryOpen => true,
            DisclosureType.HugeRedemption => true,
            DisclosureType.FundSetup => true,
            DisclosureType.OtherFundNotice => true,
            DisclosureType.ManagerLevel => true,
            DisclosureType.MangerChange => true,
            DisclosureType.OfficeAddressChange => true,
            DisclosureType.OtherManagerNotice => true,
            _ => false
        };
    }


    internal async Task<ErrorReturn> Disclosure(TemporaryOpenNotice notice, MeiShiWorkConfig? config)
    {
        var funds = await QueryFundInfo();
        var fund = funds.FirstOrDefault(x => x.Name == notice.FundName && x.Code == notice.FundCode);
        if (!long.TryParse(fund?.Id, out long fundId))
            return new ErrorReturn(false, "基金未找到");

        var typelist = new List<int>();
        if (notice.AllowPurchase) typelist.Add(1);
        if (notice.AllowRedemption) typelist.Add(2);

        var json = new TemporaryOpenJson
        {
            ProductId = fundId,
            IsAdministratorSeal = config?.Seal == true ? 1 : 0,
            NoticeTemplateType = 1,
            PublishTime = notice.PublishDate,
            NoticeStatus = config?.Notify == true ? 1 : 0,
            NotificationWay = config?.Notify == true ? [1, 2] : null,
            TradeTypeList = typelist,
            EstablishedTime = fund.SetupDate,
            FileAuthority = "[3]",
            DocumentType = 3,
            ProductName = notice.FundName,
            OpenDayHours = $"{notice.OpenDay:yyyy年MM月dd日}"
        };
        return await PublishPredefinedNotice(json);
    }


    internal async Task<ErrorReturn> Disclosure(HugeRedemptionNotice notice, MeiShiWorkConfig? config)
    {
        var funds = await QueryFundInfo();
        var fund = funds.FirstOrDefault(x => x.Name == notice.FundName && x.Code == notice.FundCode);
        if (!long.TryParse(fund?.Id, out long fundId))
            return new ErrorReturn(false, "基金未找到");


        var json = new HugeRedemptionJson
        {
            AlwaysShare = notice.RealRatio,
            DisclosureConditions = 0,
            ProductId = fundId,
            IsAdministratorSeal = config?.Seal == true ? 1 : 0,
            NoticeTemplateType = 2,
            PublishTime = notice.PublishDate,
            NoticeStatus = config?.Notify == true ? 1 : 0,
            NotificationWay = config?.Notify == true ? [1, 2] : null,
            FileAuthority = "[3]",
            DocumentType = 3,
            HugeRedeemRatio = notice.DefinedRatio,
            ShareHandlingMethod = notice.IsFullyPaied ? "全部赎回" : "部分赎回",
            FundFilingCode = notice.FundCode,
            ProductName = notice.FundName,
            OpenDayHours = $"{notice.OpenDay:yyyy年MM月dd日}"
        };
        return await PublishPredefinedNotice(json);
    }


    internal async Task<ErrorReturn> Disclosure(FundSetupNotice notice, MeiShiWorkConfig? config)
    {
        var funds = await QueryFundInfo();
        var fund = funds.FirstOrDefault(x => x.Name == notice.FundName && x.Code == notice.FundCode);
        if (!long.TryParse(fund?.Id, out long fundId))
            return new ErrorReturn(false, "基金未找到");


        var json = new FundSetupJson
        {
            ProductId = fundId,
            IsAdministratorSeal = config?.Seal == true ? 1 : 0,
            NoticeTemplateType = 3,
            PublishTime = notice.PublishDate,
            NoticeStatus = config?.Notify == true ? 1 : 0,
            NotificationWay = config?.Notify == true ? [1, 2] : null,
            EstablishedTime = fund.SetupDate,
            FileAuthority = "[3]",
            DocumentType = 3,
            ProductName = notice.FundName,
            OpenDayHours = ""
        };
        return await PublishPredefinedNotice(json);
    }


    internal async Task<ErrorReturn> Disclosure(FundSacleWarningNotice notice, MeiShiWorkConfig? config)
    {
        var funds = await QueryFundInfo();
        var fund = funds.FirstOrDefault(x => x.Name == notice.FundName && x.Code == notice.FundCode);
        if (!long.TryParse(fund?.Id, out long fundId))
            return new ErrorReturn(false, "基金未找到");


        var json = new FundScaleWarningJson
        {
            ProductId = fundId,
            IsAdministratorSeal = config?.Seal == true ? 1 : 0,
            NoticeTemplateType = notice.WarningType switch
            {
                ScaleWarningType.Continuous60TradeDaysAssetBelow500W => 4,
                ScaleWarningType.DailyAverageAssetBelow500W => 5,
                ScaleWarningType.AnnualAverageNetAssetBelow1000W => 6,
                _ => 0
            },
            PublishTime = notice.PublishDate,
            NoticeStatus = config?.Notify == true ? 1 : 0,
            NotificationWay = config?.Notify == true ? [1, 2] : null,
            FileAuthority = "[3]",
            DocumentType = 3,
            ProductName = notice.FundName,
            OpenDayHours = ""
        };
        return await PublishPredefinedNotice(json);
    }

    private async Task<ErrorReturn> PublishPredefinedNotice(object obj)
    {
        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri("https://vipfunds.simu800.com/vip-manager/noticeTemplate/saveAndPublish");
        request.Headers.Add("tokenid", Token);

        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = client.Send(request);

        var cont = await response.Content.ReadAsStringAsync();

        SigningLoger.LogRun(this, nameof(QueryFundInfo), "", cont);
        if (Regex.IsMatch(cont, "token已失效|重新登录"))
        {
            isLogin = false;
            return new ErrorReturn(false, "token已失效");
        }

        var root = JsonSerializer.Deserialize<RootJson>(cont);
        if (root is null) return new ErrorReturn(false, "Invalid response");

        return root.code == 1008 ? new ErrorReturn(true) : new ErrorReturn(false, root.message);
    }

    #region 上传公告
    public async Task<ErrorReturn> UploadDisclosureFile(string fundName, string fundCode, string shareClass, DateTime time, string announceName, FileMeta meta)
    {
        if (!IsValid) return new ErrorReturn(false, "Invalid");
        if (!isLogin && await LoginFromDisclosure() is ErrorReturn er && !er.Successed)
        {
            LogEx.Error("MeiShi Login Failed");
            return er;
        }

        var file = meta.GetFullPath();

        // 获取对应产品
        var funds = await QueryFundInfo();
        var fund = funds.FirstOrDefault(x => x.Name == fundName && x.Code == fundCode);
        if (fund?.Id is null)
            return new ErrorReturn(false, "Fund not found");

        // 上传文件
        var fileJson = await UploadFile(meta.Name, file, 131);

        if (!fileJson.Contains("1008"))
            return new ErrorReturn(false, "File upload failed");

        // 创建公告
        return await CreateDisclosure(fund.Id, fileJson, meta.Name, file, time, announceName);

    }

    private async Task<ErrorReturn> CreateDisclosure(string fundId, string fileJson, string fileName, string filePath, DateTime time, string announceName)
    {
        var root = JsonSerializer.Deserialize<RootJson>(fileJson);
        if (root is null) return new ErrorReturn(false, "Invalid file JSON");

        var fileData = root.data.Deserialize<FileUploadDataDto>();
        if (fileData is null) return new ErrorReturn(false, "Invalid file data");

        string uid = $"rc-upload-{DateTimeHelper.TimeStampByMilliseconds(DateTime.Now)}-1";

        var fo = new UploadFileInfo
        {
            Uid = uid,
            Name = fileName,
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
            return new ErrorReturn(false, "token已失效");
        }

        root = JsonSerializer.Deserialize<RootJson>(cont);
        if (root is null) return new ErrorReturn(false, "Invalid response");

        return root.code == 1008 ? new ErrorReturn(true) : new ErrorReturn(false, root.message);
    }


    #endregion
}

internal class MeiShiWorkConfig : IWorkConfig
{
    /// <summary>
    /// 通知
    /// </summary>
    public bool Notify { get; set; }

    /// <summary>
    /// 用印
    /// </summary>
    public bool Seal { get; set; }

}
