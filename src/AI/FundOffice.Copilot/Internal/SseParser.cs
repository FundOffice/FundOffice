using System.Runtime.CompilerServices;
using System.Text;

namespace FundOffice.Copilot.Internal;

/// <summary>
/// Server-Sent Events (SSE) 协议解析器。
///
/// SSE 协议格式（RFC 8895）：
///   event: &lt;event-type&gt;    # 可选，事件类型
///   data: &lt;payload&gt;        # 数据，可多行
///   id: &lt;id&gt;               # 可选，事件 ID（忽略）
///   retry: &lt;ms&gt;            # 可选，重连间隔（忽略）
///   : comment              # 注释（忽略）
///                          # 空行 = 事件分隔符，触发 dispatch
///
/// 本解析器兼容两种风格：
///   OpenAI:    只有 data: 行，没有 event: 行，以 "data: [DONE]" 结束
///   Anthropic: event: + data: 配对使用，event: 指定事件类型
///
/// 返回值元组：
///   EventType - 事件类型（OpenAI 场景下始终为 null）
///   Data      - 数据内容（多行 data 用换行符拼接）
///
/// 使用方式：
///   await foreach (var (eventType, data) in SseParser.ParseAsync(stream))
///   {
///       // eventType: "message_start", "content_block_delta" 等（Anthropic）
///       // data: JSON 字符串
///   }
/// </summary>
internal static class SseParser
{
    public static async IAsyncEnumerable<(string? EventType, string Data)> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? eventType = null;
        var dataBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            // 流结束（ReadLine 返回 null）
            if (line is null)
            {
                // 如果还有未 dispatch 的数据，发出最后一个事件
                if (dataBuilder.Length > 0)
                    yield return (eventType, dataBuilder.ToString());
                yield break;
            }

            // 空行 = 事件分隔符，dispatch 当前累积的数据
            if (line.Length == 0)
            {
                if (dataBuilder.Length > 0)
                {
                    yield return (eventType, dataBuilder.ToString());
                    eventType = null;   // 重置事件类型
                    dataBuilder.Clear();
                }
                continue;
            }

            // 解析 SSE 字段
            if (line.StartsWith("event: "))
            {
                // Anthropic 使用 event: 行指定事件类型
                eventType = line[7..].Trim();
            }
            else if (line.StartsWith("data: "))
            {
                // data: 后有空格的格式
                var data = line[6..];
                if (dataBuilder.Length > 0)
                    dataBuilder.Append('\n');
                dataBuilder.Append(data);
            }
            else if (line.StartsWith("data:"))
            {
                // data: 后无空格的格式（某些实现）
                var data = line[5..];
                if (dataBuilder.Length > 0)
                    dataBuilder.Append('\n');
                dataBuilder.Append(data);
            }
            // 其他字段（id:, retry:, 注释行）忽略
        }
    }
}
