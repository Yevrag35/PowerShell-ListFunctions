using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ListFunctions.Extensions
{
    public static class TypeExtensions
    {
        [DebuggerStepThrough]
        [return: NotNullIfNotNull(nameof(type))]
        public static string? GetTypeName(this Type? type)
        {
            return type?.FullName ?? type?.Name;
        }
    }
}
