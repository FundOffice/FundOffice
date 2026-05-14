using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMO.Models;

public enum VerifyType { Captcha, SMS };

public class VerifyMessage
{
    public long Id { get; } = DateTime.Now.Ticks;


    public string? Title { get; set; }

    public VerifyType Type { get; set; }

    public byte[]? Image { get; set; }


    public string? Code { get; set; }

    public int TimeOut { get; set; }


    public AutoResetEvent Waiter { get; } = new(false);
}

public record VerifyResultMessage(long Id, bool IsSuccessed, string? Error);
