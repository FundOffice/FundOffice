using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class OpenAIResponsesStreamMapperTests
{
    [TestMethod]
    public void MapEvent_OutputTextDelta()
    {
        var data = """{"type":"response.output_text.delta","output_index":0,"content_index":0,"delta":"Hello"}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.output_text.delta", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("Hello", ((TextDelta)tokens[0]).Text);
    }

    [TestMethod]
    public void MapEvent_ReasoningTextDelta()
    {
        var data = """{"type":"response.reasoning_text.delta","output_index":0,"content_index":0,"delta":"thinking..."}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.reasoning_text.delta", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("thinking...", ((ReasoningDelta)tokens[0]).Text);
    }

    [TestMethod]
    public void MapEvent_FunctionCallArgumentsDelta()
    {
        var data = """{"type":"response.function_call_arguments.delta","output_index":0,"call_id":"call_123","delta":"{\"lo"}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.function_call_arguments.delta", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        var tcd = (ToolCallDelta)tokens[0];
        Assert.AreEqual("call_123", tcd.Id);
        Assert.IsNull(tcd.FunctionName);
        Assert.AreEqual("{\"lo", tcd.ArgumentsDelta);
    }

    [TestMethod]
    public void MapEvent_OutputItemAdded_FunctionCall()
    {
        var data = """{"type":"response.output_item.added","output_index":0,"item":{"type":"function_call","id":"fc_1","call_id":"call_123","name":"get_weather","arguments":""}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.output_item.added", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        var tcd = (ToolCallDelta)tokens[0];
        Assert.AreEqual("call_123", tcd.Id);
        Assert.AreEqual("get_weather", tcd.FunctionName);
        Assert.AreEqual("", tcd.ArgumentsDelta);
    }

    [TestMethod]
    public void MapEvent_OutputItemAdded_NonFunctionCall_Ignored()
    {
        var data = """{"type":"response.output_item.added","output_index":0,"item":{"type":"message","id":"msg_1"}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.output_item.added", data).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapEvent_OutputItemAdded_UsesIdAsFallback_WhenNoCallId()
    {
        var data = """{"type":"response.output_item.added","output_index":0,"item":{"type":"function_call","id":"fc_1","name":"fn","arguments":""}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.output_item.added", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("fc_1", ((ToolCallDelta)tokens[0]).Id);
    }

    [TestMethod]
    public void MapEvent_ResponseCompleted_WithUsage()
    {
        var data = """{"type":"response.completed","response":{"id":"resp_1","status":"completed","usage":{"input_tokens":100,"output_tokens":50}}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.completed", data).ToList();

        Assert.AreEqual(2, tokens.Count);
        Assert.IsInstanceOfType(tokens[0], typeof(StreamComplete));
        Assert.AreEqual("stop", ((StreamComplete)tokens[0]).FinishReason);
        Assert.IsInstanceOfType(tokens[1], typeof(UsageUpdate));
        Assert.AreEqual(100, ((UsageUpdate)tokens[1]).PromptTokens);
        Assert.AreEqual(50, ((UsageUpdate)tokens[1]).CompletionTokens);
    }

    [TestMethod]
    public void MapEvent_ResponseCompleted_WithFunctionCall_ToolCallsReason()
    {
        var data = """{"type":"response.completed","response":{"status":"completed","output":[{"type":"function_call","id":"fc_1"}],"usage":{"input_tokens":10,"output_tokens":5}}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.completed", data).ToList();

        Assert.IsTrue(tokens.Any(t => t is StreamComplete sc && sc.FinishReason == "tool_calls"));
    }

    [TestMethod]
    public void MapEvent_ResponseCompleted_Incomplete()
    {
        var data = """{"type":"response.completed","response":{"status":"incomplete","usage":{}}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.completed", data).ToList();

        Assert.IsTrue(tokens.Any(t => t is StreamComplete sc && sc.FinishReason == "max_tokens"));
    }

    [TestMethod]
    public void MapEvent_ResponseFailed()
    {
        var data = """{"type":"response.failed","response":{}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.failed", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("content_filter", ((StreamComplete)tokens[0]).FinishReason);
    }

    [TestMethod]
    public void MapEvent_ResponseIncomplete()
    {
        var data = """{"type":"response.incomplete","response":{}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.incomplete", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("max_tokens", ((StreamComplete)tokens[0]).FinishReason);
    }

    [TestMethod]
    public void MapEvent_FunctionCallArgumentsDone_NoTokens()
    {
        var data = """{"type":"response.function_call_arguments.done","output_index":0,"call_id":"call_123","arguments":"{\"city\":\"BJ\"}"}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.function_call_arguments.done", data).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapEvent_OutputItemDone_NoTokens()
    {
        var data = """{"type":"response.output_item.done","output_index":0,"item":{}}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.output_item.done", data).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapEvent_ErrorEvent_Ignored()
    {
        var data = """{"type":"error","message":"Something went wrong","code":"server_error"}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("error", data).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapEvent_DoneMarker()
    {
        var tokens = OpenAIResponsesStreamMapper.MapEvent(null, "[DONE]").ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.IsNull(((StreamComplete)tokens[0]).FinishReason);
    }

    [TestMethod]
    public void MapEvent_InvalidJson_Ignored()
    {
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.output_text.delta", "not json").ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapEvent_UnknownEvent_Ignored()
    {
        var data = """{"type":"response.unknown.event","data":"something"}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.unknown.event", data).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapEvent_EmptyData_Ignored()
    {
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.output_text.delta", "").ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapEvent_ResponseCompleted_NoResponseField_EmitsStop()
    {
        var data = """{"type":"response.completed"}""";
        var tokens = OpenAIResponsesStreamMapper.MapEvent("response.completed", data).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("stop", ((StreamComplete)tokens[0]).FinishReason);
    }
}
