using FundOffice.Copilot.Configuration;
using LiteDB;

namespace Vetting.Copilot.Models;

public record AIProviderConfig(
    [property: BsonId] int Id,
    string Name,
    string ProviderType,
    string ApiKey,
    string BaseUrl,
    string Model,
    OpenAIApiVersion ApiVersion = OpenAIApiVersion.ChatCompletions);
