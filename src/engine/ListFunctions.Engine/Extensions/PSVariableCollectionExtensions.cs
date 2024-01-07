using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;

namespace ListFunctions.Extensions
{
    public static class PSVariableCollectionExtensions
    {
        [return: NotNullIfNotNull(nameof(defaultIfNull))]
        public static T GetFirstValue<T>(this Collection<PSObject>? collection, Func<object, T> convert, T defaultIfNull = default!)
        {
            if (collection is null || collection.Count <= 0 || !collection[0].TryGetBaseObject(out object? o))
            {
                return defaultIfNull;
            }

            try
            {
                return convert(o);
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
                return defaultIfNull;
            }
        }

        [return: MaybeNull]
        public static T GetLastValue<T>(this Collection<PSObject>? collection, Func<object, T> convert)
        {
            if (collection is null || collection.Count <= 0)
                return default;

            object? last = collection.LastOrDefault()?.ImmediateBaseObject;
            if (last is null)
                return default;

            try
            {
                return convert(PSObject.AsPSObject(last).ImmediateBaseObject);
            }
            catch
            {
                return default;
            }
        }
    }
}
