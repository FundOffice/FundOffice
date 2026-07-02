using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class TokenProviderBaseTests
{
    /// <summary>
    /// Test implementation of TokenProviderBase that yields predefined streaming tokens.
    /// </summary>
    private sealed class TestProvider : TokenProviderBase
    {
        private readonly List<StreamingToken> _tokens;

        public override string Identifier => "test-provider";

        public TestProvider(List<StreamingToken> tokens)
        {
            _tokens = tokens;
        }

#pragma warning disable CS1998 // Async method lacks 'await' - intentional test implementation
        public override async IAsyncEnumerable<StreamingToken> ChatCompletionStreamAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            IChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            foreach (var token in _tokens)
            {
                yield return token;
            }
        }
#pragma warning restore CS1998
    }

    [TestMethod]
    public async Task ChatCompletionAsync_TextOnly_AggregatesTextDelta()
    {
        var tokens = new List<StreamingToken>
        {
            new TextDelta("Hello "),
            new TextDelta("world!"),
            new StreamComplete("stop")
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Hi") };

        var result = await provider.ChatCompletionAsync(messages);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual(1, result.Messages[0].Content.Count);
        Assert.AreEqual("Hello world!", ((TextContent)result.Messages[0].Content[0]).Text);
        Assert.AreEqual("stop", result.FinishReason);
    }

    [TestMethod]
    public async Task ChatCompletionAsync_ToolCall_AggregatesArguments()
    {
        var tokens = new List<StreamingToken>
        {
            new ToolCallDelta("call_1", "get_weather", ""),
            new ToolCallDelta("call_1", null, "{\"lo"),
            new ToolCallDelta("call_1", null, "cation\":"),
            new ToolCallDelta("call_1", null, "\"BJ\"}"),
            new StreamComplete("tool_calls")
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Weather?") };

        var result = await provider.ChatCompletionAsync(messages);

        Assert.AreEqual(1, result.Messages[0].Content.Count);
        var tc = (ToolCallContent)result.Messages[0].Content[0];
        Assert.AreEqual("call_1", tc.Id);
        Assert.AreEqual("get_weather", tc.FunctionName);
        Assert.AreEqual("{\"location\":\"BJ\"}", tc.ArgumentsJson);
        Assert.AreEqual("tool_calls", result.FinishReason);
    }

    [TestMethod]
    public async Task ChatCompletionAsync_MultipleToolCalls()
    {
        var tokens = new List<StreamingToken>
        {
            new ToolCallDelta("c1", "fn1", "{}"),
            new ToolCallDelta("c2", "fn2", "{\"x\":1}"),
            new StreamComplete("tool_calls")
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Do two things") };

        var result = await provider.ChatCompletionAsync(messages);

        Assert.AreEqual(2, result.Messages[0].Content.Count);
        Assert.AreEqual("c1", ((ToolCallContent)result.Messages[0].Content[0]).Id);
        Assert.AreEqual("fn1", ((ToolCallContent)result.Messages[0].Content[0]).FunctionName);
        Assert.AreEqual("c2", ((ToolCallContent)result.Messages[0].Content[1]).Id);
        Assert.AreEqual("fn2", ((ToolCallContent)result.Messages[0].Content[1]).FunctionName);
    }

    [TestMethod]
    public async Task ChatCompletionAsync_TextAndToolCall()
    {
        var tokens = new List<StreamingToken>
        {
            new TextDelta("Let me check."),
            new ToolCallDelta("c1", "get_weather", "{}"),
            new StreamComplete("tool_calls")
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Weather?") };

        var result = await provider.ChatCompletionAsync(messages);

        Assert.AreEqual(2, result.Messages[0].Content.Count);
        Assert.IsInstanceOfType(result.Messages[0].Content[0], typeof(TextContent));
        Assert.IsInstanceOfType(result.Messages[0].Content[1], typeof(ToolCallContent));
    }

    [TestMethod]
    public async Task ChatCompletionAsync_UsageUpdate_TakesLastValues()
    {
        var tokens = new List<StreamingToken>
        {
            new UsageUpdate(10, 5),
            new TextDelta("Hi"),
            new UsageUpdate(10, 8), // Later update overrides
            new StreamComplete("stop")
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Hi") };

        var result = await provider.ChatCompletionAsync(messages);

        Assert.AreEqual(10, result.PromptTokens);
        Assert.AreEqual(8, result.CompletionTokens);
    }

    [TestMethod]
    public async Task ChatCompletionAsync_ReasoningDelta_Ignored()
    {
        var tokens = new List<StreamingToken>
        {
            new ReasoningDelta("thinking..."),
            new TextDelta("Answer"),
            new StreamComplete("stop")
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Question") };

        var result = await provider.ChatCompletionAsync(messages);

        // ReasoningDelta should NOT appear in result
        Assert.AreEqual(1, result.Messages[0].Content.Count);
        Assert.AreEqual("Answer", ((TextContent)result.Messages[0].Content[0]).Text);
    }

    [TestMethod]
    public async Task ChatCompletionAsync_NullFinishReason_StreamComplete_Ignored()
    {
        var tokens = new List<StreamingToken>
        {
            new TextDelta("Hi"),
            new StreamComplete(null), // [DONE] signal with null reason
            new StreamComplete("stop")  // Actual finish
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Hi") };

        var result = await provider.ChatCompletionAsync(messages);

        // Should use the non-null finish reason
        Assert.AreEqual("stop", result.FinishReason);
    }

    [TestMethod]
    public async Task ChatCompletionAsync_EmptyStream_NoContent()
    {
        var tokens = new List<StreamingToken>
        {
            new StreamComplete("stop")
        };
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Hi") };

        var result = await provider.ChatCompletionAsync(messages);

        Assert.AreEqual(0, result.Messages[0].Content.Count);
    }

    [TestMethod]
    public async Task ChatCompletionAsync_NoTokens_ReturnsEmptyResult()
    {
        var tokens = new List<StreamingToken>();
        var provider = new TestProvider(tokens);
        var messages = new List<ChatMessage> { ChatMessage.User("Hi") };

        var result = await provider.ChatCompletionAsync(messages);

        Assert.AreEqual(0, result.Messages[0].Content.Count);
        Assert.IsNull(result.FinishReason);
        Assert.IsNull(result.PromptTokens);
        Assert.IsNull(result.CompletionTokens);
    }

    [TestMethod]
    public async Task GetModelsAsync_Default_ReturnsEmptyList()
    {
        var provider = new TestProvider([]);
        var models = await provider.GetModelsAsync();

        Assert.AreEqual(0, models.Count);
    }

    [TestMethod]
    public void Identifier_ReturnsTestProvider()
    {
        var provider = new TestProvider([]);
        Assert.AreEqual("test-provider", provider.Identifier);
    }
}
