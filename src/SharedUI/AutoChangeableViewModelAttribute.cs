namespace FMO.Shared;

public class AutoChangeableViewModelAttribute(Type type) : Attribute
{ 
    public Type Type { get; set; } = type;
}
