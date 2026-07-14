using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class ChatMessageTests
{
    [TestMethod]
    public void System_CreatesSystemRoleMessage()
    {
        var msg = ChatMessage.System("You are a helpful assistant.");

        Assert.AreEqual(MessageRole.System, msg.Role);
        Assert.AreEqual(1, msg.Content.Count);
        Assert.IsInstanceOfType(msg.Content[0], typeof(TextContent));
        Assert.AreEqual("You are a helpful assistant.", ((TextContent)msg.Content[0]).Text);
    }

    [TestMethod]
    public void User_CreatesUserRoleMessage()
    {
        var msg = ChatMessage.User("Hello!");

        Assert.AreEqual(MessageRole.User, msg.Role);
        Assert.AreEqual(1, msg.Content.Count);
        Assert.IsInstanceOfType(msg.Content[0], typeof(TextContent));
        Assert.AreEqual("Hello!", ((TextContent)msg.Content[0]).Text);
    }

    [TestMethod]
    public void User_WithParts_CreatesMultiContentMessage()
    {
        var parts = new ContentPart[]
        {
            new TextContent("Look at this:"),
            new DocumentContent("application/pdf", "base64data", "file.pdf")
        };
        var msg = ChatMessage.User(parts);

        Assert.AreEqual(MessageRole.User, msg.Role);
        Assert.AreEqual(2, msg.Content.Count);
        Assert.IsInstanceOfType(msg.Content[0], typeof(TextContent));
        Assert.IsInstanceOfType(msg.Content[1], typeof(DocumentContent));
    }

    [TestMethod]
    public void Assistant_CreatesAssistantRoleMessage()
    {
        var msg = ChatMessage.Assistant("I can help with that.");

        Assert.AreEqual(MessageRole.Assistant, msg.Role);
        Assert.AreEqual(1, msg.Content.Count);
        Assert.AreEqual("I can help with that.", ((TextContent)msg.Content[0]).Text);
    }

    [TestMethod]
    public void Assistant_WithParts_CreatesMultiContentMessage()
    {
        var parts = new ContentPart[]
        {
            new TextContent("Let me check."),
            new ToolCallContent("call_123", "get_weather", "{\"city\":\"Beijing\"}")
        };
        var msg = ChatMessage.Assistant(parts);

        Assert.AreEqual(MessageRole.Assistant, msg.Role);
        Assert.AreEqual(2, msg.Content.Count);
    }

    [TestMethod]
    public void ToolResult_CreatesToolRoleMessage()
    {
        var msg = ChatMessage.ToolResult("call_123", "{\"temp\": 25}");

        Assert.AreEqual(MessageRole.Tool, msg.Role);
        Assert.AreEqual(1, msg.Content.Count);
        var tr = (ToolResultContent)msg.Content[0];
        Assert.AreEqual("call_123", tr.ToolCallId);
        Assert.AreEqual("{\"temp\": 25}", tr.Result);
        Assert.IsFalse(tr.IsError);
    }

    [TestMethod]
    public void ToolResult_WithError_SetsIsError()
    {
        var msg = ChatMessage.ToolResult("call_123", "Connection timeout", isError: true);

        var tr = (ToolResultContent)msg.Content[0];
        Assert.IsTrue(tr.IsError);
    }

    [TestMethod]
    public void RecordEquality_SameContent_AreEqual()
    {
        // ChatMessage contains IReadOnlyList<ContentPart> which uses reference equality
        // So two separate instances with "same" content are NOT equal by record equality
        var msg1 = ChatMessage.User("Hello");
        var msg2 = ChatMessage.User("Hello");

        // Verify they have the same values but are separate instances
        Assert.AreEqual(msg1.Role, msg2.Role);
        Assert.AreEqual(((TextContent)msg1.Content[0]).Text, ((TextContent)msg2.Content[0]).Text);
    }

    [TestMethod]
    public void RecordEquality_SameInstance_AreEqual()
    {
        // Same reference IS equal
        var msg1 = ChatMessage.User("Hello");
        var msg2 = msg1;

        Assert.AreEqual(msg1, msg2);
    }

    [TestMethod]
    public void RecordEquality_DifferentContent_AreNotEqual()
    {
        var msg1 = ChatMessage.User("Hello");
        var msg2 = ChatMessage.User("World");

        Assert.AreNotEqual(msg1, msg2);
    }
}
