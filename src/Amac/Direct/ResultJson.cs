

using System.Text.Json.Serialization;

namespace FMO.AMAC.Direct;

// 响应模型
public class ProcessResponse
{
    [JsonPropertyName("processCode")]
    public string? ProcessCode { get; set; }

    [JsonPropertyName("processMessage")]
    public string? ProcessMessage { get; set; }

    [JsonPropertyName("handle")]
    public string? Handle { get; set; }
}


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
public class VerifyMessageChildLeaf
{
    public string deepLevel { get; set; }
    public int radius { get; set; }
    public string level { get; set; }
    public string type { get; set; }
    public string description { get; set; }
}

public class VerifyMessageChildFolder
{
    public string deepLevel { get; set; }
    public int radius { get; set; }
    public string level { get; set; }
    public string type { get; set; }
    public string description { get; set; }
    public List<VerifyMessageChildLeaf> children { get; set; } // 可以是 leaf 或混合
}

public class Total
{
    public int warningCount { get; set; }
    public int infoCount { get; set; }
    public int errorCount { get; set; }
}

public class VerifyMessageRoot
{
    public bool levelGroup { get; set; }
    public bool show { get; set; }
    public string totalMessage { get; set; }
    public Total total { get; set; }
    public string description { get; set; }
    public string type { get; set; }
    public string deepLevel { get; set; }
    public List<VerifyMessageChildFolder> children { get; set; }
}

public class ValidationResultItem
{
    public string processCode { get; set; }
    public string processMessage { get; set; }
    public bool verifyFlag { get; set; }
    public VerifyMessageRoot verifyMessage { get; set; }
    public string handle { get; set; }
}


public class ValidationInfo
{
    public string Level { get; set; }

    public string Message { get; set; }
}


#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。