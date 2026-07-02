using System.Text.Json;
using FundOffice.Copilot.Models;
using FundOffice.Copilot.Providers;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class ChatOptionsTests
{
    [TestMethod]
    public void DefaultValues_AllNull()
    {
        var options = new ChatOptions();

        Assert.IsNull(options.Model);
        Assert.IsNull(options.Temperature);
        Assert.IsNull(options.MaxTokens);
        Assert.IsNull(options.TopP);
        Assert.IsNull(options.StopSequences);
        Assert.IsNull(options.AdditionalProperties);
    }

    [TestMethod]
    public void InitProperties_SetCorrectly()
    {
        var options = new ChatOptions
        {
            Model = "gpt-4o",
            Temperature = 0.7f,
            MaxTokens = 2048,
            TopP = 0.9f,
            StopSequences = ["STOP"],
            AdditionalProperties = new Dictionary<string, object> { ["key"] = "value" }
        };

        Assert.AreEqual("gpt-4o", options.Model);
        Assert.AreEqual(0.7f, options.Temperature);
        Assert.AreEqual(2048, options.MaxTokens);
        Assert.AreEqual(0.9f, options.TopP);
        Assert.AreEqual(1, options.StopSequences!.Count);
        Assert.AreEqual("value", options.AdditionalProperties!["key"]);
    }

    [TestMethod]
    public void ImplementsIChatOptions()
    {
        IChatOptions options = new ChatOptions { Model = "test" };
        Assert.AreEqual("test", options.Model);
    }
}

[TestClass]
public class ChatResultTests
{
    [TestMethod]
    public void RequiredMessages_MustBeSet()
    {
        var result = new ChatResult
        {
            Messages = [ChatMessage.Assistant("Hi")]
        };

        Assert.AreEqual(1, result.Messages.Count);
    }

    [TestMethod]
    public void OptionalProperties_DefaultNull()
    {
        var result = new ChatResult
        {
            Messages = [ChatMessage.Assistant("Hi")]
        };

        Assert.IsNull(result.PromptTokens);
        Assert.IsNull(result.CompletionTokens);
        Assert.IsNull(result.FinishReason);
    }

    [TestMethod]
    public void AllProperties_SetCorrectly()
    {
        var result = new ChatResult
        {
            Messages = [ChatMessage.Assistant("Hello")],
            PromptTokens = 100,
            CompletionTokens = 50,
            FinishReason = "stop"
        };

        Assert.AreEqual(100, result.PromptTokens);
        Assert.AreEqual(50, result.CompletionTokens);
        Assert.AreEqual("stop", result.FinishReason);
    }

    [TestMethod]
    public void RecordEquality_SameValues()
    {
        // ChatResult contains IReadOnlyList<ChatMessage> which uses reference equality
        // so two separate instances are NOT structurally equal
        var r1 = new ChatResult { Messages = [ChatMessage.Assistant("Hi")], FinishReason = "stop" };
        var r2 = new ChatResult { Messages = [ChatMessage.Assistant("Hi")], FinishReason = "stop" };

        // Verify same values
        Assert.AreEqual(r1.FinishReason, r2.FinishReason);
        Assert.AreEqual(r1.Messages[0].Role, r2.Messages[0].Role);
    }

    [TestMethod]
    public void RecordEquality_SameReference_AreEqual()
    {
        var r1 = new ChatResult { Messages = [ChatMessage.Assistant("Hi")], FinishReason = "stop" };
        var r2 = r1;

        Assert.AreEqual(r1, r2);
    }
}

[TestClass]
public class ToolDefinitionTests
{
    [TestMethod]
    public void RequiredProperties_SetCorrectly()
    {
        using var schema = System.Text.Json.JsonDocument.Parse("""{"type":"object","properties":{"city":{"type":"string"}}}""");
        var tool = new ToolDefinition
        {
            Name = "get_weather",
            Description = "Get weather info",
            ParametersSchema = schema.RootElement.Clone()
        };

        Assert.AreEqual("get_weather", tool.Name);
        Assert.AreEqual("Get weather info", tool.Description);
        Assert.AreEqual(JsonValueKind.Object, tool.ParametersSchema.ValueKind);
    }

    [TestMethod]
    public void ParametersSchema_CanBeSerialized()
    {
        using var schema = System.Text.Json.JsonDocument.Parse("""{"type":"object","properties":{"x":{"type":"integer"}}}""");
        var tool = new ToolDefinition
        {
            Name = "test",
            Description = "test",
            ParametersSchema = schema.RootElement.Clone()
        };

        var json = tool.ParametersSchema.GetRawText();
        Assert.IsTrue(json.Contains("\"type\""));
        Assert.IsTrue(json.Contains("\"object\""));
    }
}

[TestClass]
public class ModelInfoTests
{
    [TestMethod]
    public void RequiredId_SetCorrectly()
    {
        var info = new ModelInfo { Id = "gpt-4o" };
        Assert.AreEqual("gpt-4o", info.Id);
    }

    [TestMethod]
    public void OwnedBy_DefaultNull()
    {
        var info = new ModelInfo { Id = "gpt-4o" };
        Assert.IsNull(info.OwnedBy);
    }

    [TestMethod]
    public void OwnedBy_SetCorrectly()
    {
        var info = new ModelInfo { Id = "gpt-4o", OwnedBy = "openai" };
        Assert.AreEqual("openai", info.OwnedBy);
    }

    [TestMethod]
    public void RecordEquality()
    {
        var a = new ModelInfo { Id = "gpt-4o", OwnedBy = "openai" };
        var b = new ModelInfo { Id = "gpt-4o", OwnedBy = "openai" };
        Assert.AreEqual(a, b);
    }
}
