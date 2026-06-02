using FMO.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FMO.AI;

public class MiMoTokenProvider : TokenProvider
{
    public override string Company => "XiaoMi";


    public string Ask(HttpClient client, string model, string prompt, string message)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidDataException("错误：API密钥未配置");
        if (string.IsNullOrWhiteSpace(Url))
            throw new InvalidDataException("错误：请求地址未配置");

        try
        {
            // 公共请求头
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", Key);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string requestBody;
            string responseContent;

            switch (Style)
            {
                case TokenProviderStyle.OpenAI:
                    // 按文档：OpenAI格式请求体
                    var openAiRequest = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = prompt },
                            new { role = "user", content = message }
                        },
                        max_completion_tokens = 1024,
                        temperature = 1.0,
                        top_p = 0.95,
                        stream = false,
                        stop = (string?)null,
                        frequency_penalty = 0,
                        presence_penalty = 0
                    };
                    requestBody = JsonSerializer.Serialize(openAiRequest);
                    var openAiResponse = client.PostAsync(Url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                    responseContent = openAiResponse.Content.ReadAsStringAsync().Result;

                    // 解析OpenAI格式响应
                    var openAiResult = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                    return openAiResult?.choices?[0]?.message?.content ?? "无有效返回";

                case TokenProviderStyle.Anthropic:
                    // 按文档：Anthropic格式请求体
                    var anthropicRequest = new
                    {
                        model = model,
                        max_tokens = 1024,
                        system = prompt,
                        messages = new[]
                        {
                            new { role = "user", content = new[] { new { type = "text", text = message } }}
                        },
                        top_p = 0.95,
                        stream = false,
                        temperature = 1.0,
                        stop_sequences = (string?)null
                    };
                    requestBody = JsonSerializer.Serialize(anthropicRequest);
                    var anthropicResponse = client.PostAsync(Url, new StringContent(requestBody, Encoding.UTF8, "application/json")).Result;
                    responseContent = anthropicResponse.Content.ReadAsStringAsync().Result;

                    // 解析Anthropic格式响应
                    var anthropicResult = JsonSerializer.Deserialize<AnthropicResponse>(responseContent);
                    return anthropicResult?.content?[0]?.text ?? "无有效返回";

                default:
                    return "错误：不支持的API风格";
            }
        }
        catch (Exception ex)
        {
            return $"调用异常：{ex.Message}";
        }
    }
}



public partial class MiMoTokenProviderViewModel : TokenProviderViewModel, IViewModel<MiMoTokenProvider, MiMoTokenProviderViewModel>
{
    public static string[] Models { get; } = ["mimo-v2.5-pro", "mimo-v2.5"];
}