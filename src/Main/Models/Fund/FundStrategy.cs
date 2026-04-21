using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMO.Models;
 
public class FundStrategy
{
    public int Id { get; set; }


    public int FundId { get; set; }

    public string? Name { get; set; }

    public DateOnly Start { get; set; }

    public DateOnly End { get; set; }

    public string? Description { get; set; }
}
