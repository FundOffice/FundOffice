using FMO.Models;
using System.Diagnostics.CodeAnalysis;

namespace FMO;

/// <summary>
/// 合同定稿
/// </summary>
public partial class ContractFinalizeFlowViewModel : ContractRelatedFlowViewModel, IElementChangable
{



    [SetsRequiredMembers]
    public ContractFinalizeFlowViewModel(ContractFinalizeFlow flow) : base(flow)
    {
        Initialized = true;
    }



    protected override void CanLockOverride(ref bool ok, List<string> err)
    {
        if (!Contract.Exists)
        {
            ok = false;
            err.Add("缺少基金合同");
        }
        if (!RiskDisclosureDocument.Exists)
        {
            ok = false;
            err.Add("缺少风险揭示书");
        }
        if (!CollectionAccount.Exists)
        {
            ok = false;
            err.Add("缺少募集账户函");
        }
        //if (!CollectionAccount.Exists)
        //{
        //    ok = false;
        //    err.Add("缺少托管账户函");
        //} 
    }


}


public class FundShareChangedMessage
{
    public int FundId { get; set; }

    public int FlowId { get; set; }

}