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

    #region 重载 == != 实现和default对比
    public static bool operator ==(DateEfficient left, DateEfficient right)
    {
        return left.Begin == right.Begin
            && left.End == right.End
            && left.LongTerm == right.LongTerm;
    }

    public static bool operator !=(DateEfficient left, DateEfficient right)
    {
        return !(left == right);
    }

    // 规范：重写Equals和GetHashCode
    public override bool Equals(object? obj)
    {
        return obj is DateEfficient other && this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Begin, End, LongTerm);
    }
    #endregion

    public override string ToString()
    {
        if (Begin >= End) return string.Empty;

        return $"{Begin?.ToString("yyyy.MM.dd")}-{(LongTerm ? "长期" : End?.ToString("yyyy.MM.dd"))}";
    }
}