using FMO.Models;

namespace FMO.AI;

public class DeepSeekTokenProvider : TokenProvider
{
    public override string Company => "DeepSeek";
    public override TokenProviderStyle Style { get; set; } = TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://api.deepseek.com/chat/completions";
}

public partial class DeepSeekTokenProviderViewModel : TokenProviderViewModel, IViewModel<DeepSeekTokenProvider, DeepSeekTokenProviderViewModel>
{
    public override TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.OpenAI, TokenProviderStyle.Anthropic];
    public override string ModelsUrl => "https://api.deepseek.com/models";
    public static string[] Models { get; } = ["deepseek-chat", "deepseek-reasoner"];
}
