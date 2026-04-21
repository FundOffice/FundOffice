using FMO.Models;

namespace FMO.Trustee;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class RecordJson:JsonBase
{

    /// <summary>
    /// 交易确认单号
    /// </summary>
    public string cserialno { get; set; }

    /// <summary>
    /// 基金账号
    /// </summary>
    public string fundacco { get; set; }

    /// <summary>
    /// 客户名称
    /// </summary>
    public string custname { get; set; }

    /// <summary>
    /// 销售商名称
    /// </summary>
    public string agencyname { get; set; }

    /// <summary>
    /// 销售商代码
    /// </summary>
    public string agencyno { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    public string businname { get; set; }

    /// <summary>
    /// 申请日期
    /// </summary>
    public string date { get; set; }

    /// <summary>
    /// 确认日期
    /// </summary>
    public string cdate { get; set; }

    /// <summary>
    /// 产品编码
    /// </summary>
    public string fundcode { get; set; }

    /// <summary>
    /// 产品名称
    /// </summary>
    public string fundname { get; set; }

    /// <summary>
    /// 申请金额
    /// </summary>
    public string balance { get; set; }

    /// <summary>
    /// 申请份额
    /// </summary>
    public string shares { get; set; }

    /// <summary>
    /// 确认金额
    /// </summary>
    public string confirmbalance { get; set; }

    /// <summary>
    /// 确认份额
    /// </summary>
    public string confirmshares { get; set; }

    /// <summary>
    /// 交易费用
    /// </summary>
    public string tradefare { get; set; }

    /// <summary>
    /// 归管理人费
    /// </summary>
    public string registfare { get; set; }

    /// <summary>
    /// 归基金资产
    /// </summary>
    public string fundfare { get; set; }

    /// <summary>
    /// 业绩报酬
    /// </summary>
    public string factdeductbalance { get; set; }

    /// <summary>
    /// 单位净值
    /// </summary>
    public string netvalue { get; set; }

    /// <summary>
    /// 确认状态
    /// </summary>
    public string statusname { get; set; }

    /// <summary>
    /// 证件类型
    /// </summary>
    public string identitytype { get; set; }

    /// <summary>
    /// 证件号码
    /// </summary>
    public string identityno { get; set; }

    /// <summary>
    /// 交易账号
    /// </summary>
    public string tradeacco { get; set; }

    /// <summary>
    /// 客户类型
    /// </summary>
    public string custtypename { get; set; }

    /// <summary>
    /// 总费用
    /// </summary>
    public string totalfare { get; set; }

    /// <summary>
    /// 归销售机构费用
    /// </summary>
    public string agencyfare { get; set; }

    /// <summary>
    /// 分红方式
    /// </summary>
    public string bonustype { get; set; }

    /// <summary>
    /// 银行名称
    /// </summary>
    public string bankno { get; set; }

    /// <summary>
    /// 银行账号
    /// </summary>
    public string bankacco { get; set; }

    /// <summary>
    /// 银行户名
    /// </summary>
    public string nameinbank { get; set; }

    public TransferRecord ToObject()
    {
        var r = new TransferRecord
        {
            FundCode = fundcode,
            ConfirmedDate = DateOnly.ParseExact(cdate, "yyyy-MM-dd"),
            RequestDate = DateOnly.ParseExact(date, "yyyy-MM-dd"),
            FundName = fundname,
            Agency = agencyname,
            InvestorName = custname,
            InvestorIdentity = identityno,
            RequestAmount = ParseDecimal(balance),
            RequestShare = ParseDecimal(shares),
            ConfirmedAmount = ParseDecimal(confirmbalance),
            ConfirmedShare = ParseDecimal(confirmshares),
            CreateDate = DateOnly.FromDateTime(DateTime.Today),
            ExternalId = $"{XYZQ._Identifier}.{cserialno}",
            PerformanceFee = ParseDecimal(factdeductbalance),
            Fee = ParseDecimal(totalfare),
            //ExternalRequestId = $"{CSC._Identifier}.{OriginalNo}",
            Type = Parse(businname),
            Source = "api",
        };
        // 净额
        r.ConfirmedNetAmount = r.ConfirmedAmount - r.Fee;

        if (r.Type == TransferRecordType.UNK)
            JsonBase.ReportSpecialType(new(0, XYZQ._Identifier, nameof(TransferRecord), r.ExternalId, businname));
        return r;
    }
    public static TransferRecordType Parse(string type)
    {
        return type switch
        {
            "认购" => TransferRecordType.InitialOffer,
            "申购" => TransferRecordType.Purchase,
            "赎回" => TransferRecordType.Redemption,
            "转托管" => TransferRecordType.TransferOut,
            "托管转入" => TransferRecordType.TransferIn,
            "托管转出" => TransferRecordType.TransferOut,
            "修改分红方式" => TransferRecordType.BonusType,
            "份额冻结" => TransferRecordType.Frozen,
            "份额解冻" => TransferRecordType.Thawed,
            "非交易过户" => TransferRecordType.TransferIn,
            "基金转换(出)" => TransferRecordType.SwitchOut,
            "非交易过户出" => TransferRecordType.TransferOut,
            "非交易过户入" => TransferRecordType.TransferIn,
            "基金转换入" => TransferRecordType.SwitchIn,
            "撤单" => TransferRecordType.UNK,
            "内部转托管" => TransferRecordType.TransferOut,
            "内部转托管入" => TransferRecordType.MoveIn,
            "内部转托管出" => TransferRecordType.MoveOut,
            "预约赎回" => TransferRecordType.Redemption,
            "定期定额申购" => TransferRecordType.Purchase,
            "基金成立" => TransferRecordType.Subscription,
            "基金终止" => TransferRecordType.RaisingFailed,
            "基金清盘" => TransferRecordType.RaisingFailed,
            "强制赎回" => TransferRecordType.ForceRedemption,
            "发行失败" => TransferRecordType.RaisingFailed,
            "统一" => TransferRecordType.UNK,
            "联名卡开通" => TransferRecordType.UNK,
            "联名卡撤销" => TransferRecordType.UNK,
            "强制调增" => TransferRecordType.Increase,
            "强制调减" => TransferRecordType.Decrease,
            "红利发放" => TransferRecordType.Distribution,
            "定期定额赎回" => TransferRecordType.Redemption,
            "盘后合并入" => TransferRecordType.Increase,
            "盘后分拆入" => TransferRecordType.Increase,
            "盘后分拆出" => TransferRecordType.Decrease,
            "盘后合并出" => TransferRecordType.Decrease,
            "赎回保底强增" => TransferRecordType.Increase,
            "赎回保底强减" => TransferRecordType.Decrease,
            "统一保底保本强增" => TransferRecordType.Increase,
            "统一保底保本强减" => TransferRecordType.Decrease,
            "保本转期换算强增" => TransferRecordType.Increase,
            "保本转期换算强减" => TransferRecordType.Decrease,
            "跨TA基金转换" => TransferRecordType.SwitchIn,
            "跨TA基金转换出" => TransferRecordType.SwitchOut,
            "跨TA基金转换入" => TransferRecordType.SwitchIn,
            "网点变更入" => TransferRecordType.TransferIn,
            "网点变更出" => TransferRecordType.TransferOut,
            "基金拆分强减" => TransferRecordType.Decrease,
            "基金拆分强增" => TransferRecordType.Increase,
            "正收益支付" => TransferRecordType.Distribution,
            "基金分级入" => TransferRecordType.SwitchIn,
            "负收益支付" => TransferRecordType.Distribution,
            "基金分级出" => TransferRecordType.SwitchOut,
            "统一业绩提成强增" => TransferRecordType.Increase,
            "统一业绩提成强减" => TransferRecordType.Decrease,
            "分红业绩提成强增" => TransferRecordType.Increase,
            "分红业绩提成强减" => TransferRecordType.Decrease,
            "定期兑付赎回" => TransferRecordType.Redemption,
            "买入待交收恢复交收" => TransferRecordType.Purchase,
            "解押" => TransferRecordType.TransferIn,
            "快速过户出" => TransferRecordType.TransferOut,
            "快速过户" => TransferRecordType.TransferOut,
            "快速过户入" => TransferRecordType.TransferIn,
            "管理人强增" => TransferRecordType.Increase,
            "管理人强减" => TransferRecordType.Decrease,
            "恢复交收强增" => TransferRecordType.Increase,
            "二级账户收益强增" => TransferRecordType.Increase,
            "一级账户收益强减" => TransferRecordType.Decrease,
            "卖出" => TransferRecordType.Redemption,
            "买入" => TransferRecordType.Purchase,
            "合同生效" => TransferRecordType.Subscription,
            "开放日参与" => TransferRecordType.Purchase,
            "开放日退出" => TransferRecordType.Redemption,
            "违约退出" => TransferRecordType.Redemption,
            "合同终止" => TransferRecordType.RaisingFailed,
            "募集失败" => TransferRecordType.RaisingFailed,
            "转指定出" => TransferRecordType.TransferOut,
            "转指定入" => TransferRecordType.TransferIn,
            "定期转换协议" => TransferRecordType.SwitchIn,
            "质押" => TransferRecordType.TransferIn,

            _ => TransferRecordType.UNK
        };
    }
}

  
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。