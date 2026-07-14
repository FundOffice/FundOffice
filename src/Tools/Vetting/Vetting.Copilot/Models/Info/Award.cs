namespace Vetting.Copilot.Models.Info;

public class Award : IResolve
{
    public int Id { get; set; }
    public string? Time { get; set; }
    public string? Entity { get; set; }
    public string? Name { get; set; }
    public string? Evaluator { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Time) => Time,
        nameof(Entity) => Entity,
        nameof(Name) => Name,
        nameof(Evaluator) => Evaluator,
        _ => null,
    };
}
