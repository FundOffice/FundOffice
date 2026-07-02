namespace Vetting.Copilot.Models.Info;

public class ConversationRecord
{
    public int Id { get; set; }
    public string? SourcePath { get; set; }
    /// <summary>文件名</summary>
    public string? FileName { get; set; }
    public string? OutputPath { get; set; }
    public string? MessagesJson { get; set; }
    public int CompletedTurns { get; set; }
    public int TotalInput { get; set; }
    public int TotalOutput { get; set; }
    public string? Status { get; set; }
    public string? PlaceholdersJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}