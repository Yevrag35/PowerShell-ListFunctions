using System;
using System.Collections;

namespace ListFunctions.Modern
{
    public interface IEqualityBlock : IEqualityComparer
    {
        Type ChecksType { get; }
    }
}
