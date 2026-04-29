
namespace FMO.ESigning;

public static class SigningGalley
{
    private static Dictionary<string, ISigning> _signs = [];

    private static Dictionary<string, ESignViewModelBase> _viewModels = [];


    public static ESignViewModelBase[] ViewModels => _viewModels.Values.ToArray();

    public static ESigningWorker Worker { get; } = new();

    public static IEnumerable<ISigning> Platforms => _signs.Values;


    public static ISigning? FindByIdentifier(string? identifier) => _signs.TryGetValue(identifier ?? "", out var signing) ? signing : null;

    public static void Register(ISigning signing, ESignViewModelBase viewModel)
    {
        _signs[signing.Id] = signing;
        _viewModels[signing.Id] = viewModel;
        viewModel.Load();
    }


    /// <summary>
    /// 放到首页中
    /// </summary>
    public static void Initialize()
    {
        //Register(new MeiShiAssit(), new MeiShiViewModel());

#if !DEBUG 
        Worker.Start();
#endif
    }
}
