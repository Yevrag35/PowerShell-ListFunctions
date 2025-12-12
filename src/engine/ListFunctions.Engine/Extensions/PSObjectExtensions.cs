using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ListFunctions.Extensions
{
    /// <summary>
    /// Provides extension methods for extracting the underlying base object from wrapper objects, such as PSObject
    /// instances, in PowerShell-related scenarios.
    /// </summary>
    /// <remarks>These methods are useful when working with objects that may be wrapped by one or more
    /// PSObject layers, allowing callers to access the original object for further processing. The class is intended
    /// for use in environments where PowerShell objects are encountered, such as automation or scripting
    /// hosts.</remarks>
    public static class PSObjectExtensions
    {
        /// <summary>
        /// Retrieves the underlying base object from a wrapper object, such as a PSObject, by unwrapping all nested
        /// wrappers.
        /// </summary>
        /// <remarks>This method is typically used to obtain the original object that may have been
        /// wrapped by one or more PSObject instances. If the input object is not a recognized wrapper, it is returned
        /// unchanged.</remarks>
        /// <param name="obj">The object from which to extract the base object. This may be a wrapper object or a direct value. Can be
        /// null.</param>
        /// <returns>The innermost base object if the input is a wrapper; otherwise, returns the original object. Returns null if
        /// the input is null.</returns>
        public static object? GetBaseObject(this object? obj)
        {
            if (!TryGetPSObject(obj, out PSObject? mshObj) || Marshal.IsImmediateBaseObjectIsEmpty(mshObj))
                return obj;

            object? returnValue;
            do
            {
                returnValue = Marshal.GetRawImmediateBaseObject(mshObj);
                mshObj = returnValue as PSObject;
            } while ((mshObj is not null) && !Marshal.IsImmediateBaseObjectIsEmpty(mshObj));

            return returnValue;
        }
        /// <summary>
        /// Attempts to retrieve the underlying base object from the specified object.
        /// </summary>
        /// <param name="obj">The object from which to extract the base object. This parameter can be null.</param>
        /// <param name="result">When this method returns, contains the base object if extraction succeeded; otherwise, null. This parameter
        /// is passed uninitialized.</param>
        /// <returns>true if the base object was successfully retrieved; otherwise, false.</returns>
        public static bool TryGetBaseObject([NotNullWhen(true)] this object? obj, [NotNullWhen(true)] out object? result)
        {
            result = GetBaseObject(obj);
            return result is not null;
        }

        private static bool TryGetPSObject(object? obj, [NotNullWhen(true)] out PSObject? mshObj)
        {
            return (mshObj = obj as PSObject) is not null && !mshObj.Equals(AutomationNull.Value);
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
