using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class OpenAIStreamMapperTests
{
    [TestMethod]
    public void MapLine_DoneMarker_ReturnsStreamComplete()
    {
        var tokens = OpenAIStreamMapper.MapLine("[DONE]").ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.IsInstanceOfType(tokens[0], typeof(StreamComplete));
        Assert.IsNull(((StreamComplete)tokens[0]).FinishReason);
    }

    [TestMethod]
    public void MapLine_TextDelta()
    {
        var json = """{"choices":[{"delta":{"content":"Hello"},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.IsInstanceOfType(tokens[0], typeof(TextDelta));
        Assert.AreEqual("Hello", ((TextDelta)tokens[0]).Text);
    }

    [TestMethod]
    public void MapLine_NullContent_Ignored()
    {
        var json = """{"choices":[{"delta":{"content":null},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapLine_EmptyContent_Ignored()
    {
        var json = """{"choices":[{"delta":{"content":""},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapLine_ReasoningContent()
    {
        var json = """{"choices":[{"delta":{"reasoning_content":"thinking..."},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.IsInstanceOfType(tokens[0], typeof(ReasoningDelta));
        Assert.AreEqual("thinking...", ((ReasoningDelta)tokens[0]).Text);
    }

    [TestMethod]
    public void MapLine_ToolCallDelta_WithIdAndName()
    {
        var json = """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_123","function":{"name":"get_weather","arguments":""}}]},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        var tcd = (ToolCallDelta)tokens[0];
        Assert.AreEqual("call_123", tcd.Id);
        Assert.AreEqual("get_weather", tcd.FunctionName);
        Assert.AreEqual("", tcd.ArgumentsDelta);
    }

    [TestMethod]
    public void MapLine_ToolCallDelta_ArgumentsOnly()
    {
        var json = """{"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"lo"}}]},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        var tcd = (ToolCallDelta)tokens[0];
        Assert.IsNull(tcd.FunctionName);
        Assert.AreEqual("{\"lo", tcd.ArgumentsDelta);
    }

    [TestMethod]
    public void MapLine_ToolCallDelta_UsesIndexAsStableId_WhenNoId()
    {
        var json = """{"choices":[{"delta":{"tool_calls":[{"index":3,"function":{"arguments":"x"}}]},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("3", ((ToolCallDelta)tokens[0]).Id);
    }

    [TestMethod]
    public void MapLine_FinishReason_Stop()
    {
        var json = """{"choices":[{"delta":{},"index":0,"finish_reason":"stop"}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.IsTrue(tokens.Any(t => t is StreamComplete sc && sc.FinishReason == "stop"));
    }

    [TestMethod]
    public void MapLine_FinishReason_Length_NormalizedToMaxTokens()
    {
        var json = """{"choices":[{"delta":{},"index":0,"finish_reason":"length"}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.IsTrue(tokens.Any(t => t is StreamComplete sc && sc.FinishReason == "max_tokens"));
    }

    [TestMethod]
    public void MapLine_FinishReason_ToolCalls()
    {
        var json = """{"choices":[{"delta":{},"index":0,"finish_reason":"tool_calls"}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.IsTrue(tokens.Any(t => t is StreamComplete sc && sc.FinishReason == "tool_calls"));
    }

    [TestMethod]
    public void MapLine_UsageUpdate()
    {
        var json = """{"usage":{"prompt_tokens":10,"completion_tokens":20}}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        var uu = (UsageUpdate)tokens[0];
        Assert.AreEqual(10, uu.PromptTokens);
        Assert.AreEqual(20, uu.CompletionTokens);
    }

    [TestMethod]
    public void MapLine_UsageUpdate_PromptOnly()
    {
        var json = """{"usage":{"prompt_tokens":5}}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        var uu = (UsageUpdate)tokens[0];
        Assert.AreEqual(5, uu.PromptTokens);
        Assert.IsNull(uu.CompletionTokens);
    }

    [TestMethod]
    public void MapLine_EmptyChoices_NoTokens()
    {
        var json = """{"choices":[]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapLine_InvalidJson_NoTokens()
    {
        var tokens = OpenAIStreamMapper.MapLine("not valid json").ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapLine_TextAndToolCall_SameMessage()
    {
        var json = """{"choices":[{"delta":{"content":"Let me check","tool_calls":[{"index":0,"id":"c1","function":{"name":"fn","arguments":""}}]},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        // Both text and tool call should be produced
        Assert.IsTrue(tokens.Any(t => t is TextDelta));
        Assert.IsTrue(tokens.Any(t => t is ToolCallDelta));
    }

    [TestMethod]
    public void MapLine_MultipleToolCalls()
    {
        var json = """{"choices":[{"delta":{"tool_calls":[{"index":0,"id":"c1","function":{"name":"fn1","arguments":""}},{"index":1,"id":"c2","function":{"name":"fn2","arguments":""}}]},"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        var toolDeltas = tokens.OfType<ToolCallDelta>().ToList();
        Assert.AreEqual(2, toolDeltas.Count);
        Assert.AreEqual("c1", toolDeltas[0].Id);
        Assert.AreEqual("fn1", toolDeltas[0].FunctionName);
        Assert.AreEqual("c2", toolDeltas[1].Id);
        Assert.AreEqual("fn2", toolDeltas[1].FunctionName);
    }

    [TestMethod]
    public void MapLine_NoDelta_NoTokens()
    {
        var json = """{"choices":[{"index":0}]}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void MapLine_UsageWithNoChoices_EmitsUsageOnly()
    {
        var json = """{"usage":{"prompt_tokens":100,"completion_tokens":50}}""";
        var tokens = OpenAIStreamMapper.MapLine(json).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.IsInstanceOfType(tokens[0], typeof(UsageUpdate));
    }
}
