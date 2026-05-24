namespace FMO.Models;


[AttributeUsage(AttributeTargets.Class,  AllowMultiple = true)]
public class ForceNullAttribute : Attribute
{
    public string Name { get; set; }

    public ForceNullAttribute(string name)
    {
        Name = name;
    }
}