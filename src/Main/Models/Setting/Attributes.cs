namespace FMO.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class VerifySettingUnitAttribute : Attribute
{
    public VerifySettingUnitAttribute(string title, string description = "", bool enable = true)
    {
        Title = title;
        Description = description;
        Enable = enable;
    }

    public string Title { get; set; }

    public string Description { get; set; }
     

    public bool Enable { get; set; }
}
