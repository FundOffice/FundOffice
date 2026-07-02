using LiteDB;

namespace Vetting.Entity;

/// <summary>
/// 全局应用设置（单行记录，Id 固定为 1）
/// </summary>
public class AppSetting
{
    [BsonId]
    public int Id { get; set; } = 1;

    /// <summary>AI 回答模式：精确 = 仅从资料回答，完整 = 尽量回答</summary>
    public string AnswerMode { get; set; } = "精确";

    /// <summary>运行模式：逐步 = 手动点每步，自动 = 一键全流程</summary>
    public string RunMode { get; set; } = "逐步";

    /// <summary>已选中的 provider ID 列表（逗号分隔）</summary>
    public string SelectedProviderIds { get; set; } = "";
}
