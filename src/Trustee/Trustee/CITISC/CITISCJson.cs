using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCITICS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class RootJson
{
    [JsonPropertyName("data")]
    public JsonNode? Data { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; } = -1;

    [JsonPropertyName("message")]
    public string? Msg { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

}



internal class ReturnJsonRoot<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Msg { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

}



public class QueryRoot<T>
{
    /// <summary>
    /// 当前页
    /// </summary>
    [JsonPropertyName("pageNum")]
    public int PageNum { get; set; }

    /// <summary>
    /// 当前页的数量
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// 结果
    /// </summary>
    [JsonPropertyName("list")]
    public List<T>? List { get; set; }

    /// <summary>
    /// 下一页
    /// </summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }


    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    [JsonPropertyName("navigatePages")]
    public int NavPages { get; set; }


    public int PageCount => Pages == 0 ? NavPages : Pages;

}




public class QueryRoot 
{
    /// <summary>
    /// 当前页
    /// </summary>
    [JsonPropertyName("pageNum")]
    public int PageNum { get; set; }

    /// <summary>
    /// 当前页的数量
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// 结果
    /// </summary>
    [JsonPropertyName("list")]
    public List<JsonElement>? List { get; set; }

    /// <summary>
    /// 下一页
    /// </summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }


    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    [JsonPropertyName("navigatePages")]
    public int NavPages { get; set; }


    public int PageCount => Pages == 0 ? NavPages : Pages;

}

internal class TokenJson
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}


public class InvestorAccountMapping
{
    //FundAccount
    public string Id { get; set; }

    public string Name { get; set; }

    public string Indentity { get; set; }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。