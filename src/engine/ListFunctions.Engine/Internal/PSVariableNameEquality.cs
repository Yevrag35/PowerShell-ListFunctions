using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Internal
{
    internal sealed class PSVariableNameEquality : IEqualityComparer<PSVariable>
    {
        public bool Equals(PSVariable? x, PSVariable? y)
        {
            if (ReferenceEquals(x, y) || (x is null && y is null))
            {
                return true;
            }

            return StringComparer.InvariantCultureIgnoreCase.Equals(x?.Name, y?.Name);
        }

        public int GetHashCode(PSVariable obj)
        {
            Guard.NotNull(obj, nameof(obj));
            return StringComparer.InvariantCultureIgnoreCase.GetHashCode(obj.Name);
        }
    }
}
