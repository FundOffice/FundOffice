using System.Text.Json;
using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class OpenAIResponsesRequestBuilderTests
{
    #region NormalizeStatus

    [TestMethod]
    public void NormalizeStatus_Completed_MapsToStop()
    {
        Assert.AreEqual("stop", OpenAIResponsesRequestBuilder.NormalizeStatus("completed"));
    }

    [TestMethod]
    public void NormalizeStatus_Incomplete_MapsToMaxTokens()
    {
        Assert.AreEqual("max_tokens", OpenAIResponsesRequestBuilder.NormalizeStatus("incomplete"));
    }

    [TestMethod]
    public void NormalizeStatus_Failed_MapsToContentFilter()
    {
        Assert.AreEqual("content_filter", OpenAIResponsesRequestBuilder.NormalizeStatus("failed"));
    }

    [TestMethod]
    public void NormalizeStatus_Null_ReturnsNull()
    {
        Assert.IsNull(OpenAIResponsesRequestBuilder.NormalizeStatus(null));
    }

    [TestMethod]
    public void NormalizeStatus_Unknown_ReturnsAsIs()
    {
        Assert.AreEqual("custom", OpenAIResponsesRequestBuilder.NormalizeStatus("custom"));
    }

    #endregion

    #region ParseResponse

    [TestMethod]
    public void ParseResponse_TextMessage()
    {
        var json = """
        {
            "output": [{
                "type": "message",
                "role": "assistant",
                "content": [{"type": "output_text", "text": "Hello!"}]
            }],
            "status": "completed",
            "usage": {"input_tokens": 10, "output_tokens": 5}
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        Assert.AreEqual(1, result.Messages[0].Content.Count);
        Assert.AreEqual("Hello!", ((TextContent)result.Messages[0].Content[0]).Text);
        Assert.AreEqual("stop", result.FinishReason);
        Assert.AreEqual(10, result.PromptTokens);
        Assert.AreEqual(5, result.CompletionTokens);
    }

    [TestMethod]
    public void ParseResponse_TextType_AlsoAccepted()
    {
        var json = """
        {
            "output": [{
                "type": "message",
                "content": [{"type": "text", "text": "Hi"}]
            }],
            "status": "completed"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        Assert.AreEqual("Hi", ((TextContent)result.Messages[0].Content[0]).Text);
    }

    [TestMethod]
    public void ParseResponse_FunctionCall()
    {
        var json = """
        {
            "output": [{
                "type": "function_call",
                "id": "fc_1",
                "call_id": "call_123",
                "name": "get_weather",
                "arguments": "{\"city\":\"Beijing\"}"
            }],
            "status": "completed"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        var tc = (ToolCallContent)result.Messages[0].Content[0];
        Assert.AreEqual("call_123", tc.Id);
        Assert.AreEqual("get_weather", tc.FunctionName);
        Assert.AreEqual("{\"city\":\"Beijing\"}", tc.ArgumentsJson);
        // Function call forces tool_calls finish reason
        Assert.AreEqual("tool_calls", result.FinishReason);
    }

    [TestMethod]
    public void ParseResponse_FunctionCall_UsesIdFallback()
    {
        var json = """
        {
            "output": [{
                "type": "function_call",
                "id": "fc_1",
                "name": "fn",
                "arguments": "{}"
            }],
            "status": "completed"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        Assert.AreEqual("fc_1", ((ToolCallContent)result.Messages[0].Content[0]).Id);
    }

    [TestMethod]
    public void ParseResponse_Mixed_TextAndFunctionCall()
    {
        var json = """
        {
            "output": [
                {"type": "message", "content": [{"type": "output_text", "text": "Let me check."}]},
                {"type": "function_call", "call_id": "c1", "name": "fn", "arguments": "{}"}
            ],
            "status": "completed"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        Assert.AreEqual(2, result.Messages[0].Content.Count);
        Assert.IsInstanceOfType(result.Messages[0].Content[0], typeof(TextContent));
        Assert.IsInstanceOfType(result.Messages[0].Content[1], typeof(ToolCallContent));
    }

    [TestMethod]
    public void ParseResponse_IncompleteStatus()
    {
        var json = """
        {
            "output": [{"type": "message", "content": [{"type": "output_text", "text": "Partial..."}]}],
            "status": "incomplete"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        Assert.AreEqual("max_tokens", result.FinishReason);
    }

    [TestMethod]
    public void ParseResponse_NoUsage_ReturnsNullTokens()
    {
        var json = """
        {
            "output": [{"type": "message", "content": [{"type": "output_text", "text": "Hi"}]}],
            "status": "completed"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        Assert.IsNull(result.PromptTokens);
        Assert.IsNull(result.CompletionTokens);
    }

    [TestMethod]
    public void ParseResponse_EmptyOutput()
    {
        var json = """
        {
            "output": [],
            "status": "completed"
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var result = OpenAIResponsesRequestBuilder.ParseResponse(doc.RootElement);

        Assert.AreEqual(0, result.Messages[0].Content.Count);
        Assert.AreEqual("stop", result.FinishReason);
    }

    #endregion

    #region BuildRequestBody

    [TestMethod]
    public void BuildRequestBody_SimpleMessage()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.AreEqual("gpt-4o", root.GetProperty("model").GetString());
        Assert.IsFalse(root.GetProperty("stream").GetBoolean());
        Assert.IsTrue(root.TryGetProperty("input", out _));
    }

    [TestMethod]
    public void BuildRequestBody_SystemMessage_Instructions()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("You are helpful."),
            ChatMessage.User("Hello")
        };
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.AreEqual("You are helpful.", root.GetProperty("instructions").GetString());
        // System should not appear in input
        var input = root.GetProperty("input");
        Assert.AreEqual(1, input.GetArrayLength());
        Assert.AreEqual("user", input[0].GetProperty("role").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_MaxTokens_MapsToMaxOutputTokens()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions { MaxTokens = 4096 };
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, null, options, "gpt-4o", false);
        var json = JsonDocument.Parse(body);

        Assert.AreEqual(4096, json.RootElement.GetProperty("max_output_tokens").GetInt32());
    }

    [TestMethod]
    public void BuildRequestBody_StopSequences_Ignored()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var options = new ChatOptions { StopSequences = ["STOP"] };
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, null, options, "gpt-4o", false);
        var json = JsonDocument.Parse(body);

        Assert.IsFalse(json.RootElement.TryGetProperty("stop", out _));
        Assert.IsFalse(json.RootElement.TryGetProperty("stop_sequences", out _));
    }

    [TestMethod]
    public void BuildRequestBody_ToolResult_FunctionCallOutput()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.ToolResult("call_123", "{\"temp\": 25}")
        };
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var input = json.RootElement.GetProperty("input");
        var item = input[0];

        Assert.AreEqual("function_call_output", item.GetProperty("type").GetString());
        Assert.AreEqual("call_123", item.GetProperty("call_id").GetString());
        Assert.AreEqual("{\"temp\": 25}", item.GetProperty("output").GetString());
    }

    [TestMethod]
    public void BuildRequestBody_AssistantWithToolCalls_SeparateItems()
    {
        var parts = new ContentPart[]
        {
            new TextContent("Let me check."),
            new ToolCallContent("call_1", "get_weather", "{\"city\":\"BJ\"}")
        };
        var messages = new List<ChatMessage> { ChatMessage.Assistant(parts) };
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var input = json.RootElement.GetProperty("input");

        // assistant item + function_call item
        Assert.AreEqual(2, input.GetArrayLength());
        Assert.AreEqual("assistant", input[0].GetProperty("role").GetString());
        Assert.AreEqual("function_call", input[1].GetProperty("type").GetString());
        Assert.AreEqual("call_1", input[1].GetProperty("call_id").GetString());
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
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, tools, null, "gpt-4o", false);
        var json = JsonDocument.Parse(body);
        var toolsArr = json.RootElement.GetProperty("tools");

        Assert.AreEqual(1, toolsArr.GetArrayLength());
        var tool = toolsArr[0];
        Assert.AreEqual("function", tool.GetProperty("type").GetString());
        Assert.AreEqual("get_weather", tool.GetProperty("name").GetString());
        // Responses API: name/description at top level, not nested in "function"
        Assert.IsTrue(tool.TryGetProperty("description", out _));
        Assert.IsTrue(tool.TryGetProperty("parameters", out _));
    }

    [TestMethod]
    public void BuildRequestBody_StreamingFlag()
    {
        var messages = new List<ChatMessage> { ChatMessage.User("Hello") };
        var body = OpenAIResponsesRequestBuilder.BuildRequestBody(messages, null, null, "gpt-4o", true);
        var json = JsonDocument.Parse(body);

        Assert.IsTrue(json.RootElement.GetProperty("stream").GetBoolean());
    }

    #endregion
}
