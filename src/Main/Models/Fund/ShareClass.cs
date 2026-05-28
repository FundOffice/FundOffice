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
    /// 对应托管外包机构的份额代码 6位
    /// </summary>
    public string? Code { get; set; }

    public int Inherit { get; set; } = ShareClass.Singleton;

    /// <summary>
    /// 要求
    /// </summary>
    public string? Requirement { get; set; }


    public const int Singleton = -1;

    public const string SingletonName = "单一份额";

    public static ShareClass DefaultShare { get; } = new ShareClass { Id = -1, Name = FundElements.SingleShareKey };

    public static ShareClass[] Default { get; } = [DefaultShare];


    /// <summary>
    /// 仅用于serialize
    /// </summary>
    public ShareClass() { }

    public static int GetFlow(int id) => id / 1000;
     
}

