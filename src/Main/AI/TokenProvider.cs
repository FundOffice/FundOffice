using FMO.Models;

namespace FMO.AI;


public enum TokenProviderStyle
{
    None,
    OpenAI,
    Anthropic,
}

/// <summary>
/// ai 提供商
/// </summary>
public  class TokenProvider
{
    public int Id { get; set; }

    public virtual string Company { get; } = "未知提供商";

    public required string Url { get; set; }

    public required string Key { get; set; }

    public required TokenProviderStyle Style { get; set; }



    public override string ToString()
    {
        return Company ?? "未设置来源";
    }
}


//public class AIProvider
//{
//    public required List<TokenProvider> TokenProviders { get; set; } = new List<TokenProvider>();

//    public static string Ask(TokenProvider provider, string content)
//    {

//    }

//}

// OpenAI响应实体（适配文档返回结构）
public class OpenAIResponse
{
    public OpenAIChoice[]? choices { get; set; }
}
public class OpenAIChoice
{
    public OpenAIMessage? message { get; set; }
}
public class OpenAIMessage
{
    public string? content { get; set; }
}

// Anthropic响应实体（适配文档返回结构）
public class AnthropicResponse
{
    public AnthropicContent[]? content { get; set; }
}
public class AnthropicContent
{
    public string? type { get; set; }
    public string? text { get; set; }
}
