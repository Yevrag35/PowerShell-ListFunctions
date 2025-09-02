using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Modern.Variables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using ZLinq;

namespace ListFunctions.Modern
{
    public abstract class ScriptBlockFilter
    {
        private protected ScriptBlockFilter()
        {
        }

        protected abstract object MakePredicate();

        public static ScriptBlockFilter Create(ScriptBlock scriptBlock, Type genericType, IEnumerable<PSVariable>? additionalVariables)
        {
            Type filterType = typeof(ScriptBlockFilter<>);
            Type genFilter = filterType.MakeGenericType(genericType);

            return Activator.CreateInstance(genFilter,
                new object[] { scriptBlock, additionalVariables! })
                    as ScriptBlockFilter
                    ?? throw new InvalidOperationException("Unable to create generic script block filter instance.");
        }
        public static object ToPredicate(ScriptBlockFilter genericScriptBlock)
        {
            return genericScriptBlock.MakePredicate();
        }
    }

    public sealed class ScriptBlockFilter<T> : ScriptBlockFilter
    {
        static readonly IEqualityComparer<PSVariable> _equality = new PSVariableNameEquality();

        readonly IReadOnlySet<PSVariable> _additionalVariables;
        readonly bool _hasAdditionalVariables;
        readonly ScriptBlock _scriptBlock;
        Predicate<T>? _predicate;
        readonly PSThisVariable<T> _thisVar;
        readonly List<PSVariable> _varList;

        public ScriptBlockFilter(ScriptBlock scriptBlock)
            : this(scriptBlock, null)
        {
        }
        public ScriptBlockFilter(ScriptBlock scriptBlock, IEnumerable<PSVariable>? additionalVariables)
        {
            Guard.NotNull(scriptBlock, nameof(scriptBlock));
            _thisVar = new PSThisVariable<T>();

            _scriptBlock = scriptBlock;
            int count = 1;
            if (!(additionalVariables is null) && additionalVariables.TryGetCount(out int enumCount))
            {
                count += enumCount;
            }

            _varList = new List<PSVariable>(count);
            _additionalVariables = GetAdditionalVariables(additionalVariables, out bool hasAdditional);
            _hasAdditionalVariables = hasAdditional;
        }

        private static IReadOnlySet<PSVariable> GetAdditionalVariables(IEnumerable<PSVariable>? collection, out bool hasAdditional)
        {
            hasAdditional = true;
            if (collection is null || collection.TryGetCount(out int count) && count == 0)
            {
                hasAdditional = false;
                return Empty.Set<PSVariable>();
            }
            else if (count == 1)
            {
                return SingleValueReadOnlySet.Create(collection, _equality);
            }

            return new ReadOnlySet<PSVariable>(new HashSet<PSVariable>(collection, _equality));
        }

        public bool Any(IEnumerable<T>? collection)
        {
            if (collection is null || (collection.TryGetCount(out int count) && count <= 0))
            {
                return false;
            }

            bool flag = false;
            foreach (T item in collection.AsValueEnumerable())
            {
                if (this.IsTrue(item))
                {
                    flag = true;
                    break;
                }
            }

            return flag;
        }

        public bool All(IEnumerable<T>? collection)
        {
            if (collection is null || (collection.TryGetCount(out int count) && count <= 0))
            {
                return false;
            }

            bool result = true;

            foreach (T item in collection.AsValueEnumerable())
            {
                if (!this.IsTrue(item))
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        private List<PSVariable> InitializeContext(T value)
        {
            _varList.Clear();
            _thisVar.AddToVarList(value, _varList);
            if (_hasAdditionalVariables)
            {
                _varList.AddRange(_additionalVariables);
            }

            return _varList;
        }
        public bool IsTrue(T value)
        {
            List<PSVariable> list = this.InitializeContext(value);
            Collection<PSObject> results = _scriptBlock.InvokeWithContext(null, list, Array.Empty<object>());

            return results.GetFirstValue(ConvertToBool);
        }

        private Predicate<T> ToPredicate()
        {
            return _predicate ??= new Predicate<T>(x => this.IsTrue(x));
        }
        protected override object MakePredicate()
        {
            return this.ToPredicate();
        }

        public static implicit operator Predicate<T>(ScriptBlockFilter<T> filter)
        {
            return filter.ToPredicate();
        }

        private static bool ConvertToBool(object value) => Convert.ToBoolean(value);
    }
}
