using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Modern.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Modern
{
    public interface IComparingBlock : IComparer
    {
        Type ChecksType { get; }
    }

    public static class ComparingBlock
    {
        public static IComparer Create(ScriptBlock scriptBlock, Type genericType, IEnumerable<PSVariable>? additionalVariables)
        {
            MethodInfo genMeth = _getInit.Value.MakeGenericMethod(genericType);
            return genMeth.Invoke(null, new object[] { scriptBlock, additionalVariables! }) as IComparer
                ?? throw new InvalidOperationException("Unable to create generic comparing block instance.");
        }
        public static ComparingBlock<T> Create<T>(ScriptBlock scriptBlock, IEnumerable<PSVariable>? additionalVariables)
        {
            return new ComparingBlock<T>(scriptBlock, preValidated: true, additionalVariables);
        }

        static readonly Lazy<MethodInfo> _getInit = new Lazy<MethodInfo>(InitializeLazyMethod);
        private static MethodInfo InitializeLazyMethod()
        {
            Expression<Action> action = () => Create<object>(null!, null);
            return ((MethodCallExpression)action.Body).Method.GetGenericMethodDefinition();
        }
    }

    public sealed class ComparingBlock<T> : ComparingBase<T>, IComparer<T>, IComparingBlock
    {
        readonly PSVariable[] _additionalVariables;
        readonly Type _checkingType;
        readonly ScriptBlock _compareScript;
        readonly PSComparingVariable<T> _left;
        readonly PSComparingVariable<T> _right;
        readonly List<PSVariable> _varList;

        Type IComparingBlock.ChecksType => _checkingType;

        public T CurrentLeft => _left.Value;
        public T CurrentRight => _right.Value;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scriptBlock"></param>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        public ComparingBlock(ScriptBlock scriptBlock, IEnumerable<PSVariable>? additionalVariables)
            : this(scriptBlock, preValidated: false, additionalVariables)
        {
        }
        internal ComparingBlock(ScriptBlock scriptBlock, bool preValidated, IEnumerable<PSVariable>? additionalVariables)
            : base(scriptBlock, preValidated)
        {
            _additionalVariables = additionalVariables is null
                ? Array.Empty<PSVariable>()
                : additionalVariables.ToArray();

            _checkingType = typeof(T);
            _varList = new List<PSVariable>(4);
            _left = PSComparingVariable<T>.Left();
            _right = PSComparingVariable<T>.Right();
            _compareScript = scriptBlock;
        }

        public int Compare(T? left, T? right)
        {
            if (left is null && right is null)
            {
                return 0;
            }
            else if (left is null)
            {
                return -1;
            }
            else if (right is null)
            {
                return 1;
            }

            _varList.Clear();
            _left.AddToVarList(left, _varList);
            _right.AddToVarList(right, _varList);
            _varList.AddRange(_additionalVariables);

            return _compareScript.InvokeWithContext(_varList, x => LanguagePrimitives.ConvertTo<int>(x));
        }
        int IComparer.Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (LanguagePrimitives.TryConvertTo(x, out T isX) && LanguagePrimitives.TryConvertTo(y, out T isY))
            {
                return this.Compare(isX, isY);
            }
            else
            {
                throw new InvalidCastException($"Unable to cast either x or y as {typeof(T).GetTypeName()}.");
            }
        }
    }
}
