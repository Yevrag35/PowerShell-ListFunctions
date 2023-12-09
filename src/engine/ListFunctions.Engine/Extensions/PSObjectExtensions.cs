using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;

namespace ListFunctions.Extensions
{
    public static class PSObjectExtensions
    {
        public static object? GetBaseObject(this object? obj)
        {
            if (obj is null || !(obj is PSObject mshObj))
            {
                return obj;
            }

            if (mshObj == AutomationNull.Value)
            {
                return null;
            }

            return PSObject.AsPSObject(mshObj.ImmediateBaseObject).ImmediateBaseObject;
        }
        public static bool TryGetBaseObject(this object? obj, [NotNullWhen(true)] out object? result)
        {
            result = GetBaseObject(obj);
            return !(result is null);
        }

        [Obsolete("Use 'GetBaseObject' extension method.")]
        public static object? AsObject(this PSObject? pso)
        {
            if (pso is null)
            {
                return null;
            }

            return PSObject.AsPSObject(pso.ImmediateBaseObject).ImmediateBaseObject;
        }

        [Obsolete("Use 'TryGetBaseObject' extension method.")]
        public static bool TryAsObject(this PSObject? pso, [MaybeNullWhen(true)] out object result)
        {
            result = AsObject(pso);
            return !(result is PSObject);
        }
    }
}
