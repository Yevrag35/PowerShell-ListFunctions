using ListFunctions.Internal;
using MG.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions.Modern.Variables
{
    public abstract class PSThisVariable
    {
        public const string UNDERSCORE_NAME = "_";
        public const string THIS_NAME = "this";
        public const string PSITEM_NAME = "psitem";

        static readonly Lazy<IReadOnlySet<string>> _names = new Lazy<IReadOnlySet<string>>(GetThisNames);

        readonly PSVariable[] _allVars;
        private protected PSThisVariable()
        {
            _allVars = new PSVariable[3];
            _allVars[0] = new PSVariable(UNDERSCORE_NAME, null);
            _allVars[1] = new PSVariable(THIS_NAME, null);
            _allVars[2] = new PSVariable(PSITEM_NAME, null);
        }

        private protected void InsertIntoList(List<PSVariable> list)
        {
            list.InsertRange(0, _allVars);
        }
        internal static bool IsThisVariable(string name)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            return _names.Value.Contains(name);
        }
        internal static bool IsThisVariable(PSVariable variable)
        {
            Guard.NotNull(variable, nameof(variable));
            return _names.Value.Contains(variable.Name);
        }
        private protected void SetValue(object? value)
        {
            foreach (PSVariable v in _allVars)
            {
                v.Value = value;
            }
        }

        private static IReadOnlySet<string> GetThisNames()
        {
            return new ReadOnlySet<string>(EnumerateNames(), StringComparer.InvariantCultureIgnoreCase);
        }
        private static IEnumerable<string> EnumerateNames()
        {
            yield return UNDERSCORE_NAME;
            yield return THIS_NAME;
            yield return PSITEM_NAME;
        }
    }

    internal sealed class PSThisVariable<T> : PSThisVariable
    {
        T _value = default!;

        internal T Value => _value;

        internal PSThisVariable()
            : base()
        {
        }

        internal void AddToVarList(T value, List<PSVariable> variables)
        {
            this.SetValue(value);
            this.InsertIntoList(variables);
        }
    }
}
