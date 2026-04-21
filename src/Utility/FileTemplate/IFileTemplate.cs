using FMO.Models;

namespace FMO.TPL;

public interface IFileTemplate
{
    public object Data { get; }

    public Stream TemplateStream { get; }

}

public interface IDataProvider
{
    public T GetOne<T>(Func<T, bool>? filter = null);

    public T[]? GetMany<T>(Func<T, bool>? filter = null);
}



public class TaExport : IFileTemplate
{
    public TaExport(IDataProvider provider)
    {
        var ta = provider.GetMany<TransferRecord>();
        Data = new
        {
            ii = ta
        };

    }

    public object Data { get; set; }

    public Stream TemplateStream => throw new NotImplementedException();



}



