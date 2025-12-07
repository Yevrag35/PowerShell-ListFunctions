using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Modern.Variables
{
    public class PSThisVariable : IPoolable
    {
        public const string UNDERSCORE_NAME = "_";
        public const string THIS_NAME = "this";
        public const string PSITEM_NAME = "psitem";
        public const string ARGS_FIRST = "args[0]";
        public const string ARGS_SECOND = "args[1]";

        private static readonly
#if NET8_0_OR_GREATER
            System.Collections.Frozen.FrozenSet<string>
#else
            HashSet<string>
#endif
            _names = GetThisNames();

        private readonly PSVariable[] _allVars;
        public object? ObjValue { get; private set; }
        public PSThisVariable()
        {
            _allVars = new PSVariable[]
            {
                new PSVariable(UNDERSCORE_NAME, null),
                new PSVariable(THIS_NAME, null),
                new PSVariable(PSITEM_NAME, null),
            };
        }

        public void InsertIntoList(List<PSVariable> list)
        {
            list.InsertRange(0, _allVars);
        }
        internal static bool IsThisVariable(string name)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            return _names.Contains(name);
        }
        internal static bool IsThisVariable(PSVariable variable)
        {
            Guard.NotNull(variable, nameof(variable));
            return _names.Contains(variable.Name);
        }
        public void SetValue(object? value)
        {
            this.ObjValue = value;
            foreach (PSVariable v in _allVars)
            {
                v.Value = value;
            }
        }

        private static
#if NET8_0_OR_GREATER
            System.Collections.Frozen.FrozenSet<string>
#else
            HashSet<string>
#endif
        GetThisNames()
        {
            HashSet<string> set = new(StringComparer.OrdinalIgnoreCase)
            {
                UNDERSCORE_NAME,
                THIS_NAME,
                PSITEM_NAME,
            };

#if !NET8_0_OR_GREATER
            return set;
#else
            return System.Collections.Frozen.FrozenSet.ToFrozenSet(set, set.Comparer);
#endif
        }

        void IPoolable.Initialize()
        {
        }
        public bool TryReset()
        {
            Array.ForEach(_allVars, v => v.Value = null);
            this.ObjValue = null;
            return true;
        }
    }
}
