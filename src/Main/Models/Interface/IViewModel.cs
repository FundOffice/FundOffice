using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;

public interface IViewModel<T>
{

    
    T Build();
}
