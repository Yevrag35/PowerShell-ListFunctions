using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Modern
{
    public static class Conversion
    {
        public static bool TryConvert(object? value, Type convertTo, [NotNullWhen(true)] out object? result)
        {
            if (value is null)
            {
                result = null;
                return false;
            }

            bool converted = LanguagePrimitives.TryConvertTo(value, convertTo, out result);
            return converted && !(result is null);
        }
        public static bool TryConvert<T>(object? value, [NotNullWhen(true)] out T result)
        {
            if (value is null)
            {
                result = default!;
                return false;
            }

            bool converted = LanguagePrimitives.TryConvertTo(value, out result);
            return converted && !(result is null);
        }
    }
}
