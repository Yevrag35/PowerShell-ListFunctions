using ListFunctions.Internal;
using ListFunctions.Modern.Exceptions;
using ListFunctions.Modern.Variables;
using MG.Collections;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Modern
{
    public static class HashCodeBlock
    {
        public static IHashCodeBlock CreateBlock(Type type, ScriptBlock scriptBlock)
        {
            MethodInfo genMethod = _genMeth.MakeGenericMethod(type);
            object[] args = new object[] { scriptBlock };

            return (IHashCodeBlock)genMethod.Invoke(null, args);
        }

        static readonly MethodInfo _genMeth = typeof(HashCodeBlock)
            .GetMethod(nameof(CreateGenericBlock), BindingFlags.Static | BindingFlags.NonPublic);
        private static IHashCodeBlock CreateGenericBlock<T>(ScriptBlock scriptBlock)
        {
            return new HashCodeBlock<T>(scriptBlock);
        }
    }
    public sealed class HashCodeBlock<T> : ComparingBase<T>, IHashCodeBlock
    {
        readonly PSThisVariable<T> _thisVar;
        readonly List<PSVariable> _varList;
        readonly Type _hashesType;

        Type IHashCodeBlock.HashesType => _hashesType;

        public HashCodeBlock(ScriptBlock scriptBlock)
            : base(scriptBlock, preValidated: false)
        {
            _hashesType = typeof(T);
            _thisVar = new PSThisVariable<T>();
            _varList = new List<PSVariable>(1);
        }

        public int GetHashCode(T obj, IEnumerable<PSVariable> additionalVariables)
        {
            if (obj is null)
            {
                var argNull = new ArgumentNullException(nameof(obj));
                throw HashCodeScriptException.FromBlockException(argNull, in obj);
            }

            var list = this.SetContextVariables(obj, additionalVariables);

            if (!this.Script.TryInvokeWithContext(list, x => Convert.ToInt32(x), out int hashCode, out Exception? ex))
            {
                throw HashCodeScriptException.FromBlockException(ex, in obj, this.SetContextVariables(obj, additionalVariables));
            }

            return hashCode;
        }

        private List<PSVariable> SetContextVariables(T obj, IEnumerable<PSVariable> additionalVariables)
        {
            _varList.Clear();
            _thisVar.AddToVarList(obj, _varList);
            _varList.AddRange(additionalVariables);

            return _varList;
        }

        int IHashCodeBlock.GetHashCode(object obj, IEnumerable<PSVariable> additionalVariables)
        {
            T genObj;
            if (obj is T tObj)
            {
                genObj = tObj;
            }
            else if (LanguagePrimitives.TryConvertTo(obj, out T result))
            {
                genObj = result;
            }
            else
            {
                throw new InvalidCastException($"{nameof(obj)} is not of type '{typeof(T).FullName}'.");
            }

            return this.GetHashCode(genObj, additionalVariables);
        }
    }
}
