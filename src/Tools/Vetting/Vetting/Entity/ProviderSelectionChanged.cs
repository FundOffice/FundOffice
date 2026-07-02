namespace Vetting.Entity;

/// <summary>
/// 选中的 provider 列表变化时发送
/// </summary>
public record ProviderSelectionChanged(string Identifier, bool IsSelected);
