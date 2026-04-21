using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace FMO.Plugin;

/// <summary>
/// plugin 格式zip
/// 配置 def.json
/// </summary>
public class PluginDefinition
{
    /// <summary>
    /// 入口文件名
    /// </summary>
    public required string EndPoint { get; set; }

    [JsonIgnore]
    [AllowNull]
    public string Folder { get; internal set; }
}
