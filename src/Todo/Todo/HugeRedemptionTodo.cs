namespace FMO.Todo;

/// <summary>
/// 添加巨额赎回待办事项
/// </summary>
public class HugeRedemptionTodo : Todo
{
    public int FundId { get; init; }

    public override string? UniqueId => $"{nameof(HugeRedemptionTodo)}_{FundId}_{OpenDay}";

    public required string FundName { get; init; }

    public required string FundCode { get; init; }


    public required DateOnly OpenDay { get; set; }
     
    /// <summary>
    /// 赎回比例
    /// </summary>
    public decimal RealRatio { get; set; }

    /// <summary>
    /// 合同约定的赎回比例
    /// </summary>
    public decimal DefinedRatio { get; set; }

    /// <summary>
    /// 是否全部兑付
    /// </summary>
    public bool IsFullyPaied { get; set; }


    /// <summary>
    /// 是否生成了赎回公告
    /// </summary>
    public bool NoticeAdded { get; set; }



}
