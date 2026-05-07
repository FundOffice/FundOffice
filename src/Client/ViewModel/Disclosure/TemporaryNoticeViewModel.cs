using FMO.Disclosure;
using FMO.Models;
using FMO.Shared;
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

    public static TemporaryNoticeViewModel Create(IDisclosureNotice notice, IEnumerable<DisclosureWorkflow> workflows, IEnumerable<DisclosureInstance> runs)
    {
        return notice switch
        {
            TemporaryOpenNotice t => new TemporaryOpenNoticeViewModel(t, workflows, runs),
            _ => new TemporaryNoticeViewModel()
        };
    }
}
public class TemporaryFundNoticeViewModel: TemporaryNoticeViewModel
{

    public string? FundName { get; set; }

    public override string? DisplayName => Fund.GetDefaultShortName(FundName);



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
        var data = from workflow in workflows
                       // 左连接：以 workflow 为主体，匹配对应的实例
                   join instance in runs on workflow.Id equals instance.WorkflowId into instanceGroup
                   from instance in instanceGroup.DefaultIfEmpty()
                       // 构建 ViewModel
                   select new DisclosureRunViewModel(report, workflow, instance);

        Runs = new(data);
    }

    public string Allow => AllowPurchase && AllowRedemption ? "申购/赎回" : AllowPurchase ? "申购" : AllowRedemption ? "赎回" : "";
}