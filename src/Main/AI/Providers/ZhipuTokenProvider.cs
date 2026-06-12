using FMO.Models;

namespace FMO.AI;

public class ZhipuTokenProvider : TokenProvider
{
    public override string Company => "Zhipu";
    public override TokenProviderStyle Style { get; set; } = TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
}

public partial class ZhipuTokenProviderViewModel : TokenProviderViewModel, IViewModel<ZhipuTokenProvider, ZhipuTokenProviderViewModel>
{
    public override TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.OpenAI];
    public override string ModelsUrl => BuildApiUrl(Url, 2, "/models");
    public static string[] Models { get; } = ["glm-4-plus", "glm-4-flash", "glm-4-long"];
}
