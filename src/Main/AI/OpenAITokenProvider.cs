using FMO.Models;

namespace FMO.AI;

public class OpenAITokenProvider : TokenProvider
{
    public override string Company => "OpenAI";
    public override TokenProviderStyle Style { get; set; } = TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://api.openai.com/v1/chat/completions";
}

public partial class OpenAITokenProviderViewModel : TokenProviderViewModel, IViewModel<OpenAITokenProvider, OpenAITokenProviderViewModel>
{
    public override TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.OpenAI];
    public override string ModelsUrl => BuildApiUrl(Url, 2, "/models");
    public static string[] Models { get; } = ["gpt-4o", "gpt-4o-mini", "o3", "o4-mini"];
}
