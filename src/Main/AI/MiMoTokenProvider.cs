using FMO.Models;

namespace FMO.AI;

public class MiMoTokenProvider : TokenProvider
{
    public override string Company => "XiaoMi";
    public override TokenProviderStyle Style => TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://api.xiaomi.com/v1/chat/completions";
}

public partial class MiMoTokenProviderViewModel : TokenProviderViewModel, IViewModel<MiMoTokenProvider, MiMoTokenProviderViewModel>
{
    public static string[] Models { get; } = ["mimo-v2.5-pro", "mimo-v2.5"];
}
