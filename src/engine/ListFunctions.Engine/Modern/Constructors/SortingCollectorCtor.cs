using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;
using System.Text;

namespace ListFunctions.Modern.Constructors
{
    public sealed class SortingCollectorCtor : GenericCollectionCtor
    {
        public static readonly Type SortedSetTypeDefinition = typeof(SortedSet<>);

        IComparer? _comparer;
        readonly Type _sortedType;

        public bool IsCaseSensitive { get; set; }

        public SortingCollectorCtor(Type? genericType, IComparer? comparer)
            : base(SortedSetTypeDefinition, ToTypeArray(ref genericType), null)
        {
            _comparer = comparer;
            _sortedType = genericType;
        }

        protected override object ConstructDefault()
        {
            var comparer = new ObjectComparer(!this.IsCaseSensitive);
            _comparer ??= comparer;

            return new SortedSet<object>(comparer);
        }
        public IComparer GetComparer()
        {
            if (null != _comparer)
            {
                return _comparer;
            }
            else if (ObjectType.Equals(_sortedType))
            {
                _comparer = new ObjectComparer(!this.IsCaseSensitive);
                return _comparer;
            }
            else if (typeof(string).Equals(_sortedType))
            {
                _comparer = this.IsCaseSensitive
                    ? StringComparer.InvariantCulture
                    : StringComparer.InvariantCultureIgnoreCase;

                return _comparer;
            }

            var genMeth = _getComparerMethod.Value.MakeGenericMethod(_sortedType);
            return (IComparer)genMeth.Invoke(null, null);
        }
        protected override IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
        {
            yield return this.GetComparer();
        }
        protected override bool ShouldConstructDefault(Type[] genericTypes)
        {
            return ObjectType.Equals(_sortedType)
                   &&
                   (_comparer is null
                    ||
                    !(_comparer is IComparingBlock));
        }
        private static Type[] ToTypeArray([NotNull] ref Type? genericType)
        {
            genericType ??= typeof(object);
            return new Type[] { genericType };
        }
        private sealed class ObjectComparer : IComparer<object?>, IComparer
        {
            readonly bool _ignoreCase;
            internal bool IsCaseSensitive => !_ignoreCase;

            internal ObjectComparer(bool ignoreCase)
            {
                _ignoreCase = ignoreCase;
            }

            public int Compare(object? x, object? y)
            {
                return LanguagePrimitives.Compare(x, y, _ignoreCase);
            }
        }

        static readonly Lazy<MethodInfo> _getComparerMethod = new Lazy<MethodInfo>(InitializeLazyMethod);
        private static IComparer GetDefaultComparer<T>()
        {
            return Comparer<T>.Default;
        }
        private static MethodInfo InitializeLazyMethod()
        {
            Expression<Action> action = () => GetDefaultComparer<object>();
            MethodInfo m = ((MethodCallExpression)action.Body).Method;

            return m.GetGenericMethodDefinition();
        }
    }
}

