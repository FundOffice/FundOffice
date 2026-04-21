using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FMO.Models;
using FMO.Utilities;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;

namespace FMO;

public partial class ContractFinalizeFlowViewModel : ContractRelatedFlowViewModel, IElementChangable
{ 
      


    [SetsRequiredMembers]
    public ContractFinalizeFlowViewModel(ContractFinalizeFlow flow) : base(flow)
    {
        Initialized = true;
    }



     


}


public class FundShareChangedMessage
{
    public int FundId { get; set; }

    public int FlowId { get; set; }

}