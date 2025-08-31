using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.Serialization;

namespace ListFunctions.Modern.Exceptions
{
#if !NET8_0_OR_GREATER
    [Serializable]
#endif
    public abstract class ScriptBlockInvocationException : RuntimeException
    {
        const string DEF_MSG_ONLY_FORMAT = "{0}.";
        const string DEF_MSG_POINT_INNER_FORMAT = "{0} --> {1}";
        const string INV_MSG_FORMAT = "Invoking \"{0}\" threw '{1}'.";
        static readonly Lazy<PropertyInfo?> _statementProp = new Lazy<PropertyInfo?>(GetStatementProperty);

        public object? Offender { get; }
        protected abstract Type OffenderType { get; }
        public string Script { get; }
        public IReadOnlyDictionary<string, object?> Variables { get; }

        protected ScriptBlockInvocationException(string message, object? offender, string? statement, Exception? innerException, IReadOnlyList<PSVariable>? injectedVariables)
            : base(FormatMessage(message, innerException, statement), innerException)
        {
            this.Offender = offender;
            this.Script = statement ?? string.Empty;
            this.Variables = ToDictionary(injectedVariables);
        }
        protected ScriptBlockInvocationException(string message, object? offender, string? statement, RuntimeException innerRuntime, IReadOnlyList<PSVariable>? injectedVariables)
            : base(FormatMessage(message, innerRuntime, statement), innerRuntime, innerRuntime.ErrorRecord)
        {
            this.HelpLink = innerRuntime.HelpLink;
            this.HResult = innerRuntime.HResult;
            this.Offender = offender;
            this.Script = statement ?? string.Empty;
            this.Source = innerRuntime.Source;
            this.Variables = ToDictionary(injectedVariables);
            this.WasThrownFromThrowStatement = innerRuntime.WasThrownFromThrowStatement;
        }
#if !NET8_0_OR_GREATER
        protected ScriptBlockInvocationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Guard.NotNull(info, nameof(info));
            this.Offender = info.GetValue(nameof(this.Offender), typeof(object));
            this.Script = info.GetString(nameof(this.Script)) ?? string.Empty;
            this.Variables = (IReadOnlyDictionary<string, object?>)info.GetValue(nameof(this.Variables), typeof(ReadOnlyDictionary<string, object?>));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Guard.NotNull(info, nameof(info));
            info.AddValue(nameof(this.Offender), this.Offender, this.OffenderType);
            info.AddValue(nameof(this.Variables), this.Variables, typeof(ReadOnlyDictionary<string, object?>));
            info.AddValue(nameof(this.Script), this.Script);

            base.GetObjectData(info, context);
        }

#endif

        [return: NotNullIfNotNull(nameof(obj))]
        protected static object? CopyObject(object? obj)
        {
            switch (obj)
            {
                case ICloneable cloneable:
                    return cloneable.Clone();

                case PSObject pso:
                    return pso.Copy();

                default:
                    return obj;
            }
        }
        private static string FormatMessage(string message, Exception? inner, string? statement)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                return inner is null
                    ? string.Format(DEF_MSG_ONLY_FORMAT, message)
                    : string.Format(DEF_MSG_POINT_INNER_FORMAT, message, inner.Message);
            }
            else if (inner is null)
            {
                return string.Format(DEF_MSG_ONLY_FORMAT, message);
            }

            return string.Format(DEF_MSG_POINT_INNER_FORMAT, message,
                string.Format(INV_MSG_FORMAT, statement, inner.Message));
        }
        public static string GetScriptStatement(InvocationInfo info)
        {
            if (_statementProp.Value is null)
            {
                return info.Line;
            }

            try
            {
                string? statement = _statementProp.Value.GetValue(info) as string;
                return statement ?? info.Line;
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
                return info.Line;
            }
        }
        private static PropertyInfo? GetStatementProperty()
        {
            try
            {
                return typeof(InvocationInfo).GetProperty("Statement",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
                return null;
            }
        }

        private static void AddToDict(ref Dictionary<string, object?> dict, PSVariable? variable)
        {
            if (variable is null)
            {
                return;
            }

            object? val = CopyObject(variable.Value);

#if NET5_0_OR_GREATER
            _ = dict.TryAdd(variable.Name, val);
#else
            if (!dict.ContainsKey(variable.Name))
            {
                dict.Add(variable.Name, val);
            }
#endif
        }
        private static IReadOnlyDictionary<string, object?> ToDictionary(IReadOnlyList<PSVariable>? variables)
        {
            if (variables is null || variables.Count <= 0)
            {
                return Empty<string, object?>.Dictionary;
            }

            var dict = new Dictionary<string, object?>(variables.Count, StringComparer.InvariantCultureIgnoreCase);
            foreach (PSVariable v in variables)
            {
                AddToDict(ref dict, v);
            }

            return new ReadOnlyDictionary<string, object?>(dict);
        }
    }
}
