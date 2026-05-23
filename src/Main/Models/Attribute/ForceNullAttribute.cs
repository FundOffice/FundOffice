namespace FMO.Models;

public class ForceNullAttribute : Attribute
{
    public string Name { get; set; }

    public ForceNullAttribute(string name)
    {
        Name = name;
    }
}