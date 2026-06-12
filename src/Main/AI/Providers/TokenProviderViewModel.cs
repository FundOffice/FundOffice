using FMO.Models;

namespace FMO.AI;

public partial class TokenProviderViewModel : IViewModel<TokenProvider, TokenProviderViewModel>
{
    public static TokenProviderStyle[] Styles { get; } = 
        [TokenProviderStyle.OpenAI, TokenProviderStyle.Anthropic, TokenProviderStyle.Google];

    /// <summary>
    /// 该厂商支持的 API 风格，子类 override
    /// </summary>
    public virtual TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.OpenAI];

    /// <summary>
    /// 选中的模型名称
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// 获取模型列表的 URL，子类必须 override
    /// </summary>
    public virtual string ModelsUrl => throw new NotImplementedException($"{GetType().Name} 未重写 ModelsUrl");

    /// <summary>
    /// 获取用量信息的 URL，默认从 chat URL 推导。子类可 override
    /// </summary>
    public virtual string UsageUrl => BuildApiUrl(Url, 2, "/dashboard/billing/usage");

    /// <summary>
    /// 从 chat completions/messages URL 构建 API URL
    /// 去掉最后 stripCount 段路径，拼接指定后缀
    /// </summary>
    protected static string BuildApiUrl(string url, int stripCount, string suffix)
    {
        if (string.IsNullOrEmpty(url)) return suffix.TrimStart('/');
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var keepCount = Math.Max(0, segments.Length - stripCount);
        var basePath = keepCount > 0 ? "/" + string.Join("/", segments.Take(keepCount)) : "";
        return $"{uri.Scheme}://{uri.Authority}{basePath}{suffix}";
    }
}
