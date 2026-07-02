using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class StreamingTokenTests
{
    [TestMethod]
    public void TextDelta_Properties()
    {
        var td = new TextDelta("hello");

        Assert.AreEqual("hello", td.Text);
    }

    [TestMethod]
    public void ReasoningDelta_Properties()
    {
        var rd = new ReasoningDelta("thinking...");

        Assert.AreEqual("thinking...", rd.Text);
    }

    [TestMethod]
    public void ToolCallDelta_WithFunctionName()
    {
        var tcd = new ToolCallDelta("call_123", "get_weather", "{\"lo");

        Assert.AreEqual("call_123", tcd.Id);
        Assert.AreEqual("get_weather", tcd.FunctionName);
        Assert.AreEqual("{\"lo", tcd.ArgumentsDelta);
    }

    [TestMethod]
    public void ToolCallDelta_NullFunctionName()
    {
        var tcd = new ToolCallDelta("call_123", null, "cation\":\"BJ\"}");

        Assert.IsNull(tcd.FunctionName);
    }

    [TestMethod]
    public void UsageUpdate_AllNull()
    {
        var uu = new UsageUpdate(null, null);

        Assert.IsNull(uu.PromptTokens);
        Assert.IsNull(uu.CompletionTokens);
    }

    [TestMethod]
    public void UsageUpdate_WithValues()
    {
        var uu = new UsageUpdate(100, 50);

        Assert.AreEqual(100, uu.PromptTokens);
        Assert.AreEqual(50, uu.CompletionTokens);
    }

    [TestMethod]
    public void StreamComplete_WithFinishReason()
    {
        var sc = new StreamComplete("stop");

        Assert.AreEqual("stop", sc.FinishReason);
    }

    [TestMethod]
    public void StreamComplete_NullFinishReason()
    {
        var sc = new StreamComplete(null);

        Assert.IsNull(sc.FinishReason);
    }

    [TestMethod]
    public void StreamingToken_DiscriminatedUnion_PatternMatching()
    {
        StreamingToken[] tokens =
        [
            new TextDelta("text"),
            new ReasoningDelta("reason"),
            new ToolCallDelta("id", "fn", "{}"),
            new UsageUpdate(10, 20),
            new StreamComplete("stop")
        ];

        Assert.IsInstanceOfType(tokens[0], typeof(TextDelta));
        Assert.IsInstanceOfType(tokens[1], typeof(ReasoningDelta));
        Assert.IsInstanceOfType(tokens[2], typeof(ToolCallDelta));
        Assert.IsInstanceOfType(tokens[3], typeof(UsageUpdate));
        Assert.IsInstanceOfType(tokens[4], typeof(StreamComplete));
    }
}
