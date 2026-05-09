namespace FMO.Models;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class HookDataAttribute : Attribute
{

}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class HookableAttribute : Attribute
{
    public HookableAttribute(Type type)
    {
        Type = type;
    }

    public Type Type { get; set; }
}