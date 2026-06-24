using FMO.Models;

namespace FMO.AI;

public partial class TokenProviderViewModel : IViewModel<TokenProvider, TokenProviderViewModel>
{
    public static TokenProviderStyle[] Styles { get; } = 
        [TokenProviderStyle.OpenAI, TokenProviderStyle.Anthropic, TokenProviderStyle.Google];
}
