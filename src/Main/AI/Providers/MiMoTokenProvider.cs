using FMO.Models;

namespace FMO.AI;

public class MiMoTokenProvider : TokenProvider
{
    public override string Company => "XiaoMi";
    
    public override TokenProviderStyle Style { get; set; } = TokenProviderStyle.OpenAI;
    
    public override string Url { get; set; } = "https://api.xiaomimimo.com/v1/chat/completions";

    //protected override bool SupportsDocxBase64Inline => true;
}

public partial class MiMoTokenProviderViewModel : TokenProviderViewModel, IViewModel<MiMoTokenProvider, MiMoTokenProviderViewModel>
{
    public override TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.OpenAI, TokenProviderStyle.Anthropic];
    public override string ModelsUrl => "https://api.xiaomimimo.com/v1/models";
    public static string[] Models { get; } = ["mimo-v2.5-pro", "mimo-v2.5"];
}
