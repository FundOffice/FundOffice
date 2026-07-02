using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Models;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class SseParserTests
{
    private static async Task<List<(string? EventType, string Data)>> ParseAllAsync(string input)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input));
        var results = new List<(string? EventType, string Data)>();
        await foreach (var item in SseParser.ParseAsync(stream))
        {
            results.Add(item);
        }
        return results;
    }

    [TestMethod]
    public async Task SingleDataLine_OpenAI_Format()
    {
        var results = await ParseAllAsync("data: {\"choices\":[]}\n\n");

        Assert.AreEqual(1, results.Count);
        Assert.IsNull(results[0].EventType);
        Assert.AreEqual("{\"choices\":[]}", results[0].Data);
    }

    [TestMethod]
    public async Task DoneMarker_OpenAI()
    {
        var results = await ParseAllAsync("data: [DONE]\n\n");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("[DONE]", results[0].Data);
    }

    [TestMethod]
    public async Task EventAndData_Anthropic_Format()
    {
        var input = "event: message_start\ndata: {\"type\":\"message_start\"}\n\n";
        var results = await ParseAllAsync(input);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("message_start", results[0].EventType);
        Assert.AreEqual("{\"type\":\"message_start\"}", results[0].Data);
    }

    [TestMethod]
    public async Task MultiLineData_JoinedWithNewline()
    {
        var input = "data: line1\ndata: line2\n\n";
        var results = await ParseAllAsync(input);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("line1\nline2", results[0].Data);
    }

    [TestMethod]
    public async Task MultipleEvents_SeparatedByBlankLine()
    {
        var input = "data: first\n\ndata: second\n\n";
        var results = await ParseAllAsync(input);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("first", results[0].Data);
        Assert.AreEqual("second", results[1].Data);
    }

    [TestMethod]
    public async Task Comments_Ignored()
    {
        var input = ": this is a comment\ndata: payload\n\n";
        var results = await ParseAllAsync(input);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("payload", results[0].Data);
    }

    [TestMethod]
    public async Task DataWithoutSpace_AfterColon()
    {
        var input = "data:{\"ok\":true}\n\n";
        var results = await ParseAllAsync(input);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("{\"ok\":true}", results[0].Data);
    }

    [TestMethod]
    public async Task EmptyStream_NoResults()
    {
        var results = await ParseAllAsync("");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task OnlyBlankLines_NoResults()
    {
        var results = await ParseAllAsync("\n\n\n");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task TrailingDataWithoutBlankLine_FlushedOnStreamEnd()
    {
        // Stream ends without final blank line - data should still be emitted
        var input = "data: final";
        var results = await ParseAllAsync(input);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("final", results[0].Data);
    }

    [TestMethod]
    public async Task EventIdAndRetry_Ignored()
    {
        var input = "id: 123\nretry: 3000\ndata: payload\n\n";
        var results = await ParseAllAsync(input);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("payload", results[0].Data);
    }

    [TestMethod]
    public async Task Anthropic_FullSequence()
    {
        var input = """
            event: message_start
            data: {"type":"message_start","message":{"usage":{"input_tokens":10}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        var results = await ParseAllAsync(input);

        Assert.AreEqual(5, results.Count);
        Assert.AreEqual("message_start", results[0].EventType);
        Assert.AreEqual("content_block_start", results[1].EventType);
        Assert.AreEqual("content_block_delta", results[2].EventType);
        Assert.AreEqual("content_block_stop", results[3].EventType);
        Assert.AreEqual("message_stop", results[4].EventType);
    }

    [TestMethod]
    public async Task Cancellation_StopsProcessing()
    {
        var input = "data: first\n\ndata: second\n\ndata: third\n\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input));
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var results = new List<(string? EventType, string Data)>();
        await foreach (var item in SseParser.ParseAsync(stream, cts.Token))
        {
            results.Add(item);
        }

        Assert.AreEqual(0, results.Count);
    }
}
