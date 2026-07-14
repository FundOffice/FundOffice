namespace Vetting.Copilot.Models.Info;

/// <summary>
/// 按属性名取值（替代反射）
/// </summary>
public interface IResolve
{
    object? Resolve(string propertyName);
}

/// <summary>
/// IResolve 辅助：格式化值为字符串
/// </summary>
public static class ResolveHelper
{
    public static string ToString(object? value, string? format = null)
    {
        if (value == null) return "";
        if (format != null)
        {
            try { return string.Format($"{{0:{format}}}", value); }
            catch { /* fallback */ }
        }
        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            Enum e => e.ToString(),
            _ => value.ToString() ?? "",
        };
    }
}
