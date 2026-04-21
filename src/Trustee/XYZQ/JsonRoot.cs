using System.Text.Json.Nodes;

namespace FMO.Trustee;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

internal class JsonRoot
{
     
    public string? msg { get; set; }

    public string? returnCode { get; set; }

    public Pageation resultPage { get; set; }

    public JsonNode resultDataSet { get; set; }
}

internal class Pageation
{
    public int currentPage { get; set; }
    public int pageSize { get; set; }
    public int total { get; set; }
    public int totalPage { get; set; }
}

#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。