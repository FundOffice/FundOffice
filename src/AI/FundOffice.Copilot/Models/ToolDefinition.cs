using System.Text.Json;

namespace FundOffice.Copilot.Models;

/// <summary>
/// 工具/函数定义，告诉模型有哪些工具可用。
///
/// 对应 OpenAI 的 ChatTool（type:"function"）
/// 对应 Anthropic 的 Tool（input_schema）
///
/// 设计决策：ParametersSchema 使用 JsonElement 而非强类型对象，
/// 因为两个 SDK 最终都需要 JSON Schema 格式。JsonElement 可以：
///   - 直接 WriteTo(Utf8JsonWriter) 零拷贝写入请求体
///   - 从 JSON 字符串或 Utf8JsonReader 反序列化
///   - 调用方可以用 JsonDocument.Parse() 构造，也可以用 Utf8JsonWriter 构建
///
/// 使用示例：
/// <code>
/// var schema = JsonDocument.Parse("""
/// {
///     "type": "object",
///     "properties": {
///         "location": { "type": "string", "description": "城市名" }
///     },
///     "required": ["location"]
/// }
/// """);
///
/// var tool = new ToolDefinition
/// {
///     Name = "get_weather",
///     Description = "查询天气",
///     ParametersSchema = schema.RootElement.Clone()
/// };
/// </code>
/// </summary>
public sealed record ToolDefinition
{
    /// <summary>函数名称，模型会用此名称请求调用</summary>
    public required string Name { get; init; }

    /// <summary>函数功能描述，帮助模型决定何时调用此工具</summary>
    public required string Description { get; init; }

    /// <summary>函数参数的 JSON Schema，定义参数类型、描述和必填项</summary>
    public required JsonElement ParametersSchema { get; init; }
}
