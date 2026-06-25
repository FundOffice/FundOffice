using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Vetting.ViewModel;

public partial class FinalFileViewModel : ObservableObject
{
    public required string FileName { get; set; }
    public required string AbsolutePath { get; set; }
    public string VettingId { get; set; } = "";

    [SetsRequiredMembers]
    public FinalFileViewModel(FileInfo fileInfo, string vettingId)
    {
        FileName = fileInfo.Name;
        AbsolutePath = fileInfo.FullName;
        VettingId = vettingId;
    }

    [RelayCommand]
    private void OpenFile() => Process.Start(new ProcessStartInfo(AbsolutePath) { UseShellExecute = true });
}
