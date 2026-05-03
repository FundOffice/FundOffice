using System.Diagnostics.CodeAnalysis;

namespace FMO.Schedule;

public class MissionInfoAttribute : Attribute
{
    [SetsRequiredMembers]
    public MissionInfoAttribute(string name, string description = "")
    {
        Title = name;
        Description = description;
    }

    public required string Title { get; set; }

    public string Description { get; }
}

