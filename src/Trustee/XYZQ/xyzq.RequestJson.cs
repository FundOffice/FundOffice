using FMO.Models;
using System.Text.Json.Serialization;

namespace FMO.Trustee;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
internal class RequestJson : JsonBase
{
    public override string? JsonId => $"{ProductCode}.{TradingAccount}{BusinessType}{ApplicationDate}" ;


    /// <summary>
    /// 产品名称
    /// </summary>
    [JsonPropertyName("cpmc")]
    public string ProductName { get; set; }

    /// <summary>
    /// 产品代码
    /// </summary>
    [JsonPropertyName("cpdm")]
    public string ProductCode { get; set; }

    /// <summary>
    /// 客户名称
    /// </summary>
    [JsonPropertyName("khmc")]
    public string CustomerName { get; set; }

    /// <summary>
    /// 客户类型
    /// </summary>
    [JsonPropertyName("khlx")]
    public string CustomerType { get; set; }

    /// <summary>
    /// 证件类型
    /// </summary>
    [JsonPropertyName("zjlx")]
    public string IdCardType { get; set; }

    /// <summary>
    /// 证件号码
    /// </summary>
    [JsonPropertyName("zjhm")]
    public string IdCardNumber { get; set; }

    [JsonPropertyName("xxs")]
    public string Saler { get; set; }

    /// <summary>
    /// 交易账号
    /// </summary>
    [JsonPropertyName("jyzh")]
    public string TradingAccount { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    [JsonPropertyName("ywlb")]
    public string BusinessType { get; set; }

    /// <summary>
    /// 申请份额
    /// </summary>
    [JsonPropertyName("sqfe")]
    public string ApplicationShares { get; set; }

    /// <summary>
    /// 申请金额
    /// </summary>
    [JsonPropertyName("sqje")]
    public string ApplicationAmount { get; set; }

    /// <summary>
    /// 申请日期
    /// </summary>
    [JsonPropertyName("sqrq")]
    public string ApplicationDate { get; set; }

    /// <summary>
    /// 分红方式
    /// </summary>
    [JsonPropertyName("fhfs")]
    public string DividendMethod { get; set; }

    /// <summary>
    /// 申请折扣
    /// </summary>
    [JsonPropertyName("sqzk")]
    public string ApplicationDiscount { get; set; }

    /// <summary>
    /// 预计确认日
    /// </summary>
    [JsonPropertyName("yrq")]
    public string ExpectedConfirmationDate { get; set; }

    /// <summary>
    /// 校验结果
    /// </summary>
    [JsonPropertyName("jyjg")]
    public string ValidationResult { get; set; }

    /// <summary>
    /// 校验提示
    /// </summary>
    [JsonPropertyName("jyjc")]
    public string ValidationMessage { get; set; }


    public TransferRequest ToObject()
    {
        var r = new TransferRequest
        {
            FundName = ProductName,
            FundCode = ProductCode,
            InvestorIdentity = IdCardNumber,
            InvestorName = CustomerName,
            RequestDate = DateOnly.ParseExact(ApplicationDate, "yyyyMMdd"),
            RequestType = Parse(BusinessType),
            RequestAmount = ParseDecimal(ApplicationAmount),
            RequestShare = ParseDecimal(ApplicationShares),
            Agency = Saler,
            FeeDiscount = ParseDecimal(ApplicationDiscount),
            ExternalId = $"{XYZQ._Identifier}.{JsonId}",
            Source = "api"
        };

        if (r.RequestType == TransferRequestType.UNK)
            JsonBase.ReportSpecialType(new(0, XYZQ._Identifier, nameof(TransferRequest), JsonId, BusinessType));
        return r;
    }


    public static TransferRequestType Parse(string type)
    {
        return type switch
        {
            // -----------------------------
            // 常见交易类
            // -----------------------------
            "01" => TransferRequestType.Subscription,         // 认购
            "02" => TransferRequestType.Purchase,             // 申购
            "03" => TransferRequestType.Redemption,           // 赎回
            "05" => TransferRequestType.TransferIn,           // 托管转入
            "06" => TransferRequestType.TransferOut,          // 托管转出
            "07" => TransferRequestType.BonusType,            // 修改分红方式
            "13" => TransferRequestType.SwitchOut,            // 基金转换(出)
            "15" => TransferRequestType.TransferOut,          // 非交易过户出
            "16" => TransferRequestType.SwitchIn,             // 基金转换入
            "14" => TransferRequestType.TransferIn,           // 非交易过户入
            "17" => TransferRequestType.Abort,                // 撤单
            "20" => TransferRequestType.TransferOut,          // 内部转托管
            "21" => TransferRequestType.TransferIn,           // 内部转托管入
            "22" => TransferRequestType.TransferOut,          // 内部转托管出
            "25" => TransferRequestType.Redemption,           // 预约赎回
            "39" => TransferRequestType.Purchase,             // 定期定额申购
            "50" => TransferRequestType.InitialOffer,         // 基金成立 → 视为认购
            "53" => TransferRequestType.ForceRedemption,      // 强制赎回
            "70" => TransferRequestType.Increase,             // 强制调增
            "71" => TransferRequestType.Decrease,             // 强制调减
            "74" => TransferRequestType.Distribution,         // 红利发放（分红）
            "95" => TransferRequestType.Redemption,           // 定期定额赎回

            // -----------------------------
            // 特殊操作 & 扩展
            // -----------------------------
            "A1" => TransferRequestType.Increase,             // 盘后合并入
            "A2" => TransferRequestType.Increase,             // 盘后分拆入
            "A3" => TransferRequestType.Decrease,             // 盘后分拆出
            "A4" => TransferRequestType.Increase,             // 盘后合并出 → 实际是调减？但逻辑上合并出是减少，这里可能需业务确认
            "A5" => TransferRequestType.Increase,             // 赎回保底强增
            "A6" => TransferRequestType.Decrease,             // 赎回保底强减
            "A7" => TransferRequestType.Increase,             // 统一保底保本强增
            "A8" => TransferRequestType.Decrease,             // 统一保底保本强减
            "A9" => TransferRequestType.Increase,             // 保本转期换算强增
            "AA" => TransferRequestType.Decrease,             // 保本转期换算强减
            "B0" => TransferRequestType.SwitchIn,             // 跨TA基金转换（整体）
            "B1" => TransferRequestType.SwitchOut,            // 跨TA基金转换出
            "B2" => TransferRequestType.SwitchIn,             // 跨TA基金转换入
            "C1" => TransferRequestType.TransferIn,           // 网点变更入
            "C2" => TransferRequestType.TransferOut,          // 网点变更出
            "C3" => TransferRequestType.Decrease,             // 基金拆分强减
            "F1" => TransferRequestType.Increase,             // 基金拆分强增
            "F2" => TransferRequestType.Distribution,         // 正收益支付 → 分红
            "F3" => TransferRequestType.SwitchIn,             // 基金分级入
            "F4" => TransferRequestType.Distribution,         // 负收益支付 → 分红
            "F5" => TransferRequestType.SwitchOut,            // 基金分级出
            "F6" => TransferRequestType.Increase,             // 统一业绩提成强增
            "F7" => TransferRequestType.Decrease,             // 统一业绩提成强减
            "F8" => TransferRequestType.Increase,             // 分红业绩提成强增
            "F9" => TransferRequestType.Decrease,             // 分红业绩提成强减
            "G1" => TransferRequestType.Redemption,           // 定期兑付赎回
            "JS" => TransferRequestType.Purchase,             // 买入待交收恢复交收 → 买入
            "JY" => TransferRequestType.TransferIn,           // 解押 → 视为转入
            "KC" => TransferRequestType.TransferOut,          // 快速过户出
            "KH" => TransferRequestType.TransferOut,          // 快速过户（默认视为出）
            "KR" => TransferRequestType.TransferIn,           // 快速过户入
            "M2" => TransferRequestType.Increase,             // 管理人强增
            "M3" => TransferRequestType.Decrease,             // 管理人强减
            "M4" => TransferRequestType.Increase,             // 恢复交收强增
            "M5" => TransferRequestType.Increase,             // 二级账户收益强增
            "M6" => TransferRequestType.Decrease,             // 一级账户收益强减
            "mc" => TransferRequestType.Redemption,           // 卖出
            "mr" => TransferRequestType.Purchase,             // 买入
            "Z2" => TransferRequestType.Subscription,         // 合同生效 → 认购
            "Z3" => TransferRequestType.Purchase,             // 开放日参与 → 申购
            "Z4" => TransferRequestType.Redemption,           // 开放日退出 → 赎回
            "Z5" => TransferRequestType.Redemption,           // 违约退出 → 赎回
            "Z6" => TransferRequestType.Abort,                // 合同终止 → 撤单/取消
            "Z7" => TransferRequestType.Abort,                // 募集失败 → 取消
            "ZC" => TransferRequestType.TransferOut,          // 转指定出
            "ZR" => TransferRequestType.TransferIn,           // 转指定入
            "ZT" => TransferRequestType.SwitchIn,             // 定期转换协议 → 转入
            "ZY" => TransferRequestType.TransferIn,           // 质押 → 视为转入（或可单独处理）

            // -----------------------------
            // 其他未明确映射（可选：返回 UNK 或按需扩展）
            // -----------------------------
            "04" => TransferRequestType.TransferOut,          // 转托管 → 视为转出
            "10" => TransferRequestType.UNK,                  // 份额冻结（无对应）
            "11" => TransferRequestType.UNK,                  // 份额解冻（无对应）
            "12" => TransferRequestType.TransferOut,          // 非交易过户 → 视为转出
            "51" => TransferRequestType.UNK,                  // 基金终止（无直接对应）
            "52" => TransferRequestType.UNK,                  // 基金清盘
            "54" => TransferRequestType.Abort,                // 发行失败 → 取消
            "58" => TransferRequestType.UNK,                  // 统一（含义模糊）
            "67" => TransferRequestType.UNK,                  // 联名卡开通（非交易类）
            "68" => TransferRequestType.UNK,                  // 联名卡撤销
            _ => TransferRequestType.UNK                      // 默认未知
        };
    }
}



#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。