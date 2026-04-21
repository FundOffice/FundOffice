using System.ComponentModel;

namespace FMO.Models;

[Flags]
[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum PersonRole
{
    // 无角色
    None = 0x0,
    // 法定代表人
    [Description("法定代表人")] Legal = 0x1,
    // 实际控制人
    [Description("实际控制人")] ActualController = 0x2,
    // 投资经理
    [Description("投资经理")] InvestmentManager = 0x4,
    // 代理人
    [Description("开户代理人")] Agent = 0x8,
    // 下单人
    [Description("指定下单人")] OrderPlacer = 0x10,
    // 资金划转人
    [Description("资金划转人")] FundTransferor = 0x20,
    // 确认人
    [Description("结算单确认人")] ConfirmationPerson = 0x40
}



/// <summary>
/// 人
/// </summary>
public class Person : IEntity
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public Identity? Identity { get; set; }

    public DateEfficient Efficient { get; set; }

    public string? Phone { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 简介
    /// </summary>
    public string? Profile { get; set; }
}
