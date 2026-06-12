using FMO.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FMO.AI;

/// <summary>
/// 豆包（火山引擎）- 支持文件上传和 base64 inline
/// </summary>
public class DoubaoTokenProvider : TokenProvider
{
    public override string Company => "Doubao";
    public override TokenProviderStyle Style { get; set; } = TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";

    protected override bool SupportsDocxFileUpload => true;
    protected override bool SupportsDocxBase64Inline => true;

    protected override async Task<string> UploadFileAsync(HttpClient client, string filePath)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);

        var content = new MultipartFormDataContent();
        var fileStream = File.OpenRead(filePath);
        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
        content.Add(new StringContent("file-extract"), "purpose");

        var response = await client.PostAsync("https://ark.cn-beijing.volces.com/api/v3/files", content);
        var responseText = await response.Content.ReadAsStringAsync();
        fileStream.Dispose();

        var result = JsonSerializer.Deserialize<DoubaoFileResponse>(responseText);
        if (result?.id is null)
            throw new Exception($"文件上传失败: {responseText}");

        return result.id;
    }

    protected override async Task<string> AskWithFileIdAsync(HttpClient client, string model, string prompt, string fileId)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var request = new
        {
            model = model,
            messages = new object[]
            {
                new { role = "system", content = prompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "file", file = new { file_id = fileId } },
                        new { type = "text", text = "请从上面的文档中提取基金信息" }
                    }
                }
            },
            max_tokens = 8192,
            temperature = 0.1,
            stream = false
        };

        var requestBody = JsonSerializer.Serialize(request);
        var response = await client.PostAsync(Url, new StringContent(requestBody, Encoding.UTF8, "application/json"));
        var responseText = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<OpenAIResponse>(responseText);
        return result?.choices?[0]?.message?.content ?? "无有效返回";
    }

    private class DoubaoFileResponse
    {
        public string? id { get; set; }
    }
}

public partial class DoubaoTokenProviderViewModel : TokenProviderViewModel, IViewModel<DoubaoTokenProvider, DoubaoTokenProviderViewModel>
{
    public override TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.OpenAI];
    public override string ModelsUrl => BuildApiUrl(Url, 2, "/models");
    public static string[] Models { get; } = ["doubao-1.5-pro-32k", "doubao-1.5-lite-32k"];
}
