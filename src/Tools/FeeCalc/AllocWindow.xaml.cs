using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FMO.Models;
using LiteDB;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Data;

namespace FMO.FeeCalc;

/// <summary>
/// Interaction logic for AllocWindow.xaml
/// </summary>
public partial class AllocWindow : Window
{
    public AllocWindow()
    {
        InitializeComponent();
    }
}


public partial class AllocWindowViewModel : ObservableObject
{
    [SetsRequiredMembers]
    public AllocWindowViewModel(LiteDatabase db)
    {
        FeeDB = db;

        Recipients = [.. FeeDB.GetCollection<ProfitAllocation>().Query().Select(x => x.Name).ToEnumerable().Distinct().ToArray()];

        if (!Recipients.Contains("管理人"))
            Recipients.Insert(0, "管理人");

        var list = FeeDB.GetCollection<TransferRecord>().Query().Select(x => x.InvestorId).ToList();

        var dbInvestors = FeeDB.GetCollection<Investor>().Query().Where(Query.In("_id", list.Select(x => new BsonValue(x)))).ToArray();

        var allocs = FeeDB.GetCollection<ProfitAllocation>().Query().ToArray();


        var j = from inv in dbInvestors
                join alloc in allocs on (int)inv.Id equals alloc.TargetId into allocGrp
                let itemAllocs = allocGrp.ToList()
                let invId = (int)inv.Id
                select new InvestorAllocViewModel(FeeDB, Recipients, invId, inv.Name ?? "", inv.Identity?.Id ?? "",
                itemAllocs.Any() ? [.. itemAllocs.Select(x => new ProfitAllocationViewModel(x))] : [new(invId, "管理人", 100m)]);

        Investors = [.. j];

        InvestorSource = new CollectionViewSource();
        InvestorSource.Source = Investors;
        InvestorSource.Filter += InvestorSource_Filter;
    }

    private void InvestorSource_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = string.IsNullOrWhiteSpace(SearchInvestor) || (e.Item as InvestorAllocViewModel)!.Name.Contains(SearchInvestor);
    }

    public required LiteDatabase FeeDB { get; set; }



    public ObservableCollection<string> Recipients { get; }

    [ObservableProperty]
    public partial string? SearchInvestor { get; set; }


    public InvestorAllocViewModel[] Investors { get; set; }

    public CollectionViewSource InvestorSource { get; }



    [ObservableProperty]
    public partial string? NewOwner { get; set; }





    [RelayCommand]
    public void AddOwner()
    {
        if (string.IsNullOrWhiteSpace(NewOwner)) return;

        if (Recipients.Contains(NewOwner))
            HandyControl.Controls.Growl.Warning($"已存在{NewOwner}");
        else
            FeeDB.GetCollection<string>("Recipient").Insert(NewOwner);

        NewOwner = null;
    }


    [RelayCommand]
    public void ClearSearch()
    {
        SearchInvestor = null;
    }


    partial void OnSearchInvestorChanged(string? value)
    {
        InvestorSource.View.Refresh();
    }





}

public partial class InvestorAllocViewModel : ObservableObject
{
    public LiteDatabase FeeDB { get; }
    public ObservableCollection<string> Recipients { get; }
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Card { get; set; }


    /// <summary>是否编辑模式</summary>
    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    public required ObservableCollection<ProfitAllocationViewModel> Allocations { get; set; }


    public bool CanConfirm => !Allocations.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Ratio is null or 0) && Allocations.Sum(x => x.Ratio) == 100;


    [SetsRequiredMembers]
    public InvestorAllocViewModel(LiteDatabase feeDB, ObservableCollection<string> recipients, int invId, string v1, string v2, ObservableCollection<ProfitAllocationViewModel> value)
    {
        FeeDB = feeDB;
        Recipients = recipients;
        Id = invId;
        Name = v1;
        Card = v2;
        Allocations = value;
        Allocations.CollectionChanged += Allocations_CollectionChanged;
        foreach (var item in value)
            item.PropertyChanged += (s, e) => ConfirmCommand.NotifyCanExecuteChanged();
    }

    private void Allocations_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (ProfitAllocationViewModel item in e.NewItems)
            {
                item.PropertyChanged += (s, e) => ConfirmCommand.NotifyCanExecuteChanged();
            }
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public void Add()
    {
        if (Allocations.Any(x => string.IsNullOrWhiteSpace(x.Name)))
        {
            HandyControl.Controls.Growl.Warning("还有未设置提成人员名字的项");
            return;
        }

        Allocations.Add(new(Id));
    }

    [RelayCommand]
    public void Delete(ProfitAllocationViewModel vm)
    {
        Allocations.Remove(vm);

        if (Allocations.Count == 0)
            Allocations.Add(new(Id, "管理人", 100));

    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    public void Confirm()
    {        
        FeeDB.GetCollection<ProfitAllocation>().DeleteMany(x => x.TargetId == Id);
        FeeDB.GetCollection<ProfitAllocation>().InsertBulk(Allocations.Select(x => x.Build()));

        foreach (var n in Allocations.Select(x => x.Name!).Except(Recipients))
            Recipients.Add(n);

        IsEditMode = false;
    }
}


public partial class ProfitAllocationViewModel : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial decimal? Ratio { get; set; }

    public ProfitAllocationViewModel(int id)
    {
        Id = id;

    }

    public ProfitAllocationViewModel(ProfitAllocation alloc)
    {
        Id = alloc.TargetId;
        Name = alloc.Name;
        Ratio = alloc.Ratio;
    }

    public ProfitAllocationViewModel(int id, string name, decimal ratio)
    {
        Id = id;
        Name = name;
        Ratio = ratio;
    }

    public ProfitAllocation Build() => new ProfitAllocation(Id, Name!, Ratio ?? 0);

}