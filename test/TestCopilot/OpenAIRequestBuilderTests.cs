using System.Text.Json;
using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class OpenAIRequestBuilderTests
{
    #region NormalizeFinishReason

    [TestMethod]
    public void NormalizeFinishReason_Stop()
    {
        Assert.AreEqual("stop", OpenAIRequestBuilder.NormalizeFinishReason("stop"));
    }

    [TestMethod]
    public void NormalizeFinishReason_ToolCalls()
    {
        Assert.AreEqual("tool_calls", OpenAIRequestBuilder.NormalizeFinishReason("tool_calls"));
    }

    [TestMethod]
    public void NormalizeFinishReason_Length_MapsToMaxTokens()
    {
        Assert.AreEqual("max_tokens", OpenAIRequestBuilder.NormalizeFinishReason("length"));
    }

    [TestMethod]
    public void NormalizeFinishReason_ContentFilter()
    {
        Assert.AreEqual("content_filter", OpenAIRequestBuilder.NormalizeFinishReason("content_filter"));
    }

    [TestMethod]
    public void NormalizeFinishReason_Null_ReturnsStop()
    {
        Assert.AreEqual("stop", OpenAIRequestBuilder.NormalizeFinishReason(null));
    }

    [TestMethod]
    public void NormalizeFinishReason_Unknown_ReturnsAsIs()
    {
        Assert.AreEqual("something_else", OpenAIRequestBuilder.NormalizeFinishReason("something_else"));
    }

    #endregion

    #region ParseCompletion

    [TestMethod]
    public void ParseCompletion_SimpleTextResponse()
    {
        var json = """
        {
            "choices": [{
                "message": {"role": "assistant", "content": "Hello!"},
                "finish_reason": "stop"
            }],
            "usage": {"prompt_tokens": 10, "completion_tokens": 5}
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual(MessageRole.Assistant, result.Messages[0].Role);
        Assert.AreEqual(1, result.Messages[0].Content.Count);
        Assert.AreEqual("Hello!", ((TextContent)result.Messages[0].Content[0]).Text);
        Assert.AreEqual("stop", result.FinishReason);
        Assert.AreEqual(10, result.PromptTokens);
        Assert.AreEqual(5, result.CompletionTokens);
    }

    [TestMethod]
    public void ParseCompletion_NullContent_ToolCallOnly()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [{
                        "id": "call_123",
                        "type": "function",
                        "function": {"name": "get_weather", "arguments": "{\"city\":\"Beijing\"}"}
                    }]
                },
                "finish_reason": "tool_calls"
            }]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual(1, result.Messages[0].Content.Count);
        var tc = (ToolCallContent)result.Messages[0].Content[0];
        Assert.AreEqual("call_123", tc.Id);
        Assert.AreEqual("get_weather", tc.FunctionName);
        Assert.AreEqual("{\"city\":\"Beijing\"}", tc.ArgumentsJson);
        Assert.AreEqual("tool_calls", result.FinishReason);
    }

    [TestMethod]
    public void ParseCompletion_TextAndToolCall()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "role": "assistant",
                    "content": "Let me check the weather.",
                    "tool_calls": [{
                        "id": "call_456",
                        "type": "function",
                        "function": {"name": "get_weather", "arguments": "{}"}
                    }]
                },
                "finish_reason": "tool_calls"
            }]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual(2, result.Messages[0].Content.Count);
        Assert.IsInstanceOfType(result.Messages[0].Content[0], typeof(TextContent));
        Assert.IsInstanceOfType(result.Messages[0].Content[1], typeof(ToolCallContent));
    }

    [TestMethod]
    public void ParseCompletion_MultipleToolCalls()
    {
        var json = """
        {
            "choices": [{
                "message": {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [
                        {"id": "c1", "type": "function", "function": {"name": "fn1", "arguments": "{}"}},
                        {"id": "c2", "type": "function", "function": {"name": "fn2", "arguments": "{\"x\":1}"}}
                    ]
                },
                "finish_reason": "tool_calls"
            }]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual(2, result.Messages[0].Content.Count);
        Assert.AreEqual("c1", ((ToolCallContent)result.Messages[0].Content[0]).Id);
        Assert.AreEqual("c2", ((ToolCallContent)result.Messages[0].Content[1]).Id);
    }

    [TestMethod]
    public void ParseCompletion_NoUsage_ReturnsNullTokens()
    {
        var json = """
        {
            "choices": [{
                "message": {"role": "assistant", "content": "Hi"},
                "finish_reason": "stop"
            }]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.IsNull(result.PromptTokens);
        Assert.IsNull(result.CompletionTokens);
    }

    [TestMethod]
    public void ParseCompletion_LengthFinishReason_Normalized()
    {
        var json = """
        {
            "choices": [{
                "message": {"role": "assistant", "content": "Truncated..."},
                "finish_reason": "length"
            }]
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual("max_tokens", result.FinishReason);
    }

    #endregion

    #region BuildRequestBody

    [TestMethod]
    public void BuildRequestBody_SimpleMessage_ContainsModelAndMessages()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.AreEqual("gpt-4o", root.GetProperty("model").GetString());
        Assert.IsFalse(root.GetProperty("stream").GetBoolean());
        Assert.AreEqual(1, root.GetProperty("messages").GetArrayLength());
    }

    [TestMethod]
    public void BuildRequestBody_Streaming_AddsStreamOptions()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", true);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.IsTrue(root.GetProperty("stream").GetBoolean());
        Assert.IsTrue(root.GetProperty("stream_options").GetProperty("include_usage").GetBoolean());
    }

    [TestMethod]
    public void BuildRequestBody_OptionsOverride_Model()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions { Model = "gpt-4o-mini" };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, options, "gpt-4o", false);
        var json = JsonDocument.Parse(body);

        Assert.AreEqual("gpt-4o-mini", json.RootElement.GetProperty("model").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_OptionsOverride_TemperatureAndMaxTokens()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions { Temperature = 0.7f, MaxTokens = 2048 };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, options, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.AreEqual(0.7f, root.GetProperty("temperature").GetSingle());
        Assert.AreEqual(2048, root.GetProperty("max_tokens").GetInt32());
    }

    [TestMethod]
    public void BuildRequestBody_StopSequences()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions { StopSequences = ["STOP", "END"] };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, options, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var stop = json.RootElement.GetProperty("stop");

        Assert.AreEqual(2, stop.GetArrayLength());
        Assert.AreEqual("STOP", stop[0].GetString());
        Assert.AreEqual("END", stop[1].GetString());
    }

    [TestMethod]
    public void BuildRequestBody_NoOptions_NoOptionalFields()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.IsFalse(root.TryGetProperty("temperature", out _));
        Assert.IsFalse(root.TryGetProperty("max_tokens", out _));
        Assert.IsFalse(root.TryGetProperty("top_p", out _));
        Assert.IsFalse(root.TryGetProperty("stop", out _));
        Assert.IsFalse(root.TryGetProperty("tools", out _));
    }

    [TestMethod]
    public void BuildRequestBody_SystemMessage_WrittenAsRole()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are helpful."),
            ChatMessage.User("Hello")
        };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var msgs = json.RootElement.GetProperty("messages");

        Assert.AreEqual(2, msgs.GetArrayLength());
        Assert.AreEqual("system", msgs[0].GetProperty("role").GetString());
        Assert.AreEqual("You are helpful.", msgs[0].GetProperty("content").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_ToolResult_WrittenCorrectly()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.ToolResult("call_123", "{\"temp\": 25}")
        };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var msg = json.RootElement.GetProperty("messages")[0];

        Assert.AreEqual("tool", msg.GetProperty("role").GetString());
        Assert.AreEqual("call_123", msg.GetProperty("tool_call_id").GetString());
        Assert.AreEqual("{\"temp\": 25}", msg.GetProperty("content").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_AssistantWithToolCalls()
    {
        var parts = new ContentPart[]
        {
            new TextContent("Let me check."),
            new ToolCallContent("call_1", "get_weather", "{\"city\":\"BJ\"}")
        };
        var messages = new List<ChatMessage> { ChatMessage.Assistant(parts) };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var msg = json.RootElement.GetProperty("messages")[0];

        Assert.AreEqual("assistant", msg.GetProperty("role").GetString());
        Assert.AreEqual("Let me check.", msg.GetProperty("content").GetString());
        Assert.AreEqual(1, msg.GetProperty("tool_calls").GetArrayLength());
        Assert.AreEqual("call_1", msg.GetProperty("tool_calls")[0].GetProperty("id").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_AssistantToolCallOnly_NullContent()
    {
        var parts = new ContentPart[]
        {
            new ToolCallContent("call_1", "fn", "{}")
        };
        var messages = new List<ChatMessage> { ChatMessage.Assistant(parts) };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var msg = json.RootElement.GetProperty("messages")[0];

        Assert.AreEqual(JsonValueKind.Null, msg.GetProperty("content").ValueKind);
    }

    [TestMethod]
    public void BuildRequestBody_WithTools()
    {
        var schema = JsonDocument.Parse("""{"type":"object","properties":{"city":{"type":"string"}}}""");
        var tools = new List<ToolDefinition>
        {
            new() { Name = "get_weather", Description = "Get weather", ParametersSchema = schema.RootElement.Clone() }
        };
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, tools, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var toolsArr = json.RootElement.GetProperty("tools");

        Assert.AreEqual(1, toolsArr.GetArrayLength());
        var tool = toolsArr[0];
        Assert.AreEqual("function", tool.GetProperty("type").GetString());
        Assert.AreEqual("get_weather", tool.GetProperty("function").GetProperty("name").GetString());
        Assert.AreEqual("Get weather", tool.GetProperty("function").GetProperty("description").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_UserMultimodal_ContentArray()
    {
        var parts = new ContentPart[]
        {
            new TextContent("Look at this:"),
            new DocumentContent("application/pdf", "base64data", "file.pdf")
        };
        var messages = new List<ChatMessage> { ChatMessage.User(parts) };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var content = json.RootElement.GetProperty("messages")[0].GetProperty("content");

        Assert.AreEqual(JsonValueKind.Array, content.ValueKind);
        Assert.AreEqual(2, content.GetArrayLength());
        Assert.AreEqual("text", content[0].GetProperty("type").GetString());
        Assert.AreEqual("file", content[1].GetProperty("type").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_AdditionalProperties_Included()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                ["frequency_penalty"] = 0.5f,
                ["presence_penalty"] = 0.3f
            }
        };
        var body = OpenAIRequestBuilder.BuildRequestBody(messages, null, options, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.AreEqual(0.5f, root.GetProperty("frequency_penalty").GetSingle());
        Assert.AreEqual(0.3f, root.GetProperty("presence_penalty").GetSingle());
    }

    #endregion
}
