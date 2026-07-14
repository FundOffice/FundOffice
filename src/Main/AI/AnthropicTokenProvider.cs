using FMO.Models;

namespace FMO.AI;

public class AnthropicTokenProvider : TokenProvider
{
    public override string Company => "Anthropic";
    public override TokenProviderStyle Style => TokenProviderStyle.Anthropic;
    public override string Url { get; set; } = "https://api.anthropic.com/v1/messages";
}

public partial class AnthropicTokenProviderViewModel : TokenProviderViewModel, IViewModel<AnthropicTokenProvider, AnthropicTokenProviderViewModel>
{
    public static string[] Models { get; } = ["claude-sonnet-4-20250514", "claude-haiku-4-20250414"];
}
