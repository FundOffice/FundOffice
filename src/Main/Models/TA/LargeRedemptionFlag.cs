namespace FMO.Models;

/// <summary>
/// 巨额赎回处理方式
/// </summary>
public enum LargeRedemptionFlag
{
    Unk,

    /// <summary>
    /// 取消剩下的
    /// </summary>
    CancelRemaining,

    /// <summary>
    /// 顺延
    /// </summary>
    RollOver
}

