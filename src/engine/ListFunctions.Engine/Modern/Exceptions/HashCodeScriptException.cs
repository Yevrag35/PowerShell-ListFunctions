using ListFunctions.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Runtime.Serialization;

namespace ListFunctions.Modern.Exceptions
{
#if !NET8_0_OR_GREATER
    [Serializable]
#endif
    public sealed class HashCodeScriptException : ScriptBlockInvocationException
    {
        const string DEF_MSG = "An exception occurred trying to calculate the hash code of a specific object";

        public Type HashCodeBlockType { get; }
        protected override Type OffenderType => this.HashCodeBlockType;

        public HashCodeScriptException(object? offender, string? scriptStatement, Type? blockType, Exception? innerException, IReadOnlyList<PSVariable>? injectedVariables)
            : base(DEF_MSG, offender, scriptStatement, innerException, injectedVariables)
        {
            this.HashCodeBlockType = blockType ?? typeof(object);
        }
        private HashCodeScriptException(object? offender, string? scriptStatement, Type? blockType, RuntimeException runtimeException, IReadOnlyList<PSVariable>? injectedVariables)
            : base(DEF_MSG, offender, scriptStatement, runtimeException, injectedVariables)
        {
            this.HashCodeBlockType = blockType ?? typeof(object);
        }

#if !NET8_0_OR_GREATER
        private HashCodeScriptException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.HashCodeBlockType = (Type)info.GetValue(nameof(this.HashCodeBlockType), typeof(Type));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Guard.NotNull(info, nameof(info));
            info.AddValue(nameof(this.HashCodeBlockType), this.HashCodeBlockType);

            base.GetObjectData(info, context);
        }
#endif

        public static HashCodeScriptException FromBlockException<T>(Exception exception, [MaybeNull] in T obj)
        {
            return FromBlockException(exception, in obj, null);
        }
        public static HashCodeScriptException FromBlockException<T>(Exception exception, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
        {
            return FromBlockException(treatAsNonRuntime: false, exception, in obj, injectedVariables);
        }
        private static HashCodeScriptException FromBlockException<T>(bool treatAsNonRuntime, Exception exception, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
        {
            if (!treatAsNonRuntime && exception is RuntimeException runtime)
            {
                return FromBlockException(runtimeException: runtime, in obj, injectedVariables);
            }

            return new HashCodeScriptException(obj, null, typeof(T), innerException: exception, injectedVariables);
        }
        public static HashCodeScriptException FromBlockException<T>(RuntimeException runtimeException, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
        {
            if (runtimeException.ErrorRecord is null || runtimeException.ErrorRecord.InvocationInfo is null)
            {
                return FromBlockException(treatAsNonRuntime: true, exception: runtimeException, in obj, injectedVariables);
            }

            string statement = GetScriptStatement(runtimeException.ErrorRecord.InvocationInfo);

            return new HashCodeScriptException(obj, statement, typeof(T), runtimeException: runtimeException, injectedVariables);
        }
    }
}
