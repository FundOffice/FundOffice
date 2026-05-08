using FMO.Disclosure;
using FMO.Models;
using FMO.Shared;
using FMO.TPL;
using System.Collections.ObjectModel;

namespace FMO;


public class TemporaryNoticeViewModel
{

    public long Id { get; set; }

    public string? Name { get; set; }

    public virtual string? DisplayName => Name;

    public SimpleFileViewModel? Word { get; set; }

    public SimpleFileViewModel? Pdf { get; set; }


    public ObservableCollection<DisclosureRunViewModel>? Runs { get; init; }


    public TemporaryNoticeViewModel(IDisclosureNotice notice, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs)
    {
        var data = from workflow in workflows
                       // 左连接：以 workflow 为主体，匹配对应的实例
                   join instance in runs on workflow.Id equals instance.WorkflowId into instanceGroup
                   from instance in instanceGroup.DefaultIfEmpty()
                       // 构建 ViewModel
                   select new DisclosureRunViewModel(notice, workflow, instance);

        Runs = new(data);
    }

    public TemporaryNoticeViewModel()
    {
    }

    public static TemporaryNoticeViewModel Create(IDisclosureNotice notice, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs)
    {
        return notice switch
        {
            TemporaryOpenNotice t => new TemporaryOpenNoticeViewModel(t, workflows, runs),
            HugeRedemptionNotice t =>  new HugeRedemptionNoticeViewModel(t, workflows, runs),
            _ => new TemporaryNoticeViewModel(notice, workflows, runs)
        };
    }
}
public class TemporaryFundNoticeViewModel : TemporaryNoticeViewModel
{

    public string? FundName { get; set; }

    public override string? DisplayName => Fund.GetDefaultShortName(FundName);


    public TemporaryFundNoticeViewModel(IDisclosureNotice notice, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs) : base(notice, workflows, runs)
    {

    }

    public TemporaryFundNoticeViewModel()
    {
    }

    //public TemporaryNoticeViewModel(ITemporaryDisclosureNotice notice)
    //{
    //    Word = new(notice.Word);
    //    Pdf = new(notice.Pdf);
    //}
}


[AutoViewModel(typeof(TemporaryOpenNotice))]
public partial class TemporaryOpenNoticeViewModel : TemporaryFundNoticeViewModel
{

    public TemporaryOpenNoticeViewModel(TemporaryOpenNotice report, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs) : this(report)
    {

    }

    public string Allow => AllowPurchase && AllowRedemption ? "申购/赎回" : AllowPurchase ? "申购" : AllowRedemption ? "赎回" : "";
}

[AutoViewModel(typeof(HugeRedemptionNotice))]
public partial class HugeRedemptionNoticeViewModel : TemporaryFundNoticeViewModel
{
    public HugeRedemptionNoticeViewModel(IDisclosureNotice notice, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs) : base(notice, workflows, runs)
    {
        FillBy(notice as HugeRedemptionNotice); 
    }
}