using FMO.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FMO.AI;

/// <summary>
/// 百川智能 - 支持文件上传
/// </summary>
public class BaichuanTokenProvider : TokenProvider
{
    public override string Company => "Baichuan";
    public override TokenProviderStyle Style => TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://api.baichuan-ai.com/v1/chat/completions";

    protected override bool SupportsDocxFileUpload => true;

    protected override async Task<string> UploadFileAsync(HttpClient client, string filePath)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);

        var content = new MultipartFormDataContent();
        var fileStream = File.OpenRead(filePath);
        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
        content.Add(new StringContent("extract"), "purpose");

        var response = await client.PostAsync("https://api.baichuan-ai.com/v1/files", content);
        var responseText = await response.Content.ReadAsStringAsync();
        fileStream.Dispose();

        var result = JsonSerializer.Deserialize<BaichuanFileResponse>(responseText);
        if (result?.data?.file is null)
            throw new Exception($"文件上传失败: {responseText}");

        return result.data.file.id;
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
                        new { type = "file", file_id = fileId },
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

    private class BaichuanFileResponse
    {
        public BaichuanFileData? data { get; set; }
    }

    private class BaichuanFileData
    {
        public BaichuanFileInfo? file { get; set; }
    }

    private class BaichuanFileInfo
    {
        public string? id { get; set; }
    }
}

public partial class BaichuanTokenProviderViewModel : TokenProviderViewModel, IViewModel<BaichuanTokenProvider, BaichuanTokenProviderViewModel>
{
    public static string[] Models { get; } = ["Baichuan4", "Baichuan3-Turbo"];
}
