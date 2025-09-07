using ListFunctions.Internal;
using ListFunctions.Modern.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using ZLinq;

namespace ListFunctions.Modern
{
    public static class EqualityBlock
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="hashCodeBlock"></param>
        /// <param name="equalityScript"></param>
        /// <param name="additionalVariables"></param>
        /// <returns></returns>
        /// /// <exception cref="ArgumentException">
        ///     <paramref name="equalityScript"/> is not a proper scriptblock.
        /// </exception>
        /// <exception cref="ArgumentNullException"/>
        public static IEqualityBlock CreateBlock(Type type, IHashCodeBlock hashCodeBlock, ScriptBlock equalityScript, IEnumerable<PSVariable>? additionalVariables)
        {
            Guard.NotNull(type, nameof(type));
            Guard.NotNull(hashCodeBlock, nameof(hashCodeBlock));
            Guard.NotNull(equalityScript, nameof(equalityScript));

            MethodInfo genMeth = _genMeth.MakeGenericMethod(type);
            object[] args = new object[] { hashCodeBlock, equalityScript, additionalVariables! };

            return genMeth.Invoke(null, args) as IEqualityBlock
                ?? throw new InvalidOperationException("Unable to create generic equality block instance.");
        }
        
        static readonly MethodInfo _genMeth = typeof(EqualityBlock)
            .GetMethod(nameof(CreateGenericBlock), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Unable to find generic method definition for CreateGenericBlock.");

        private static EqualityBlock<T> CreateGenericBlock<T>(IHashCodeBlock hashCodeBlock, ScriptBlock equalityScript, IEnumerable<PSVariable>? additionalVariables)
        {
            return new EqualityBlock<T>(equalityScript, hashCodeBlock, additionalVariables);
        }
    }

    public sealed class EqualityBlock<T> : ComparingBase, IEqualityBlock, IEqualityComparer<T>
    {
        readonly List<PSVariable> _varList;
        readonly PSVariable[] _additionalVariables;
        readonly IHashCodeBlock _hashCodeBlock;
        readonly PSComparingVariable<T> _left;
        readonly PSComparingVariable<T> _right;
        readonly Type _checksType;

        Type IEqualityBlock.ChecksType => _checksType;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scriptBlock"></param>
        /// <param name="hashCodeBlock"></param>
        /// <param name="additionalVariables"></param>
        /// <exception cref="ArgumentException"><paramref name="scriptBlock"/> is not a proper scriptblock.</exception>
        /// <exception cref="ArgumentNullException"/>
        public EqualityBlock(ScriptBlock scriptBlock, IHashCodeBlock hashCodeBlock, IEnumerable<PSVariable>? additionalVariables)
            : this(scriptBlock, hashCodeBlock, additionalVariables, preValidated: false)
        {
        }
        internal EqualityBlock(ScriptBlock scriptBlock, IHashCodeBlock hashCodeBlock, IEnumerable<PSVariable>? additionalVariables, bool preValidated)
            : base(scriptBlock, preValidated)
        {
            Guard.NotNull(hashCodeBlock, nameof(hashCodeBlock));
            _additionalVariables = additionalVariables is null
                ? Array.Empty<PSVariable>()
                : additionalVariables.AsValueEnumerable().ToArray();

            _checksType = typeof(T);
            if (!typeof(T).Equals(hashCodeBlock.HashesType))
            {
                throw new ArgumentException($"{nameof(hashCodeBlock)} does not hash type '{typeof(T).FullName}'.");
            }

            _hashCodeBlock = hashCodeBlock;
            _varList = new List<PSVariable>(4);
            _left = PSComparingVariable.Left<T>();
            _right = PSComparingVariable.Right<T>();
        }

        public bool Equals([System.Diagnostics.CodeAnalysis.AllowNull] T x, [System.Diagnostics.CodeAnalysis.AllowNull] T y)
        {
            if (x is null && y is null)
            {
                return true;
            }
            else if (ReferenceEquals(x, y))
            {
                return true;
            }

            _varList.Clear();
            _left.AddToVarList(x, _varList);
            _right.AddToVarList(y, _varList);
            _varList.AddRange(_additionalVariables);

            return this.Script.InvokeWithContext(_varList, x => LanguagePrimitives.ConvertTo<bool>(x));
        }
        bool IEqualityComparer.Equals(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (TryConvert(x, out T? isX) && TryConvert(y, out T? isY))
            {
                return this.Equals(isX, isY);
            }
            else
            {
                return false;
            }
        }

        private static bool TryConvert(object? obj, [MaybeNullWhen(false)] out T converted)
        {
            if (obj is T tObj)
            {
                converted = tObj;
                return true;
            }
            else if (LanguagePrimitives.TryConvertTo(obj, out T tRes) && !(tRes is null))
            {
                converted = tRes;
                return true;
            }
            else
            {
                converted = default;
                return false;
            } 
        }

        public int GetHashCode(T obj)
        {
            Guard.NotNull(obj, nameof(obj));
            return _hashCodeBlock.GetHashCode(obj, _additionalVariables);
        }
        int IEqualityComparer.GetHashCode(object? obj)
        {
            Guard.NotNull(obj, nameof(obj));
            return _hashCodeBlock.GetHashCode(obj, _additionalVariables);
        }
    }
}
