using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;
using System.Text;

#nullable enable

namespace ListFunctions.Extensions
{
    /// <summary>
    /// Provides extension methods for cloning objects and arrays, including support for common PowerShell object types.
    /// </summary>
    /// <remarks>These methods enable convenient cloning of objects and arrays, handling PowerShell-specific
    /// types such as PSObject, PSCustomObject, and PSMemberInfo. They are intended to simplify scenarios where deep or
    /// shallow copies are required, particularly when working with PowerShell automation objects.</remarks>
    public static class ObjectCloningExtensions
    {
        /// <summary>
        /// Creates a copy of the specified object if it supports cloning; otherwise, returns the original object.
        /// </summary>
        /// <remarks>This method checks for common PowerShell object types and uses their respective copy
        /// mechanisms. If the object does not support cloning, the original reference is returned.</remarks>
        /// <param name="obj">The object to clone. Can be null.</param>
        /// <returns>A new object that is a copy of <paramref name="obj"/> if it implements <see cref="ICloneable"/>, <see
        /// cref="System.Management.Automation.PSObject"/>, <see cref="System.Management.Automation.PSCustomObject"/>,
        /// or <see cref="System.Management.Automation.PSMemberInfo"/>; otherwise, returns <paramref name="obj"/>
        /// itself. Returns null if <paramref name="obj"/> is null.</returns>
        [return: NotNullIfNotNull(nameof(obj))]
        internal static object? CloneIf(this object? obj)
        {
            return obj switch
            {
                ICloneable cloneable => cloneable.Clone(),
                PSObject pso => pso.Copy(),
                PSCustomObject customObj => PSObject.AsPSObject(customObj).Copy(),
                PSMemberInfo member => member.Copy(),
                _ => obj
            };
        }

        /// <summary>
        /// Creates a deep copy of the specified array, cloning each element.
        /// </summary>
        /// <remarks>Each element in the returned array is a deep clone of the corresponding element in
        /// the source array. The cloning behavior depends on the type of each element; reference types are recursively
        /// cloned where possible, while value types are copied by value.</remarks>
        /// <param name="source">The array of objects to clone. Can be null or empty.</param>
        /// <returns>A new array containing deep copies of the elements in the source array. Returns an empty array if the source
        /// is null or empty.</returns>
        public static object?[] DeepClone(this object?[]? source)
        {
            if (source is null || source.Length == 0)
                return [];

            return Array.ConvertAll(source, CloneIf);
        }
    }
}

