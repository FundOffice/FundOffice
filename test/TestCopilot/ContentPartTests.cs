using System.Text.Json;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class ContentPartTests
{
    [TestMethod]
    public void TextContent_Equality()
    {
        var a = new TextContent("hello");
        var b = new TextContent("hello");
        var c = new TextContent("world");

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
    }

    [TestMethod]
    public void ToolCallContent_Equality()
    {
        var a = new ToolCallContent("id1", "fn", "{}");
        var b = new ToolCallContent("id1", "fn", "{}");
        var c = new ToolCallContent("id2", "fn", "{}");

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
    }

    [TestMethod]
    public void ToolCallContent_Properties()
    {
        var tc = new ToolCallContent("call_abc", "get_weather", "{\"city\":\"Beijing\"}");

        Assert.AreEqual("call_abc", tc.Id);
        Assert.AreEqual("get_weather", tc.FunctionName);
        Assert.AreEqual("{\"city\":\"Beijing\"}", tc.ArgumentsJson);
    }

    [TestMethod]
    public void ToolResultContent_DefaultIsError_IsFalse()
    {
        var tr = new ToolResultContent("call_1", "result");

        Assert.IsFalse(tr.IsError);
    }

    [TestMethod]
    public void ToolResultContent_IsErrorTrue()
    {
        var tr = new ToolResultContent("call_1", "error msg", IsError: true);

        Assert.IsTrue(tr.IsError);
    }

    [TestMethod]
    public void DocumentContent_WithFileName()
    {
        var doc = new DocumentContent("application/pdf", "base64data", "test.pdf");

        Assert.AreEqual("application/pdf", doc.MediaType);
        Assert.AreEqual("base64data", doc.Data);
        Assert.AreEqual("test.pdf", doc.FileName);
    }

    [TestMethod]
    public void DocumentContent_NullFileName()
    {
        var doc = new DocumentContent("application/pdf", "base64data");

        Assert.IsNull(doc.FileName);
    }

    [TestMethod]
    public void ContentPart_DiscriminatedUnion_PatternMatching()
    {
        ContentPart[] parts =
        [
            new TextContent("text"),
            new ToolCallContent("id", "fn", "{}"),
            new ToolResultContent("tid", "result"),
            new DocumentContent("app/pdf", "data")
        ];

        Assert.IsInstanceOfType(parts[0], typeof(TextContent));
        Assert.IsInstanceOfType(parts[1], typeof(ToolCallContent));
        Assert.IsInstanceOfType(parts[2], typeof(ToolResultContent));
        Assert.IsInstanceOfType(parts[3], typeof(DocumentContent));
    }
}
