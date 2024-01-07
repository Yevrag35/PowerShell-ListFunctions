using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Modern
{
    public interface IHashCodeBlock
    {
        Type HashesType { get; }
        int GetHashCode(object obj, IEnumerable<PSVariable> additionalVariables);
    }
}
