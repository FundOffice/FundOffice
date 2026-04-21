namespace FMO.Plugin;

public interface IPlugin
{
    public string Title { get; }

    public string? Description { get; }

    public Stream? Icon { get; }

    public void OnLoad();


    public void OnUnload();


}
