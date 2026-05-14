using System.Diagnostics.CodeAnalysis;

namespace FMO.Models;

/// <summary>
/// 有不同份额安排
/// </summary>
public class ShareClass
{
    public int Id { get; set; }

    /// <summary>
    /// 份额名称
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 要求
    /// </summary>
    public string? Requirement { get; set; }


    public static ShareClass DefaultShare => new ShareClass { Id = -1, Name = FundElements.SingleShareKey };

    /// <summary>
    /// 仅用于serialize
    /// </summary>
    public ShareClass() {   }



}


/// <summary>
/// 与份额相关的要素
/// </summary>
public class PortionElements
{
    /// <summary>
    /// 份额
    /// </summary>
    public ShareClass? Class { get; set; }

    /// <summary>
    /// 锁定期
    /// </summary>
    public Mutable<SealingRule>? LockingRule { get; set; }

}

