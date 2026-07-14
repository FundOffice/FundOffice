namespace FMO.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class AbilityUnitAttribute : Attribute
{
    public AbilityUnitAttribute(string section, string title, string description = "", bool enable = true)
    {
        Section = section;
        Title = title;
        Description = description;
        Enable = enable;
    }

    public string Section { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }
     

    public bool Enable { get; set; }
}


[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class DataMonitorUnitAttribute : Attribute
{
    public DataMonitorUnitAttribute(string title, string description = "", bool enable = true)
    {
        Title = title;
        Description = description;
        Enable = enable;
    }

    public string Title { get; set; }

    public string Description { get; set; }


    public bool Enable { get; set; }
}
