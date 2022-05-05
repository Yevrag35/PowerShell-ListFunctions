using ListFunctions.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions
{
    public sealed class ScriptBlockEquality
    {
        private readonly List<PSVariable> _allVars;
        private readonly ScriptBlock _scriptBlock;

        public ScriptBlockEquality(ScriptBlock scriptBlock, IEnumerable<PSVariable?> variables)
        {
            _scriptBlock = scriptBlock;
            _allVars = new List<PSVariable>(FilterOnlyNonNull(variables));
            this.InsertDefaultVariable(null);
        }

        public bool Any(IEnumerable<object?>? collection)
        {
            if (collection is null)
                return false;

            return collection.Any(obj => this.IsTrue(obj));
        }

        public bool All(IEnumerable? collection)
        {
            if (collection is null)
                return false;

            bool result = true;

            foreach (object? item in collection)
            {
                if (!this.IsTrue(item))
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        private IEnumerable<PSVariable> FilterOnlyNonNull(IEnumerable<PSVariable?> variables)
        {
            foreach (PSVariable? variable in variables)
            {
                if (!(variable is null) && !IsInvalidScope(variable) && !variable.Name.Equals("null", StringComparison.CurrentCultureIgnoreCase))
                {
                    yield return variable;
                }
            }
        }

        private void InsertDefaultVariable(object? value)
        {
            _allVars.Insert(0, new PSVariable("_", value));
        }
        public bool IsTrue(object? value)
        {
            if (_allVars.Count <= 0 || _allVars[0] is null || _allVars[0].Name != "_")
            {
                this.InsertDefaultVariable(value);
            }
            else
            {
                _allVars[0].Value = value;
            }

            Collection<PSObject> results = _scriptBlock.InvokeWithContext(null, _allVars);

            return results.GetFirstValue(x => Convert.ToBoolean(x));
        }

        private static bool IsInvalidScope(PSVariable variable)
        {
            var opts = variable.Options;
            return opts.HasFlag(ScopedItemOptions.AllScope)
                   ||
                   opts.HasFlag(ScopedItemOptions.ReadOnly)
                   ||
                   opts.HasFlag(ScopedItemOptions.Constant)
                   ||
                   opts.HasFlag(ScopedItemOptions.Unspecified);
        }

        public static ScriptBlockEquality Create(ScriptBlock scriptBlock, IEnumerable<PSObject> variables)
        {
            return new ScriptBlockEquality(scriptBlock, variables.Select(x => x.ImmediateBaseObject as PSVariable));
        }

        public static ScriptBlockEquality Create(ScriptBlock scriptBlock, IEnumerable<object> variables)
        {
            if (!(variables is null) && variables.Any(x => x is PSObject))
                return Create(scriptBlock, variables.Cast<PSObject>());

            else
                return new ScriptBlockEquality(scriptBlock, variables.Cast<PSVariable>());
        }
    }
}
