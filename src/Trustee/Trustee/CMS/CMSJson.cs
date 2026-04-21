using System.Text.Json.Serialization;

namespace FMO.Trustee.JsonCMS;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class JsonRoot
{
    [JsonPropertyName("resultcode")]
    public string Code { get; set; }


    [JsonPropertyName("msg")]
    public string Msg { get; set; }


    [JsonPropertyName("page")]
    public string? Page { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

public class PaginationInfo
{
    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

internal class JsonRoot<T>
{
    [JsonPropertyName("resultcode")]
    public string Code { get; set; }


    [JsonPropertyName("msg")]
    public string Msg { get; set; }


    [JsonPropertyName("page")]
    public required PaginationInfo Page { get; set; }

    [JsonPropertyName("data")]
    public required T[] Data { get; set; }
}




#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。