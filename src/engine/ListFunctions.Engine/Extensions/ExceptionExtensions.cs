using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace ListFunctions.Extensions
{
    public static class ExceptionExtensions
    {
        [DebuggerStepThrough]
        [return: NotNullIfNotNull(nameof(exception))]
        public static ErrorRecord? ToRecord(this Exception? exception, ErrorCategory category)
        {
            return ToRecord(exception, category, null);
        }
        [return: NotNullIfNotNull(nameof(exception))]
        public static ErrorRecord? ToRecord(this Exception? exception, ErrorCategory category, object? targetObj)
        {
            if (exception is null)
            {
                return null;
            }

            return new ErrorRecord(exception, exception.GetType().GetTypeName(), category, targetObj);
        }
    }
}
