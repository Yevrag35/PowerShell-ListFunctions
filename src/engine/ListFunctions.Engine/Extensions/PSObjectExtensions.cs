using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ListFunctions.Extensions
{
    public static class PSObjectExtensions
    {
        public static object? GetBaseObject(this object? obj)
        {
            if (obj is not PSObject mshObj)
            {
                return obj;
            }

            if (mshObj == AutomationNull.Value)
                return null;
            if (Marshal.IsImmediateBaseObjectIsEmpty(mshObj))
            {
                return obj;
            }

            object returnValue;
            do
            {
                returnValue = Marshal.GetRawImmediateBaseObject(mshObj)!;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                mshObj = returnValue as PSObject;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            } while ((mshObj is not null) && !Marshal.IsImmediateBaseObjectIsEmpty(mshObj));

            return returnValue;
        }
        public static bool TryGetBaseObject(this object? obj, [NotNullWhen(true)] out object? result)
        {
            result = GetBaseObject(obj);
            return result is not null;
        }

        private static class Marshal
        {
#if NET9_0_OR_GREATER
            internal static object? GetRawImmediateBaseObject(PSObject psObject)
            {
                return GetImmediateBaseObject(psObject);
            }
            internal static bool IsImmediateBaseObjectIsEmpty(PSObject psObject)
            {
                return ImmediateBaseObjectIsEmpty(psObject);
            }

            [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_immediateBaseObject")]
            private static extern ref object? GetImmediateBaseObject(PSObject psObject);

            [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ImmediateBaseObjectIsEmpty")]
            private static extern bool ImmediateBaseObjectIsEmpty(PSObject psObject);

#else
            private static readonly FieldInfo _immediateBaseObjectField;
            private static readonly FieldInfo _immediateBaseObjectIsEmptyField;
            static Marshal()
            {
                _immediateBaseObjectIsEmptyField = typeof(PSObject).GetField("immediateBaseObjectIsEmpty", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("Could not find field 'immediateBaseObjectIsEmpty' on type 'PSObject'.");

                _immediateBaseObjectField = typeof(PSObject).GetField("immediateBaseObject", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("Could not find field 'immediateBaseObject' on type 'PSObject'.");
            }

            internal static object? GetRawImmediateBaseObject(PSObject psObject)
            {
                return _immediateBaseObjectField.GetValue(psObject);
            }

            internal static bool IsImmediateBaseObjectIsEmpty(PSObject psObject)
            {
                return _immediateBaseObjectIsEmptyField.GetValue(psObject) as bool? ?? false;
            }
#endif
        }
    }
}
