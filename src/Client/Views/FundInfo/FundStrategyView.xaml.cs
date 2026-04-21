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
        st.Start.NewValue = Strategies.Count == 0 ? new DateTime(FundSetupDate, default) : Strategies.LastOrDefault()?.End?.OldValue?.Date switch { DateTime t => t < DateTime.MaxValue.Date ? t.AddDays(1) : t, _ => null }; //?.AddDays(1); 
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
        InvestManagerViewModel st = new(new FundInvestmentManager { FundId = FundId });
        st.IsReadOnly = false;
        st.Start.NewValue = Managers.Count == 0 ? new DateTime(FundSetupDate, default) : Managers.LastOrDefault()?.End?.OldValue?.Date switch { DateTime t => t < DateTime.MaxValue.Date ? t.AddDays(1) : t, _ => null }; 
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

public partial class StrategyInfoViewModel : EditableControlViewModelBase<FundStrategy>
{
    public StrategyInfoViewModel(FundStrategy strategy)
    {
        Id = strategy.Id;
        FundId = strategy.FundId;

        Name = new ChangeableViewModel<FundStrategy, string>
        {
            Label = "策略名称",
            InitFunc = x => x.Name,
            UpdateFunc = (a, b) => a.Name = b,
            ClearFunc = x => x.Name = null
        };
        Name.Init(strategy);

        Start = new ChangeableViewModel<FundStrategy, DateTime?>
        {
            Label = "起始日期",
            InitFunc = x => x.Start == default ? null : new DateTime(x.Start, default),
            UpdateFunc = (a, b) => a.Start = b.HasValue ? DateOnly.FromDateTime(b.Value) : default,
            ClearFunc = x => x.Start = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        Start.Init(strategy);

        End = new ChangeableViewModel<FundStrategy, BooleanDate>
        {
            Label = "终止日期",
            InitFunc = x => x.End == default ? new BooleanDate() : new BooleanDate { Date = new DateTime(x.End, default), IsLongTerm = x.End == DateOnly.MaxValue },
            UpdateFunc = (a, b) => a.End = b is null ? default : b.IsLongTerm ? DateOnly.MaxValue : DateOnly.FromDateTime(b.Date ?? default),
            ClearFunc = x => x.End = default,
            DisplayFunc = x => x?.IsLongTerm ?? false ? "至今" : x?.Date?.ToString("yyyy-MM-dd")
        };
        End.Init(strategy);

        Description = new ChangeableViewModel<FundStrategy, string>
        {
            Label = "策略说明",
            InitFunc = x => x.Description,
            UpdateFunc = (a, b) => a.Description = b,
            ClearFunc = x => x.Description = null
        };
        Description.Init(strategy);
    }

    public ChangeableViewModel<FundStrategy, string> Name { get; }

    public ChangeableViewModel<FundStrategy, DateTime?> Start { get; }

    public ChangeableViewModel<FundStrategy, BooleanDate> End { get; }

    public ChangeableViewModel<FundStrategy, string> Description { get; }


    public int FundId { get; }

    protected override FundStrategy InitNewEntity()
    {
        return new FundStrategy { FundId = FundId };
    }

    protected override void NotifyChanged()
    {
        WeakReferenceMessenger.Default.Send(new FundStrategyChangedMessage(FundId));
    }
    //[RelayCommand]
    //public override void Delete(UnitViewModel unit)
    //{
    //    if (unit is IEntityViewModel<FundStrategy> entity)
    //    {
    //        using var db = DbHelper.Base();
    //        var v = db.GetCollection<FundStrategy>().FindById(Id);

    //        if (v is not null)
    //        {
    //            entity.RemoveValue(v);
    //            entity.Init(v);
    //            db.GetCollection<FundStrategy>().Upsert(v);

    //            WeakReferenceMessenger.Default.Send(v);
    //        }
    //    }
    //}


    //[RelayCommand]
    //public override void Modify(UnitViewModel unit)
    //{
    //    if (unit is IEntityViewModel<FundStrategy> property)
    //    {

    //        using var db = DbHelper.Base();
    //        var v = db.GetCollection<FundStrategy>().FindById(Id) ?? new();

    //        if (v is not null)
    //            property.UpdateEntity(v);

    //        if (v is not null)
    //        {
    //            db.GetCollection<FundStrategy>().Upsert(v);
    //            if (Id == 0) Id = v.Id;

    //            //WeakReferenceMessenger.Default.Send(v);
    //        }
    //    }
    //    unit.Apply();
    //}
}


public partial class InvestManagerViewModel : EditableControlViewModelBase<FundInvestmentManager>
{
    public InvestManagerViewModel(FundInvestmentManager value)
    {
        Id = value.Id;
        FundId = value.FundId;

        using var db = DbHelper.Base();
        var managers = db.GetCollection<Participant>().FindAll().ToArray().Where(x => x.Role.HasFlag(PersonRole.InvestmentManager));
        Managers = new(managers.Select(x=>new PersonInfo(x.Id, x.Name!)));


        Person = new ChangeableViewModel<FundInvestmentManager, PersonInfo>
        {
            Label = "投资经理",
            InitFunc = x => new PersonInfo(x.PersonId, x.Name!),
            UpdateFunc = (a, b) => { a.Name = b!.Name; a.PersonId = b.Id; },
            ClearFunc = x => x.Name = null,
            DisplayFunc = x=>x?.Name
        };
        Person.Init(value);




        Start = new ChangeableViewModel<FundInvestmentManager, DateTime?>
        {
            Label = "起始日期",
            InitFunc = x => x.Start == default ? null : new DateTime(x.Start, default),
            UpdateFunc = (a, b) => a.Start = b.HasValue ? DateOnly.FromDateTime(b.Value) : default,
            ClearFunc = x => x.Start = default,
            DisplayFunc = x => x?.ToString("yyyy-MM-dd")
        };
        Start.Init(value);

        End = new ChangeableViewModel<FundInvestmentManager, BooleanDate>
        {
            Label = "终止日期",
            InitFunc = x => x.End == default ? new BooleanDate() : new BooleanDate { Date = new DateTime(x.End, default), IsLongTerm = x.End == DateOnly.MaxValue },
            UpdateFunc = (a, b) => a.End = b is null ? default : b.IsLongTerm ? DateOnly.MaxValue : DateOnly.FromDateTime(b.Date ?? default),
            ClearFunc = x => x.End = default,
            DisplayFunc = x => x?.IsLongTerm ?? false ? "至今" : x?.Date?.ToString("yyyy-MM-dd")
        };
        End.Init(value);

        Profile = new ChangeableViewModel<FundInvestmentManager, string>
        {
            Label = "简介",
            InitFunc = x => x.Profile,
            UpdateFunc = (a, b) => a.Profile = b,
            ClearFunc = x => x.Profile = null
        };
        Profile.Init(value);
    }


    [ObservableProperty]
    public partial PersonInfo? InvestManager { get; set; }

    public ObservableCollection<PersonInfo> Managers { get; set; }

    public ChangeableViewModel<FundInvestmentManager, PersonInfo> Person { get; }

    public ChangeableViewModel<FundInvestmentManager, DateTime?> Start { get; }

    public ChangeableViewModel<FundInvestmentManager, BooleanDate> End { get; }

    public ChangeableViewModel<FundInvestmentManager, string> Profile { get; }


    public int FundId { get; }

    protected override FundInvestmentManager InitNewEntity()
    {
        return new FundInvestmentManager { FundId = FundId };
    }

    protected override void NotifyChanged()
    {
        WeakReferenceMessenger.Default.Send(new FundStrategyChangedMessage(FundId));
    }
     

    public record PersonInfo(int Id, string Name);
}