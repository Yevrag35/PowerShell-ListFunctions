using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace ListFunctions.Modern
{
    public interface IHashBlock
    {
        int GetHashCode([DisallowNull] object obj, IEnumerable<PSVariable>? additionalVariables);
    }

    public interface IHashCodeBlock
    {
        Type HashesType { get; }
        int GetHashCode(object obj, IEnumerable<PSVariable> additionalVariables);
    }
}
