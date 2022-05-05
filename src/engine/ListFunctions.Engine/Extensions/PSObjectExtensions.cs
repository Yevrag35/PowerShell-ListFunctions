using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions.Extensions
{
    public static class PSObjectExtensions
    {
        public static object? AsObject(this PSObject? pso)
        {
            if (pso is null)
                return null;

            return PSObject.AsPSObject(pso.ImmediateBaseObject).ImmediateBaseObject;
        }

        public static bool TryAsObject(this PSObject? pso, [MaybeNullWhen(true)] out object result)
        {
            result = AsObject(pso);
            return !(result is PSObject);
        }
    }
}
