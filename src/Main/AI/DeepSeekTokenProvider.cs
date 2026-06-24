using FMO.Models;

namespace FMO.AI;

public class DeepSeekTokenProvider : TokenProvider
{
    public override string Company => "DeepSeek";
    public override TokenProviderStyle Style => TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://api.deepseek.com/v1/chat/completions";
}

public partial class DeepSeekTokenProviderViewModel : TokenProviderViewModel, IViewModel<DeepSeekTokenProvider, DeepSeekTokenProviderViewModel>
{
    public static string[] Models { get; } = ["deepseek-chat", "deepseek-reasoner"];
}
