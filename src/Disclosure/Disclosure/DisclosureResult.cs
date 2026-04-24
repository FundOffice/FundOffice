using System.Runtime.CompilerServices;
using System.Threading.Channels;


[assembly: InternalsVisibleTo("Disclosure.UI")]

namespace FMO.Disclosure;



public class DisclosureResult
{
    /// <summary>
    /// = DisclosureInstance.Id
    /// </summary>
    public required string Id { get; set; }

    public DisclosureStatus Status { get; internal set; }

    public DateTime StartedTime { get; internal set; }
     

    public DateTime CompletedTime { get; internal set; }


    public string? Error { get; set; }
}

