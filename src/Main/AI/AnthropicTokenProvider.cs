using FMO.Models;

namespace FMO.AI;

public class AnthropicTokenProvider : TokenProvider
{
    public override string Company => "Anthropic";
    public override TokenProviderStyle Style { get; set; } = TokenProviderStyle.Anthropic;
    public override string Url { get; set; } = "https://api.anthropic.com/v1/messages";
}

public partial class AnthropicTokenProviderViewModel : TokenProviderViewModel, IViewModel<AnthropicTokenProvider, AnthropicTokenProviderViewModel>
{
    public override TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.Anthropic];
    public override string ModelsUrl => BuildApiUrl(Url, 1, "/models");
    public static string[] Models { get; } = ["claude-sonnet-4-20250514", "claude-haiku-4-20250414"];
}
