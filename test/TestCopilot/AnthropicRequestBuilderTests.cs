using System.Text.Json;
using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class AnthropicRequestBuilderTests
{
    #region NormalizeFinishReason

    [TestMethod]
    public void NormalizeFinishReason_EndTurn_MapsToStop()
    {
        Assert.AreEqual("stop", AnthropicRequestBuilder.NormalizeFinishReason("end_turn"));
    }

    [TestMethod]
    public void NormalizeFinishReason_ToolUse_MapsToToolCalls()
    {
        Assert.AreEqual("tool_calls", AnthropicRequestBuilder.NormalizeFinishReason("tool_use"));
    }

    [TestMethod]
    public void NormalizeFinishReason_MaxTokens()
    {
        Assert.AreEqual("max_tokens", AnthropicRequestBuilder.NormalizeFinishReason("max_tokens"));
    }

    [TestMethod]
    public void NormalizeFinishReason_StopSequence_MapsToStop()
    {
        Assert.AreEqual("stop", AnthropicRequestBuilder.NormalizeFinishReason("stop_sequence"));
    }

    [TestMethod]
    public void NormalizeFinishReason_Null_ReturnsStop()
    {
        Assert.AreEqual("stop", AnthropicRequestBuilder.NormalizeFinishReason(null));
    }

    #endregion

    #region ParseCompletion

    [TestMethod]
    public void ParseCompletion_TextResponse()
    {
        var json = """
        {
            "content": [{"type": "text", "text": "Hello!"}],
            "stop_reason": "end_turn",
            "usage": {"input_tokens": 10, "output_tokens": 5}
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = AnthropicRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual("Hello!", ((TextContent)result.Messages[0].Content[0]).Text);
        Assert.AreEqual("stop", result.FinishReason);
        Assert.AreEqual(10, result.PromptTokens);
        Assert.AreEqual(5, result.CompletionTokens);
    }

    [TestMethod]
    public void ParseCompletion_ToolUse()
    {
        var json = """
        {
            "content": [
                {"type": "text", "text": "Let me check."},
                {"type": "tool_use", "id": "toolu_123", "name": "get_weather", "input": {"city": "Beijing"}}
            ],
            "stop_reason": "tool_use",
            "usage": {"input_tokens": 10, "output_tokens": 20}
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = AnthropicRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual(2, result.Messages[0].Content.Count);
        Assert.IsInstanceOfType(result.Messages[0].Content[0], typeof(TextContent));
        var tc = (ToolCallContent)result.Messages[0].Content[1];
        Assert.AreEqual("toolu_123", tc.Id);
        Assert.AreEqual("get_weather", tc.FunctionName);
        Assert.IsTrue(tc.ArgumentsJson.Contains("Beijing"));
        Assert.AreEqual("tool_calls", result.FinishReason);
    }

    [TestMethod]
    public void ParseCompletion_EmptyText_Ignored()
    {
        var json = """
        {
            "content": [{"type": "text", "text": ""}],
            "stop_reason": "end_turn"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = AnthropicRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.AreEqual(0, result.Messages[0].Content.Count);
    }

    [TestMethod]
    public void ParseCompletion_NoUsage_ReturnsNullTokens()
    {
        var json = """
        {
            "content": [{"type": "text", "text": "Hi"}],
            "stop_reason": "end_turn"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = AnthropicRequestBuilder.ParseCompletion(doc.RootElement);

        Assert.IsNull(result.PromptTokens);
        Assert.IsNull(result.CompletionTokens);
    }

    #endregion

    #region BuildRequestBody

    [TestMethod]
    public void BuildRequestBody_SimpleMessage()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.AreEqual("claude-sonnet-4-20250514", root.GetProperty("model").GetString());
        Assert.AreEqual(16384, root.GetProperty("max_tokens").GetInt32());
        Assert.IsFalse(root.GetProperty("stream").GetBoolean());
    }

    [TestMethod]
    public void BuildRequestBody_SystemMessage_ExtractedToTopLevel()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are helpful."),
            ChatMessage.User("Hello")
        };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.AreEqual("You are helpful.", root.GetProperty("system").GetString());
        // System should NOT be in messages array
        var msgs = root.GetProperty("messages");
        Assert.AreEqual(1, msgs.GetArrayLength());
        Assert.AreEqual("user", msgs[0].GetProperty("role").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_MultipleSystemMessages_Joined()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("Part 1"),
            ChatMessage.System("Part 2"),
            ChatMessage.User("Hello")
        };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);

        Assert.AreEqual("Part 1\n\nPart 2", json.RootElement.GetProperty("system").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_OptionsOverride_MaxTokens()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions { MaxTokens = 4096 };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, options, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);

        Assert.AreEqual(4096, json.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [TestMethod]
    public void BuildRequestBody_StopSequences()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions { StopSequences = ["STOP"] };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, options, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);
        var stops = json.RootElement.GetProperty("stop_sequences");

        Assert.AreEqual(1, stops.GetArrayLength());
        Assert.AreEqual("STOP", stops[0].GetString());
    }

    [TestMethod]
    public void BuildRequestBody_ToolResult_MergedIntoUserMessage()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.Assistant("Let me check."),
            ChatMessage.ToolResult("toolu_123", "{\"temp\": 25}")
        };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);
        var msgs = json.RootElement.GetProperty("messages");

        // assistant, then user (tool result merged into user)
        Assert.AreEqual(2, msgs.GetArrayLength());
        Assert.AreEqual("assistant", msgs[0].GetProperty("role").GetString());
        Assert.AreEqual("user", msgs[1].GetProperty("role").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_AssistantWithToolCalls()
    {
        var parts = new ContentPart[]
        {
            new ToolCallContent("toolu_123", "get_weather", "{\"city\":\"Beijing\"}")
        };
        var messages = new List<ChatMessage> { ChatMessage.Assistant(parts) };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);
        var content = json.RootElement.GetProperty("messages")[0].GetProperty("content");

        Assert.AreEqual(JsonValueKind.Array, content.ValueKind);
        var block = content[0];
        Assert.AreEqual("tool_use", block.GetProperty("type").GetString());
        Assert.AreEqual("toolu_123", block.GetProperty("id").GetString());
        Assert.AreEqual("get_weather", block.GetProperty("name").GetString());
        Assert.IsTrue(block.GetProperty("input").GetProperty("city").GetString() == "Beijing");
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
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, tools, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);
        var toolsArr = json.RootElement.GetProperty("tools");

        Assert.AreEqual(1, toolsArr.GetArrayLength());
        var tool = toolsArr[0];
        Assert.AreEqual("get_weather", tool.GetProperty("name").GetString());
        Assert.AreEqual("Get weather", tool.GetProperty("description").GetString());
        Assert.IsTrue(tool.TryGetProperty("input_schema", out _));
    }

    [TestMethod]
    public void BuildRequestBody_NoTools_NoToolsField()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);

        Assert.IsFalse(json.RootElement.TryGetProperty("tools", out _));
    }

    [TestMethod]
    public void BuildRequestBody_TopK_AdditionalProperty()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions
        {
            AdditionalProperties = new Dictionary<string, object> { ["top_k"] = 40 }
        };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, options, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);

        Assert.AreEqual(40, json.RootElement.GetProperty("top_k").GetInt32());
    }

    [TestMethod]
    public void BuildRequestBody_ContinuousToolMessages_Merged()
    {
        // Two consecutive tool results should be merged into one user message
        var messages = new List<ChatMessage>
        {
            ChatMessage.Assistant("checking..."),
            ChatMessage.ToolResult("toolu_1", "result1"),
            ChatMessage.ToolResult("toolu_2", "result2")
        };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", false);
        var json = JsonDocument.Parse(body);
        var msgs = json.RootElement.GetProperty("messages");

        // assistant + merged user (tool results)
        Assert.AreEqual(2, msgs.GetArrayLength());
        Assert.AreEqual("user", msgs[1].GetProperty("role").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_StreamingFlag()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = AnthropicRequestBuilder.BuildRequestBody(messages, null, null, "claude-sonnet-4-20250514", true);
        var json = JsonDocument.Parse(body);

        Assert.IsTrue(json.RootElement.GetProperty("stream").GetBoolean());
    }

    #endregion
}
