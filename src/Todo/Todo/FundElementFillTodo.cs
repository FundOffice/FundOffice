using System.Reflection;

namespace FMO.Todo;

/// <summary>
/// 基金要素补全待办事项
/// </summary>
public class FundElementFillTodo : Todo
{
    public int FundId { get; init; }

    public override string? UniqueId =>  $"{nameof(FundElementFillTodo)}_{FundId}";

    public override bool JustNotify => true;

    public required string FundName { get; init; }

    public required string FundCode { get; init; }

    public List<string>? Missing { get; set; }
}


/// <summary>
/// 
/// </summary>
public class FundElementMissingTodo : Todo
{
    public int FundId { get; init; }

    public override string? UniqueId => $"{nameof(FundElementFillTodo)}_{FundId}_{Missing}";

    public override bool JustNotify => true;

    public required string FundName { get; init; }

    public required string FundCode { get; init; }

    public required string Missing { get; set; }
}