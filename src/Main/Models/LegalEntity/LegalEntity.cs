namespace FMO.Models;

/// <summary>
/// 法律主体
/// </summary>
public abstract class LegalEntity
{
    /// <summary>
    /// 身份证、执照、备案号
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; set; }


    /// <summary>
    /// 类型
    /// </summary>
    public abstract EntityType Type { get; }



}

