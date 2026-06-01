using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;


public enum TokenProviderStyle
{
    None,
    OpenAI,
    Anthropic,
}

/// <summary>
/// ai 提供商
/// </summary>
public class TokenProvider
{
    public required string Id { get; set; }

    public required string Url { get; set; }

    public required string Key { get; set; }

    public required TokenProviderStyle Style { get; set; }

    public override string ToString()
    {
        return Id ?? "未设置来源";
    }
}
