using ListFunctions.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace ListFunctions.Modern.Variables
{
    /// <summary>
    /// Represents a set of special PowerShell variables (such as '_', 'this', and 'psitem') that refer to the current
    /// object or context within a PowerShell pipeline or script block.
    /// </summary>
    /// <remarks>This class provides access to commonly used contextual variables in PowerShell, allowing
    /// their values to be set and inserted into variable lists as needed. It is typically used to manage the values of
    /// these variables during script execution or when emulating PowerShell behavior in custom hosts or engines.</remarks>
    public sealed class PSThisVariable : IPoolable, ICloneable
    {
        public const string Underscore = "_";
        public const string This = "this";
        public const string PSItem = "psitem";
        public const string FirstArg = "args[0]";
        public const string SecondArg = "args[1]";

        private PSVariable[]? _variables;
        public object? ObjValue { get; private set; }
        public PSThisVariable()
        {
        }
        private PSThisVariable(PSThisVariable other)
        {
            this.ObjValue = other.ObjValue.CloneIf();
        }

        /// <summary>
        /// Creates a new PSThisVariable object that is a copy of the current instance.
        /// </summary>
        /// <returns>A new PSThisVariable object with the same values as the current instance.</returns>
        public PSThisVariable Clone()
        {
            return new(this);
        }
        [DebuggerStepThrough]
        object ICloneable.Clone()
        {
            return this.Clone();
        }

        public void InsertIntoList(List<PSVariable> list)
        {
#if NETCOREAPP
            _ = list.EnsureCapacity(3);
#endif
            list.InsertRange(0, InitializeArray(ref _variables, this.ObjValue));
        }

        public void SetValue(object? value)
        {
            this.ObjValue = value;
            _ = InitializeArray(ref _variables, value);
        }

        private static PSVariable[] InitializeArray([NotNull] ref PSVariable[]? array, object? value)
        {
            if (array is null)
            {
                array =
                [
                    new(Underscore, value),
                    new(This, value),
                    new(PSItem, value),
                ];

                return array;
            }

            if (array.Length != 3)
            {
                array = null;
                return InitializeArray(ref array, value);
            }
            
            for (int i = 0; i < array.Length; i++)
            {
                ref PSVariable v = ref array[i];
                if (v is null)
                {
                    array = null;
                    return InitializeArray(ref array, value);
                }

                v.Value = value;
            }

            Array.Sort(array, VariableComparer.Shared);
            return array;
        }

        void IPoolable.Initialize()
        {
        }
        public bool TryReset()
        {
            if (_variables is not null)
            {
                Array.Clear(_variables, 0, _variables.Length);
            }

            this.ObjValue = null;
            return true;
        }

        private sealed class VariableComparer : IComparer<PSVariable>
        {
            internal static readonly VariableComparer Shared = new();

            public int Compare(PSVariable? x, PSVariable? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;

                switch (x.Name)
                {
                    case Underscore:
                        return Underscore.Equals(y.Name) ? 0 : 1;

                    case This:
                        return This.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : Underscore.Equals(y.Name) ? -1 : 1;

                    case PSItem:
                        return PSItem.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : (Underscore.Equals(y.Name) || This.Equals(y.Name, StringComparison.OrdinalIgnoreCase)) ? -1 : 1;

                    default:
                        return Underscore.Equals(y.Name) || This.Equals(y.Name, StringComparison.OrdinalIgnoreCase) || PSItem.Equals(y.Name, StringComparison.OrdinalIgnoreCase)
                            ? -1
                            : string.CompareOrdinal(x.Name, y.Name);
                }
            }
        }
    }
}
