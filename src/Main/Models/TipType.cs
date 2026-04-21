namespace FMO.Models;

public enum TipType
{
    None,

    /// <summary>
    /// 基金份额与估值表不致
    /// </summary>
    FundShareNotPair,

    /// <summary>
    /// 没有TA数据
    /// </summary>
    FundNoTARecord,
    OverDue,



    /// <summary>
    /// ta 的fundid 或investorid = 0
    /// </summary>
    TANoOwner,


    /// <summary>
    /// 有record 但是没有request
    /// </summary>
    TransferRequestMissing,


}
