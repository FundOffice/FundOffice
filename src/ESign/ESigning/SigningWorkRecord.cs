namespace FMO.ESigning;

public class SigningWorkRecord
{
    public required string Id { get; set; }


    public DateTime QueryCustomerTime { get; set; }

    public DateTime QueryQualificationTime { get; set; }


    public DateTime QueryOrderTime { get; set; }


}