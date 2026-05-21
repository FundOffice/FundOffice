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


    public int Inherit { get; set; } = ShareClass.Singleton;

    /// <summary>
    /// 要求
    /// </summary>
    public string? Requirement { get; set; }


    public const int Singleton = -1;

    public static ShareClass DefaultShare { get; } = new ShareClass { Id = -1, Name = FundElements.SingleShareKey };

    public static ShareClass[] Default { get; } = [DefaultShare];


    /// <summary>
    /// 仅用于serialize
    /// </summary>
    public ShareClass() { }



}


public record struct ShareType(string? Requirement, string Id = "singleton", string Name = "单一份额", string Inherit = "singleton")
{
    public const string Singleton = "singleton";

    public static ShareType[] Default { get; } = [new(null)];
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

