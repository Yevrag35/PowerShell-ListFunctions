using ListFunctions.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Exceptions
{
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

#if NET6_0_OR_GREATER
        const string RECOM_ACT = "Validate that the object being passed can be converted to \"";
        private static string ConstructRecommendedAction(Type convertingTo)
        {
            string name = convertingTo.GetTypeName();
            int length = RECOM_ACT.Length + name.Length + 2;

            return string.Create(length, name, (chars, state) =>
            {
                RECOM_ACT.AsSpan().CopyTo(chars);
                int pos = RECOM_ACT.Length;

                state.AsSpan().CopyTo(chars.Slice(pos));
                pos += state.Length;

                chars[pos++] = '"';
                chars[pos++] = '.';
            });
        }
#else
        const string RECOM_ACT = "Validate that the object being passed can be converted to \"{0}\".";
        private static string ConstructRecommendedAction(Type convertingTo)
        {
            return string.Format(RECOM_ACT, convertingTo.GetTypeName());
        }
#endif  

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
