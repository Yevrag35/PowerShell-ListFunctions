using ListFunctions.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

#nullable enable

namespace ListFunctions.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when an object cannot be converted to the specified type during a
    /// language function operation.
    /// </summary>
    /// <remarks>This exception provides detailed error information, including the value that failed to
    /// convert, its original type, the target type, and the underlying error message. It is typically used to wrap a
    /// PowerShell invalid cast exception with additional context relevant to language function processing.</remarks>
    public sealed class LFInvalidCastException : PSInvalidCastException
    {
        const string MSG_FORMAT = "Cannot convert value \"{0}\" of type \"{1}\" to type \"{2}\".";
        const string ADD_FORMAT = MSG_FORMAT + " Error: {3}";
        static readonly string? _namespace = typeof(LFInvalidCastException).Namespace;

        readonly string? _itemAsStr;
        readonly string _message;
        readonly string? _stackTrace;

        public override string Message => _message;
        public override string? StackTrace => _stackTrace ?? base.StackTrace;
        public override string? Source
        {
            get => base.Source ?? _namespace;
            set => base.Source = value;
        }

        /// <summary>
        /// Initializes a new instance of the LFInvalidCastException class using information from a
        /// PSInvalidCastException, the target type, and the item that failed to convert.
        /// </summary>
        /// <remarks>This constructor preserves error details and context from the original
        /// PSInvalidCastException, including error category information and recommended actions. Use this constructor
        /// to wrap PowerShell cast exceptions with additional context for error handling or reporting.</remarks>
        /// <param name="inner">The PSInvalidCastException that contains details about the original cast failure. Cannot be null.</param>
        /// <param name="convertingTo">The type to which the conversion was attempted. Cannot be null.</param>
        /// <param name="item">The object that could not be converted. May be null if the original cast did not involve a specific item.</param>
        public LFInvalidCastException(PSInvalidCastException inner, Type convertingTo, object? item)
            : base(string.Empty, inner.InnerException)
        {
            _message = FormatMessage(inner, item, convertingTo, out _itemAsStr, out string? itemType);
            _stackTrace = inner.StackTrace;

            this.ErrorRecord.CategoryInfo.Reason = inner.ErrorRecord.FullyQualifiedErrorId;
            this.ErrorRecord.CategoryInfo.TargetName = _itemAsStr ?? string.Empty;
            this.ErrorRecord.CategoryInfo.TargetType = itemType ?? string.Empty;
            this.ErrorRecord.ErrorDetails = new ErrorDetails(_message)
            {
                RecommendedAction = ConstructRecommendedAction(convertingTo),
            };

            this.HResult = inner.HResult;
            this.HelpLink = inner.HelpLink;
        }

        const string RECOM_ACT = "Validate that the object being passed can be converted to \"{0}\".";
        private static string ConstructRecommendedAction(Type convertingTo)
        {
            return string.Format(RECOM_ACT, convertingTo.GetTypeName());
        }

        private static string FormatMessage(Exception inner, object? item, Type convertingTo, out string? itemAsStr, out string? itemType)
        {
            itemType = item?.GetType().GetTypeName();
            string type = itemType ?? "null";
            itemAsStr = item?.ToString();

            Exception baseEx = inner.GetBaseException();
            baseEx.Source = itemAsStr;

            return string.Format(ADD_FORMAT, itemAsStr, type, convertingTo.GetTypeName(), baseEx.Message);
        }
    }
}
