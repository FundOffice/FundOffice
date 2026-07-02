# FundOffice.Copilot

统一的 AI 调用抽象层，支持 OpenAI 和 Anthropic API，纯 `HttpClient` 实现，零外部依赖。

## 目录

- [快速开始](#快速开始)
- [基础对话](#基础对话)
- [流式输出](#流式输出)
- [Tool 调用（Function Calling）](#tool-调用function-calling)
- [配置参数](#配置参数)
- [错误处理](#错误处理)
- [消息模型详解](#消息模型详解)
- [架构说明](#架构说明)

---

## 快速开始

### 创建 Provider

Provider 构造时一次性校验并存储所有配置（ApiKey、Model、BaseUrl 等），之后不再持有 Options 引用。

```csharp
using FundOffice.Copilot.Configuration;
using FundOffice.Copilot.Providers;

// OpenAI（也兼容 OpenRouter 等第三方代理）
var openai = new OpenAITokenProvider(new OpenAIOptions
{
    ApiKey = "sk-xxx",
    Model = "gpt-4o",                          // 必填
    BaseUrl = "https://api.openai.com"         // 默认值
});

// Anthropic（也兼容第三方代理）
var anthropic = new AnthropicTokenProvider(new AnthropicOptions
{
    ApiKey = "sk-ant-xxx",
    Model = "claude-sonnet-4-20250514",        // 必填
    BaseUrl = "https://api.anthropic.com"      // 默认值
});

// 使用自定义 HttpClient（可选，用于代理、超时等）
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var provider = new OpenAITokenProvider(options, httpClient);
```

### 使用兼容端点

```csharp
// 第三方 OpenAI 兼容代理
var provider = new OpenAITokenProvider(new OpenAIOptions
{
    ApiKey = "your-key",
    Model = "deepseek-v4-pro",
    BaseUrl = "https://your-proxy.com"
});

// 第三方 Anthropic 兼容代理
var provider2 = new AnthropicTokenProvider(new AnthropicOptions
{
    ApiKey = "your-key",
    Model = "claude-sonnet-4-20250514",
    BaseUrl = "https://your-proxy.com"
});
```

---

## 基础对话

使用 `ChatCompletionAsync` 进行非流式调用，返回完整的 `ChatResult`：

```csharp
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;

ITokenProvider provider = new OpenAITokenProvider(new OpenAIOptions
{
    ApiKey = "sk-xxx",
    Model = "gpt-4o"
});

var messages = new List<ChatMessage>
{
    ChatMessage.System("你是一个有用的助手。"),
    ChatMessage.User("什么是量子计算？")
};

ChatResult result = await provider.ChatCompletionAsync(messages);

// 读取文本回复
string reply = result.Messages
    .SelectMany(m => m.Content)
    .OfType<TextContent>()
    .FirstOrDefault()?.Text ?? "";

Console.WriteLine(reply);

// 读取 token 用量
Console.WriteLine($"输入 tokens: {result.PromptTokens}");
Console.WriteLine($"输出 tokens: {result.CompletionTokens}");
Console.WriteLine($"结束原因: {result.FinishReason}");
```

### 多轮对话

把历史消息全部传入即可：

```csharp
var messages = new List<ChatMessage>
{
    ChatMessage.System("你是一个有用的助手。"),
    ChatMessage.User("你好！"),
    ChatMessage.Assistant("你好！有什么可以帮你的？"),
    ChatMessage.User("今天天气怎么样？")
};

var result = await provider.ChatCompletionAsync(messages);
```

### 请求级切换模型

```csharp
var options = new ChatOptions { Model = "gpt-4o-mini" };
var result = await provider.ChatCompletionAsync(messages, options: options);
```

---

## 流式输出

使用 `ChatCompletionStreamAsync` 获取 `IAsyncEnumerable<StreamingToken>`：

```csharp
var messages = new List<ChatMessage>
{
    ChatMessage.System("你是一个有用的助手。"),
    ChatMessage.User("写一首关于编程的诗。")
};

await foreach (var token in provider.ChatCompletionStreamAsync(messages))
{
    switch (token)
    {
        case TextDelta text:
            Console.Write(text.Text);  // 逐字输出
            break;

        case ToolCallDelta toolCall:
            Console.WriteLine($"\n[Tool call: {toolCall.FunctionName}]");
            break;

        case UsageUpdate usage:
            Console.WriteLine($"\nTokens - 输入: {usage.PromptTokens}, 输出: {usage.CompletionTokens}");
            break;

        case StreamComplete complete:
            Console.WriteLine($"\n结束: {complete.FinishReason}");
            break;
    }
}
```

### 流式收集完整回复

```csharp
var textBuilder = new StringBuilder();

await foreach (var token in provider.ChatCompletionStreamAsync(messages))
{
    if (token is TextDelta td)
        textBuilder.Append(td.Text);
}

string fullReply = textBuilder.ToString();
```

---

## Tool 调用（Function Calling）

Tool 调用是一个**对话循环**：模型请求调用工具 → 你执行工具 → 把结果喂回模型 → 模型继续回复。

### 完整流程

```csharp
using System.Text.Json;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;

ITokenProvider provider = new OpenAITokenProvider(new OpenAIOptions
{
    ApiKey = "sk-xxx",
    Model = "gpt-4o"
});

// ---- 第 1 步：定义工具 ----

var schemaJson = """
{
    "type": "object",
    "properties": {
        "location": {
            "type": "string",
            "description": "城市名称，如 'Beijing' 或 'New York'"
        }
    },
    "required": ["location"]
}
""";

var schemaDoc = JsonDocument.Parse(schemaJson);

var tools = new List<ToolDefinition>
{
    new()
    {
        Name = "get_weather",
        Description = "查询指定城市的当前天气",
        ParametersSchema = schemaDoc.RootElement.Clone()
    }
};

// ---- 第 2 步：发送初始请求 ----

var messages = new List<ChatMessage>
{
    ChatMessage.System("你是一个有用的助手。遇到需要查询信息的问题时请使用工具。"),
    ChatMessage.User("北京今天天气怎么样？")
};

var result = await provider.ChatCompletionAsync(messages, tools);

// ---- 第 3 步：检查是否有 tool call ----

var assistantMessage = result.Messages[0];
var toolCalls = assistantMessage.Content.OfType<ToolCallContent>().ToList();

if (toolCalls.Count > 0 && result.FinishReason == "tool_calls")
{
    // 先把 assistant 的回复（含 tool call）加入对话历史
    messages.Add(assistantMessage);

    // ---- 第 4 步：执行每个 tool call，返回结果 ----

    foreach (var call in toolCalls)
    {
        Console.WriteLine($"调用工具: {call.FunctionName}({call.ArgumentsJson})");

        var args = JsonDocument.Parse(call.ArgumentsJson);
        var location = args.RootElement.GetProperty("location").GetString();

        string toolResult = await GetWeatherAsync(location);
        messages.Add(ChatMessage.ToolResult(call.Id, toolResult));
    }

    // ---- 第 5 步：再次调用模型，让它基于工具结果回复 ----

    var finalResult = await provider.ChatCompletionAsync(messages, tools);
    string finalReply = finalResult.Messages
        .SelectMany(m => m.Content)
        .OfType<TextContent>()
        .FirstOrDefault()?.Text ?? "";

    Console.WriteLine(finalReply);
}
else
{
    // 模型直接回复，无需调用工具
    string reply = assistantMessage.Content
        .OfType<TextContent>()
        .FirstOrDefault()?.Text ?? "";
    Console.WriteLine(reply);
}

async Task<string> GetWeatherAsync(string location)
{
    await Task.Delay(100);
    return $"{{\"location\": \"{location}\", \"temperature\": \"22°C\", \"condition\": \"晴\"}}";
}
```

### 流式 Tool 调用

流式场景下需要手动累积 `ToolCallDelta`：

```csharp
var textBuilder = new StringBuilder();
var toolCallArgs = new Dictionary<string, StringBuilder>();
var toolCallNames = new Dictionary<string, string>();
string? finishReason = null;

await foreach (var token in provider.ChatCompletionStreamAsync(messages, tools))
{
    switch (token)
    {
        case TextDelta td:
            textBuilder.Append(td.Text);
            break;

        case ToolCallDelta tcd:
            if (!toolCallArgs.ContainsKey(tcd.Id))
            {
                toolCallArgs[tcd.Id] = new StringBuilder();
                toolCallNames[tcd.Id] = tcd.FunctionName ?? "";
            }
            if (tcd.FunctionName is not null)
                toolCallNames[tcd.Id] = tcd.FunctionName;
            toolCallArgs[tcd.Id].Append(tcd.ArgumentsDelta);
            break;

        case StreamComplete sc:
            finishReason = sc.FinishReason;
            break;
    }
}

var assistantParts = new List<ContentPart>();
if (textBuilder.Length > 0)
    assistantParts.Add(new TextContent(textBuilder.ToString()));

foreach (var (id, argsBuilder) in toolCallArgs)
    assistantParts.Add(new ToolCallContent(id, toolCallNames[id], argsBuilder.ToString()));

messages.Add(ChatMessage.Assistant(assistantParts));

if (finishReason == "tool_calls")
{
    foreach (var part in assistantParts.OfType<ToolCallContent>())
    {
        var args = JsonDocument.Parse(part.ArgumentsJson);
        var location = args.RootElement.GetProperty("location").GetString();
        string result2 = await GetWeatherAsync(location);
        messages.Add(ChatMessage.ToolResult(part.Id, result2));
    }

    var finalResult = await provider.ChatCompletionAsync(messages, tools);
}
```

### 多工具并行调用 / 工具执行出错

```csharp
foreach (var call in toolCalls)
{
    try
    {
        var result = await ExecuteToolAsync(call.FunctionName, call.ArgumentsJson);
        messages.Add(ChatMessage.ToolResult(call.Id, result));
    }
    catch (Exception ex)
    {
        // isError = true 告诉模型工具执行失败
        messages.Add(ChatMessage.ToolResult(call.Id, ex.Message, isError: true));
    }
}
```

---

## 配置参数

### Provider 级别（构造时，一次性校验后存为字段）

| 参数 | OpenAI | Anthropic | 说明 |
|------|--------|-----------|------|
| `ApiKey` | 必填 | 必填 | API 密钥 |
| `Model` | 必填 | 必填 | 默认模型，请求级 ChatOptions 可覆盖 |
| `BaseUrl` | 必填 | 必填 | API 地址，兼容第三方代理 |
| `ApiVersion` | - | 默认 `2023-06-01` | Anthropic API 版本 |

### 请求级别（每次调用时，通过 ChatOptions 传入）

```csharp
var options = new ChatOptions
{
    Model = "gpt-4o-mini",      // 覆盖 Provider 默认模型
    Temperature = 0.7f,          // 0.0 ~ 2.0，越高越随机
    MaxTokens = 16384,           // 最大输出 token 数（Anthropic 默认 16384）
    TopP = 0.9f,                 // nucleus sampling
    StopSequences = ["\n\n"],   // 停止序列
};

var result = await provider.ChatCompletionAsync(messages, tools, options);
```

### Provider 特有参数

通过 `AdditionalProperties` 传入：

```csharp
// OpenAI 特有
var options = new ChatOptions
{
    AdditionalProperties = new Dictionary<string, object>
    {
        ["frequency_penalty"] = 0.5f,
        ["presence_penalty"] = 0.5f,
        ["parallel_tool_calls"] = false
    }
};

// Anthropic 特有
var options = new ChatOptions
{
    AdditionalProperties = new Dictionary<string, object>
    {
        ["top_k"] = 40
    }
};
```

---

## 错误处理

所有 API 错误统一抛出 `TokenProviderException`，包含结构化的错误分类和原始响应体。

```csharp
try
{
    var result = await provider.ChatCompletionAsync(messages);
}
catch (TokenProviderException ex)
{
    Console.WriteLine($"错误类型: {ex.Kind}");        // InvalidModel, RateLimited, etc.
    Console.WriteLine($"HTTP 状态: {ex.StatusCode}"); // 400, 401, 403, 429, 500...
    Console.WriteLine($"错误信息: {ex.Message}");      // 人类可读的错误描述
    Console.WriteLine($"响应体:   {ex.ResponseBody}"); // API 原始错误 JSON
    Console.WriteLine($"错误代码: {ex.ErrorCode}");    // API 返回的 error code
    Console.WriteLine($"可重试:   {ex.IsRetryable}");  // RateLimited/ServerError = true
}
```

### 错误类型 (TokenProviderErrorKind)

| Kind | 说明 | 典型 HTTP 状态码 | 可重试 |
|------|------|-----------------|--------|
| `Authentication` | API Key 无效或无权限 | 401, 403 | 否 |
| `InvalidModel` | 模型不存在或无权限访问 | 400, 403, 404 | 否 |
| `RateLimited` | 请求频率超限 | 429 | **是** |
| `BadRequest` | 请求参数错误 | 400, 422 | 否 |
| `ServerError` | 服务端问题 | 500, 502, 503, 504 | **是** |
| `NetworkError` | 网络连接失败（DNS/超时/连接拒绝） | - | 否 |
| `JsonError` | JSON 序列化/解析失败 | - | 否 |
| `ContentFiltered` | 内容被安全过滤 | - | 否 |

### 模型不存在的自动识别

`ErrorMapper` 会从响应体中提取错误信息，自动识别模型相关错误：

```
// OpenAI 403: "This token has no access to model xxx"     → InvalidModel
// Anthropic 代理 400: param = "Not supported model xxx"   → InvalidModel
// OpenAI 400: "model xxx does not exist"                  → InvalidModel
```

---

## 消息模型详解

### ChatMessage

```csharp
// 系统提示
ChatMessage.System("你是一个助手")

// 用户消息
ChatMessage.User("你好")

// 助手回复（含 tool call，通常由 Provider 解析响应自动生成）
ChatMessage.Assistant(new List<ContentPart>
{
    new TextContent("让我查一下天气。"),
    new ToolCallContent("call_123", "get_weather", """{"location":"Beijing"}""")
})

// 工具执行结果
ChatMessage.ToolResult("call_123", """{"temp":"22°C"}""")
ChatMessage.ToolResult("call_123", "查询失败", isError: true)
```

### ContentPart 类型

| 类型 | 用途 | 来源 |
|------|------|------|
| `TextContent` | 文本内容 | 用户/助手 |
| `ToolCallContent` | 工具调用请求 | 模型返回 |
| `ToolResultContent` | 工具执行结果 | 你构造 |

### StreamingToken 类型

| 类型 | 含义 |
|------|------|
| `TextDelta` | 一小段文本增量，拼接后得到完整回复 |
| `ToolCallDelta` | 工具调用增量，`ArgumentsDelta` 是 JSON 片段，需按 `Id` 累积 |
| `UsageUpdate` | token 用量，可能在流式中途或结束时到达 |
| `StreamComplete` | 流结束信号，包含 `FinishReason` |

### FinishReason 值（已归一化）

| 值 | 含义 | OpenAI 原始值 | Anthropic 原始值 |
|----|------|--------------|-----------------|
| `"stop"` | 模型正常结束 | `stop` | `end_turn`, `stop_sequence` |
| `"tool_calls"` | 模型请求调用工具 | `tool_calls` | `tool_use` |
| `"max_tokens"` | 达到最大 token 限制 | `length` | `max_tokens` |
| `"content_filter"` | 内容被过滤 | `content_filter` | - |

---

## 架构说明

```
FundOffice.Copilot/
  Models/                           # 统一数据模型（零依赖）
    MessageRole.cs                  # System | User | Assistant | Tool
    ContentPart.cs                  # TextContent | ToolCallContent | ToolResultContent
    ChatMessage.cs                  # 消息，含工厂方法
    ToolDefinition.cs               # 工具定义（JsonElement schema）
    ChatOptions.cs                  # IChatOptions + ChatOptions
    ChatResult.cs                   # 非流式返回结果
    StreamingToken.cs               # TextDelta | ToolCallDelta | UsageUpdate | StreamComplete

  Providers/                        # 公开 API
    ITokenProvider.cs               # 核心接口：两个方法
    TokenProviderBase.cs            # 抽象基类，非流式默认聚合流式
    TokenProviderException.cs       # 统一异常 + TokenProviderErrorKind 枚举
    OpenAITokenProvider.cs          # OpenAI 实现（构造后不再持有 Options 引用）
    AnthropicTokenProvider.cs       # Anthropic 实现（构造后不再持有 Options 引用）

  Internal/                         # 内部实现，不对外暴露
    SseParser.cs                    # SSE 流解析（OpenAI + Anthropic 通用）
    OpenAIRequestBuilder.cs         # 构建 OpenAI JSON 请求 / 解析响应
    OpenAIStreamMapper.cs           # OpenAI SSE → StreamingToken
    AnthropicRequestBuilder.cs      # 构建 Anthropic JSON 请求 / 解析响应
    AnthropicStreamMapper.cs        # Anthropic SSE → StreamingToken（实例类，支持并发）
    ErrorMapper.cs                  # HTTP 错误 → TokenProviderException 统一转换

  Configuration/                    # Provider 配置（仅用于构造时传入）
    OpenAIOptions.cs                # ApiKey, Model, BaseUrl
    AnthropicOptions.cs             # ApiKey, Model, BaseUrl, ApiVersion
```

**依赖**：仅 `System.Net.Http`、`System.Text.Json`（.NET BCL 内置），无任何 NuGet 包。

**设计原则**：
- Provider 构造时一次性校验并存储配置为字段，不再持有 Options 引用
- `ITokenProvider` 只负责单次请求/响应，工具执行循环由调用方管理
- 流式 `ToolCallDelta.ArgumentsDelta` 是增量片段，需按 `Id` 累积
- `Internal/` 下的类是 `internal` 可见性，不暴露给消费者
- `AnthropicStreamMapper` 是实例类（非静态），避免并发请求共享状态
- 所有 API 错误统一转为 `TokenProviderException`，含结构化分类和原始响应体
