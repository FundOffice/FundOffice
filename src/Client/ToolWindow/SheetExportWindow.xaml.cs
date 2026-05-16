using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Logging;
using FMO.Models;
using FMO.TPL;
using FMO.Utilities;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Utilities;

namespace FMO;

/// <summary>
/// SheetExportWindow.xaml 的交互逻辑
/// </summary>
public partial class SheetExportWindow : Window
{
    public SheetExportWindow()
    {
        InitializeComponent();


    }
}


public partial class SheetExportWindowViewModel : ObservableObject
{
    private ExcelTemplate Template;

    private string Id => Template.Meta.Id;

    private ScriptGlobal Global { get; set; } = new();

    public InputViewModel[]? Inputs { get; set; }

    [ObservableProperty]
    public partial InputViewModel? InputContext { get; set; }

    [ObservableProperty]
    public partial string[] FileNames { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateSheetCommand))]
    public partial string? SelectedFile { get; set; } = "默认模板";


    public bool CanGenerate => SelectedFile?.Length > 1 && Inputs?.All(x => x.IsFilled) == true;

    public SheetExportWindowViewModel(ExcelTemplate t)
    {
        Template = t;

        InitInputs();

        var di = new DirectoryInfo(@$"files\tpl\excel\{t.Meta.Id}");
        var files = di.GetFiles("*.xlsx");

        FileNames = files.Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToArray();

        //if (FileNames.Length == 1) SelectedFile = FileNames[0];

    }


    private void InitInputs()
    {
        List<InputViewModel> vm = [];
 
        foreach (var item in Template.Script.Input)
        {
            switch (item)
            {
                case InputFund input:
                    vm.Add(new FundInputViewModel
                    {
                        Title = "选择基金",
                        ChooseFundMode = input.ChooseType switch
                        {
                            ChooseType.Single => SelectionMode.Single,
                            _ => SelectionMode.Multiple
                        }
                    });
                    break;


                case InputDate input:
                    vm.Add(new DateInputViewModel
                    {
                        Title = item.Tilte, 
                        
                    });


                    break;


                default:
                    break;
            }
        }

        vm.ForEach(x => x.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(InputViewModel.IsFilled))
                GenerateSheetCommand.NotifyCanExecuteChanged();
        });

        Inputs = [.. vm];
    }


    [RelayCommand(CanExecute = nameof(CanGenerate))]
    public async Task GenerateSheet()
    {

        try
        {
            var data = await Template.Prepare(Global);
            var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            path = Path.Combine(path, $"{Template.Meta.Name}.xlsx");
            await Template.SaveTo(path, data, @$"files\tpl\excel\{Id}\{SelectedFile}.xlsx");

            Toast.Success("导出成功");
        }
        catch (Exception e)
        {
            LogEx.Error(e);
            Toast.Warning("导出报告失败");
        }
        App.Current.Windows[^1].Close();
    }



    [RelayCommand]
    public void ModifyFile()
    {
        var path = @$"files\tpl\excel\{Id}\{SelectedFile}.xlsx";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true }); 
    }
}


public abstract partial class InputViewModel : ObservableObject
{
    public required string Title { get; set; }

    protected Throttle Throttle { get; } = new(TimeSpan.FromMilliseconds(200));


    public abstract object? Value { get; }

    [ObservableProperty]

    public partial bool IsFilled { get; set; }

 



}

public partial class FundInputViewModel : InputViewModel
{

    public List<FundSelection> Funds { get; set; }

    [ObservableProperty]
    public partial string? SearchKey { get; set; }

    [ObservableProperty]
    public partial SelectionMode ChooseFundMode { get; set; }


    public CollectionViewSource FundSource { get; set; } = new();

    public override object? Value => Funds.Where(x => x.IsSelected).Select(x => x.Fund).ToArray();

    public FundInputViewModel()
    {
        using var db = DbHelper.Base();
        Funds = db.GetCollection<Fund>().FindAll().Select(x => new FundSelection { Fund = x, IsSelected = false }).ToList();

        Funds.ForEach(x => x.PropertyChanged += (s, e) => { if (x.IsSelected) IsFilled = true; });

        FundSource.Source = Funds;
        FundSource.Filter += FundSource_Filter;
    }

    private void FundSource_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = string.IsNullOrWhiteSpace(SearchKey) || (e.Item as FundSelection)!.Fund.Name.Contains(SearchKey);
    }

    partial void OnSearchKeyChanged(string? value)
    {
        FundSource.View.Refresh();
    }


    [RelayCommand]
    public void SelectAllFunds() => Funds?.ToList().ForEach(x => x.Select(true));



    [RelayCommand]
    public void UnselectAllFunds() => Funds?.ToList().ForEach(x => x.Select(false));


    [RelayCommand]
    public void ReverseSelectFunds() => Funds?.ToList().ForEach(x => x.Select(!x.IsSelected));


    public partial class FundSelection : ObservableObject
    {
        public required Fund Fund { get; set; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public void Select(bool selected) => IsSelected = selected;
    }
}

public partial class DateInputViewModel : InputViewModel
{ 

    [ObservableProperty]
    public partial DateTime Selected { get; set; }

    public override object? Value => DateOnly.FromDateTime(Selected);


    partial void OnSelectedChanged(DateTime value)
    {
        if (Selected != default)
            IsFilled = true;
    }
}