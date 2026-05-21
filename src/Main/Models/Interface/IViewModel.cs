using System;
using System.Collections.Generic;
using System.Text;

namespace FMO.Models;

public interface IViewModel<T>
{

    
    T Build(); 
}


public interface IViewModel<TValue, TViewModel>: IEquatable<TValue>
{
    static abstract TValue Trans(TViewModel vm);
    static abstract TViewModel Trans(TValue? vm);
}
