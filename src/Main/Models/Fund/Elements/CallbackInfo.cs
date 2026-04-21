namespace FMO.Models;

public class CallbackInfo
{
    public bool IsRequired { get; set; }

    public override string ToString()
    {
        return IsRequired ? "需要回访" : "不适用";
    }
}