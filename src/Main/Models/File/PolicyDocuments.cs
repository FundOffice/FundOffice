using System.Text;

namespace FMO.Models;

public class PolicyDocument : MultiDualFile
{
    public int Id => ComputeStableHash16(Label);


    private const uint FnvPrime = 16777619;
    private const uint FnvOffsetBasis = 2166136261;
    private static ushort ComputeStableHash16(string? input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        // 使用 UTF-8 编码确保跨平台一致性
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        uint hash = FnvOffsetBasis;

        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        // 折叠 32 位哈希到 16 位 (XOR folding)
        return (ushort)((hash >> 16) ^ (hash & 0xFFFF));
    }
}


