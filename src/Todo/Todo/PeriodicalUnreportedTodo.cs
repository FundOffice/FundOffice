namespace FMO.Todo;


/// <summary>
/// 定期报告未报送Todo
/// 
/// </summary>
public class PeriodicalUnreportedTodo : Todo
{
    public override string? UniqueId => nameof(PeriodicalUnreportedTodo);

    public string[]? Monthly { get; set; }

    public string[]? Quarterly { get; set; }

    public string[]? SemiAnnually { get; set; }

    public string[]? Annually { get; set; }
}