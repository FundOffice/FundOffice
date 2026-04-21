namespace FMO.Models;

/// <summary>
/// 标记类需要生成从T类型更新的扩展方法
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class UpdateByAttribute(Type type) : Attribute
{
    public Type Type { get; set; } = type;
}

