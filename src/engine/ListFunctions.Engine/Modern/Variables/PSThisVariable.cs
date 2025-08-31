using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Modern.Variables
{
    public abstract class PSThisVariable
    {
        public const string UNDERSCORE_NAME = "_";
        public const string THIS_NAME = "this";
        public const string PSITEM_NAME = "psitem";

        static readonly Lazy<HashSet<string>> _names = new Lazy<HashSet<string>>(GetThisNames);

        private readonly PSVariable[] _allVars;
        private protected object? ObjValue { get; private set; }
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
            this.ObjValue = value;
            foreach (PSVariable v in _allVars)
            {
                v.Value = value;
            }
        }

        private static HashSet<string> GetThisNames()
        {
            return new(StringComparer.OrdinalIgnoreCase)
            {
                UNDERSCORE_NAME,
                THIS_NAME,
                PSITEM_NAME,
            };
        }
    }

    internal sealed class PSThisVariable<T> : PSThisVariable
    {
        internal T? Value => (T?)base.ObjValue;

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
