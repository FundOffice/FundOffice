using CommunityToolkit.Mvvm.ComponentModel;
using FundOffice.Copilot.Providers;

namespace Vetting.ViewModel;

public partial class VettingParseTaskViewModel : ObservableObject
{

    public ITokenProvider? Provider { get; set; }

    /// <summary>
    /// 显示token使用，先用估算值
    /// </summary>
    [ObservableProperty]
    public partial int Usage { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }
}
