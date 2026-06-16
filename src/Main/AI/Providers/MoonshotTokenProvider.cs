using FMO.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FMO.AI;

/// <summary>
/// Moonshot（月之暗面）- 支持文件上传
/// </summary>
public class MoonshotTokenProvider : TokenProvider
{
    public override string Company => "Moonshot";
    public override TokenProviderStyle Style { get; set; } = TokenProviderStyle.OpenAI;
    public override string Url { get; set; } = "https://api.moonshot.cn/v1/chat/completions";

    protected override bool SupportsDocxFileUpload => true;

    protected override async Task<string> UploadFileAsync(HttpClient client, string filePath)
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);

        // Step 1: 上传文件
        var content = new MultipartFormDataContent();
        var fileStream = File.OpenRead(filePath);
        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
        content.Add(new StringContent("file-extract"), "purpose");

        var uploadResponse = await client.PostAsync("https://api.moonshot.cn/v1/files", content);
        var uploadText = await uploadResponse.Content.ReadAsStringAsync();
        fileStream.Dispose();

        var uploadResult = JsonSerializer.Deserialize<MoonshotFileResponse>(uploadText);
        if (uploadResult?.id is null)
            throw new Exception($"文件上传失败: {uploadText}");

        // Step 2: 获取文件内容
        var contentResponse = await client.GetAsync($"https://api.moonshot.cn/v1/files/{uploadResult.id}/content");
        var fileContent = await contentResponse.Content.ReadAsStringAsync();

        return fileContent;
    }

    protected override async Task<string> AskWithFileIdAsync(HttpClient client, string model, string prompt, string fileContent, IProgress<int>? progress = null)
    {
        // Moonshot 文件上传后获取的是文本内容，直接作为 message 发送
        return await AskAsync(client, model, prompt, fileContent, progress);
    }

    private class MoonshotFileResponse
    {
        public string? id { get; set; }
    }
}

public partial class MoonshotTokenProviderViewModel : TokenProviderViewModel, IViewModel<MoonshotTokenProvider, MoonshotTokenProviderViewModel>
{
    public override TokenProviderStyle[] SupportedStyles { get; } = [TokenProviderStyle.OpenAI];
    public override string ModelsUrl => BuildApiUrl(Url, 2, "/models");
    public static string[] Models { get; } = ["moonshot-v1-128k", "moonshot-v1-32k", "moonshot-v1-8k"];
}
