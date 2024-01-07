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
    public sealed class EqualityScriptException : ScriptBlockInvocationException
    {
        const string DEF_MSG = "An exception occurred trying to determine the equality between two specific objects";

        public Type EqualityBlockType { get; }
        protected override Type OffenderType => this.EqualityBlockType;

        public EqualityScriptException(object? offender, string? scriptStatement, Type? blockType, Exception? innerException, IReadOnlyList<PSVariable>? injectedVariables)
            : base(DEF_MSG, offender, scriptStatement, innerException, injectedVariables)
        {
            this.EqualityBlockType = blockType ?? typeof(object);
        }
        private EqualityScriptException(object? offender, string? scriptStatement, Type? blockType, RuntimeException runtimeException, IReadOnlyList<PSVariable>? injectedVariables)
            : base(DEF_MSG, offender, scriptStatement, runtimeException, injectedVariables)
        {
            this.EqualityBlockType = blockType ?? typeof(object);
        }

#if !NET8_0_OR_GREATER
        private EqualityScriptException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.EqualityBlockType = (Type)info.GetValue(nameof(this.EqualityBlockType), typeof(Type));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Guard.NotNull(info, nameof(info));
            info.AddValue(nameof(this.EqualityBlockType), this.EqualityBlockType);

            base.GetObjectData(info, context);
        }
#endif

        private static EqualityScriptException FromBlockException<T>(bool treatAsNonRuntime, Exception exception, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
        {
            if (!treatAsNonRuntime && exception is RuntimeException runtime)
            {
                return FromBlockException(runtimeException: runtime, in obj, injectedVariables);
            }

            return new EqualityScriptException(obj, null, typeof(T), innerException: exception, injectedVariables);
        }
        public static EqualityScriptException FromBlockException<T>(Exception exception, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
        {
            return FromBlockException(treatAsNonRuntime: false, exception, in obj, injectedVariables);
        }
        public static EqualityScriptException FromBlockException<T>(RuntimeException runtimeException, [MaybeNull] in T obj, IReadOnlyList<PSVariable>? injectedVariables)
        {
            if (runtimeException.ErrorRecord is null || runtimeException.ErrorRecord.InvocationInfo is null)
            {
                return FromBlockException(treatAsNonRuntime: true, exception: runtimeException, in obj, injectedVariables);
            }

            string statement = GetScriptStatement(runtimeException.ErrorRecord.InvocationInfo);

            return new EqualityScriptException(obj, statement, typeof(T), runtimeException: runtimeException, injectedVariables);
        }
    }
}
