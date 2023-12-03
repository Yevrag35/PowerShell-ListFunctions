using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Modern.Variables;
using MG.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions.Modern
{
    public sealed class ScriptBlockEquality<T>
    {
        static readonly IEqualityComparer<PSVariable> _equality = new PSVariableNameEquality();

        readonly IReadOnlySet<PSVariable> _additionalVariables;
        readonly bool _hasAdditionalVariables;
        readonly ScriptBlock _scriptBlock;
        readonly PSThisVariable<T> _thisVar;
        readonly List<PSVariable> _varList;

        public ScriptBlockEquality(ScriptBlock scriptBlock)
            : this(scriptBlock, null)
        {
        }
        public ScriptBlockEquality(ScriptBlock scriptBlock, IEnumerable<PSVariable>? additionalVariables)
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
                return Empty<PSVariable>.Set;
            }
            else if (count == 1)
            {
                return new SingleValueReadOnlySet<PSVariable>(collection.First(), _equality);
            }

            return new ReadOnlySet<PSVariable>(collection, _equality);
        }

        public bool Any(IEnumerable<T>? collection)
        {
            if (!(collection is null))
            {
                foreach (T item in collection)
                {
                    if (this.IsTrue(item))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool All(IEnumerable<T>? collection)
        {
            if (collection is null || collection.TryGetCount(out int count) && count <= 0)
            {
                return false;
            }

            bool result = true;

            foreach (T item in collection)
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

            return results.GetFirstValue(x => Convert.ToBoolean(x));
        }
    }
}
