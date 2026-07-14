namespace Vetting.Copilot.Models.Info;

public class Department : IResolve
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? MainFunction { get; set; }
    public string? Head { get; set; }
    public string? HasPartTime { get; set; }

    public object? Resolve(string propertyName) => propertyName switch
    {
        nameof(Name) => Name,
        nameof(MainFunction) => MainFunction,
        nameof(Head) => Head,
        _ => null,
    };
}
