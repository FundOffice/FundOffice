using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Shared;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace FMO;

/// <summary>
/// FundStrategyView.xaml 的交互逻辑
/// </summary>
public partial class FundStrategyView : UserControl
{
    public FundStrategyView()
    {
        InitializeComponent();
    }
}


public partial class FundStrategyViewModel : ObservableObject
{

    public ObservableCollection<StrategyInfoViewModel> Strategies { get; }

    public ObservableCollection<InvestManagerViewModel> Managers { get; }

    public int FundId { get; }

    public DateOnly FundSetupDate { get; }

    public FundStrategyViewModel(int fundId, DateOnly setupDate)
    {
        using var db = DbHelper.Base();
        var data = db.GetCollection<FundStrategy>().Find(x => x.FundId == fundId).ToArray();

        Strategies = new(data.Select(x => new StrategyInfoViewModel(x)));

        Managers = new(db.GetCollection<FundInvestmentManager>().Find(x => x.FundId == fundId).ToArray().Select(x => new InvestManagerViewModel(x)));


        FundId = fundId;
        FundSetupDate = setupDate;
    }

    [RelayCommand]
    public void AddStrategy()
    {
        var s = Strategies.LastOrDefault();

        if (s is not null && (string.IsNullOrWhiteSpace(s.Name.OldValue) || s.Start.OldValue == default || s.End.OldValue == default))
        {
            HandyControl.Controls.Growl.Warning("请先设置已有的策略");
            return;
        }
        StrategyInfoViewModel st = new(new FundStrategy { FundId = FundId });
        st.IsReadOnly = false;
        st.Start.NewValue = Strategies.Count == 0 ? FundSetupDate : Strategies.LastOrDefault()?.End?.OldValue?.Date switch { DateTime t => DateOnly.FromDateTime(t < DateTime.MaxValue.Date ? t.AddDays(1) : t), _ => null }; //?.AddDays(1); 
        Strategies.Add(st);
    }

    [RelayCommand]
    public void DeleteStrategy(StrategyInfoViewModel v)
    {
        if (HandyControl.Controls.MessageBox.Show("是否确认删除", button: System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
        {
            if (v.Id > 0)
            {
                using var db = DbHelper.Base();
                db.GetCollection<FundStrategy>().Delete(v.Id);
            }
            Strategies.Remove(v);
        }
    }

    [RelayCommand]
    public void AddManager()
    {
        var s = Managers.LastOrDefault();

        if (s is not null && (s.Person.OldValue is null || s.Start.OldValue == default || s.End.OldValue == default))
        {
            HandyControl.Controls.Growl.Warning("请先设置已有的投资经理");
            return;
        }
        InvestManagerViewModel st = new(FundId);
        st.IsReadOnly = false;
        if (Managers.Count == 0)
            st.Start.NewValue = FundSetupDate;
        if (Managers.LastOrDefault() is InvestManagerViewModel last && last.End.OldValue != DateOnly.MaxValue)
            st.Start.NewValue = last.End.OldValue.AddDays(1);
        Managers.Add(st);
    }

    [RelayCommand]
    public void DeleteManager(InvestManagerViewModel v)
    {
        if (HandyControl.Controls.MessageBox.Show("是否确认删除", button: System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes)
        {
            if (v.Id > 0)
            {
                using var db = DbHelper.Base();
                db.GetCollection<FundInvestmentManager>().Delete(v.Id);
            }
            Managers.Remove(v);
        }
    }
}


[EntityModifiable(typeof(FundStrategy))]
public partial class StrategyInfoViewModel : ObservableObject
{
    private readonly FundStrategy _strategy;

    public int Id { get; }

    public StrategyInfoViewModel(FundStrategy strategy)
    {
        Id = strategy.Id;
        FundId = strategy.FundId;
        _strategy = strategy;

        FillBy(_strategy);

        End = new() { NewValue = new(_strategy.End), OldValue = new(_strategy.End) };
        End.Changed += (e) => { _strategy.End = DateOnly.FromDateTime(End.NewValue?.Date ?? default); OnEntityChanged(); };
    }


    public ModifiableViewModel<DateOnly?> Start { get; private set; } = null!;

    public ModifiableViewModel<BooleanDate> End { get; private set; } = null!;



    public int FundId { get; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;

    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<FundStrategy>().Upsert(_strategy);

        WeakReferenceMessenger.Default.Send(new FundStrategyChangedMessage(FundId));
    }

    [RelayCommand]
    protected void Save()
    {
        if (this.Name.CanConfirm)
            Name.Apply();
        if (this.Start.CanConfirm)
            Start.Apply();
        if (this.End.CanConfirm)
            End.Apply();
    }
}

[EntityModifiable(typeof(FundInvestmentManager))]
public partial class InvestManagerViewModel : ObservableObject
{
    private readonly FundInvestmentManager investmentManager;

    public int Id { get; private set; }

    public InvestManagerViewModel(int fundId)
    {
        FundId = fundId;
        investmentManager = new() { Name = "", FundId = fundId };

        FillBy(investmentManager);

        using var db = DbHelper.Base();
        var managers = db.GetCollection<Participant>().FindAll().ToArray().Where(x => x.Role.HasFlag(PersonRole.InvestmentManager));
        Managers = new(managers.Select(x => new PersonInfo(x.Id, x.Name!, x.Profile)));


        Person = new() { NewValue = Managers.FirstOrDefault(x => x.Id == investmentManager.PersonId), OldValue = Managers.FirstOrDefault(x => x.Id == investmentManager.PersonId) };
        Person.Changed += (e) =>
        {
            if (e.NewValue is null) return;
            investmentManager.PersonId = e.NewValue.Id;
            investmentManager.Name = e.NewValue.Name;
            OnEntityChanged();
        };
        Person.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Person.NewValue))
            {
                if (string.IsNullOrWhiteSpace(Profile.NewValue))
                    Profile.NewValue = Person.NewValue?.Profile;
            }
        };
    }

    public InvestManagerViewModel(FundInvestmentManager value)
    {
        Id = value.Id;
        investmentManager = value;
        FundId = value.FundId;

        FillBy(value);

        using var db = DbHelper.Base();
        var managers = db.GetCollection<Participant>().FindAll().ToArray().Where(x => x.Role.HasFlag(PersonRole.InvestmentManager));
        Managers = new(managers.Select(x => new PersonInfo(x.Id, x.Name!, x.Profile)));

        if (string.IsNullOrWhiteSpace(Profile.NewValue) && db.GetCollection<Participant>().FindById(value.PersonId) is Participant pp)
            Profile.NewValue = pp.Profile;

        Person = new() { NewValue = Managers.FirstOrDefault(x => x.Id == investmentManager.PersonId), OldValue = Managers.FirstOrDefault(x => x.Id == investmentManager.PersonId) };
        Person.Changed += (e) =>
        {
            if (e.NewValue is null) return;
            investmentManager.PersonId = e.NewValue.Id;
            OnEntityChanged();
        };

    }


    [ObservableProperty]
    public partial PersonInfo? InvestManager { get; set; }

    public ObservableCollection<PersonInfo> Managers { get; set; }

    public ModifiableViewModel<PersonInfo> Person { get; private set; } = null!;

    public ModifiableViewModel<DateOnly?> Start { get; private set; } = null!;

    public ModifiableViewModel<DateOnly, BooleanDate> End { get; private set; } = null!;


    [ObservableProperty]
    public partial bool IsReadOnly { get; set; } = true;


    public int FundId { get; }


    public partial void OnEntityChanged()
    {
        using var db = DbHelper.Base();
        db.GetCollection<FundInvestmentManager>().Upsert(investmentManager);
        if (Id == 0) Id = investmentManager.Id;
    }



    [RelayCommand]
    protected void Save()
    {
        if (this.Name.CanConfirm)
            Name.Apply();
        if (Person.CanConfirm)
            Person.Apply();
        if (this.Start.CanConfirm)
            Start.Apply();
        if (this.End.CanConfirm)
            End.Apply();
        if (this.Profile.CanConfirm)
            Profile.Apply();
    }

    public class PersonInfo(int Id, string Name, string? Profile) : IEquatable<PersonInfo>
    {
        public int Id { get; } = Id;
        
        public string Name { get; } = Name;

        public string? Profile { get; set; } = Profile;

        public bool Equals(PersonInfo? other) => Id == other?.Id;

        public override string ToString() => Name;


    }


}