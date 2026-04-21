namespace FMO.Models;

/// <summary>
/// 有效期
/// </summary>
public struct DateEfficient
{
    public DateOnly? Begin { get; set; }

    public DateOnly? End { get; set; }

    /// <summary>
    /// 长期有限
    /// </summary>
    public bool LongTerm { get; set; }


    public override string ToString()
    {
        if (Begin >= End) return string.Empty;

        return $"{Begin?.ToString("yyyy.MM.dd")}-{(LongTerm ? "长期" : End?.ToString("yyyy.MM.dd"))}";
    }
}