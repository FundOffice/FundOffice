namespace FMO.Models;

/// <summary>
/// 临时开放
/// </summary>
public class TemporarilyOpenInfo
{
    public bool IsAllowed { get; set; }

    /// <summary>
    /// 仅在合同变更、法规
    /// </summary>
    public bool IsLimited { get; set; }

    public bool AllowPurchase { get; set; }

    public bool AllowRedemption { get; set; }


    public override string ToString() => !IsAllowed ? "不允许临开" : (IsLimited ? "仅合同变更、法规变更时，" : "") + $"允许{(AllowPurchase ? "申购" : "")}{(AllowRedemption ? "赎回" : "")}";
}