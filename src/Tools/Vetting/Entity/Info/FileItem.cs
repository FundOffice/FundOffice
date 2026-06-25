using CommunityToolkit.Mvvm.ComponentModel;

namespace FundOffice.Vetting.Models.Entities;

/// <summary>
/// 文件列表项
/// </summary>
public partial class FileItem : ObservableObject
{
    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    private string? _fullPath;

    [ObservableProperty]
    private long _size;
}
