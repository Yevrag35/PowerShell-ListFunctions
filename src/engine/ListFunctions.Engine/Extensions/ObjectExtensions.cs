using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;
using System.Text;

#nullable enable

namespace ListFunctions.Extensions
{
    public static class ObjectExtensions
    {
        [return: NotNullIfNotNull(nameof(obj))]
        internal static object? CloneIf(this object? obj)
        {
            if (obj is ICloneable cloneable)
            {
                return cloneable.Clone();
            }
            else if (obj is PSObject pso)
            {
                return pso.Copy();
            }
            else if (obj is PSCustomObject customObj)
            {
                return PSObject.AsPSObject(customObj).Copy();
            }
            else if (obj is PSMemberInfo member)
            {
                return member.Copy();
            }
            else
            {
                return obj;
            }
        }

        public static object?[] DeepClone(this object?[]? source)
        {
            if (source is null || source.Length == 0)
                return Array.Empty<object>();

            return Array.ConvertAll(source, CloneIf);
        }
    }
}

