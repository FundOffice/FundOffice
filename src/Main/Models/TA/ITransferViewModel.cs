using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;


public interface ITransferViewModel
{
    string FundName { get; }

    string InvestorName { get; }
}

public interface IHasOrderViewModel
{
    int OrderId { get; }

    bool HasOrder { get; }

    bool LackOrder { get; }

    bool IsSameManager { get; }

    bool IsOrderRequired { get; }

    bool IsLiquidating { get; }

    public bool IsBuy();

    public bool IsSell();
}
