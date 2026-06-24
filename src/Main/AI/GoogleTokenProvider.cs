using FMO.Models;

namespace FMO.AI;

public class GoogleTokenProvider : TokenProvider
{
    public override string Company => "Google";
    public override TokenProviderStyle Style => TokenProviderStyle.Google;
    public override string Url { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
}

public partial class GoogleTokenProviderViewModel : TokenProviderViewModel, IViewModel<GoogleTokenProvider, GoogleTokenProviderViewModel>
{
    public static string[] Models { get; } = ["gemini-2.5-pro", "gemini-2.5-flash"];
}
