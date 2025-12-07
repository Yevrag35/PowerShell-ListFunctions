using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ListFunctions.Modern.Variables
{
    public abstract class PSComparingVariable
    {
        public const string X = "x";
        public const string Y = "y";
        public const string LEFT = "left";
        public const string RIGHT = "right";
        private static readonly string[] _left = new[] { X, LEFT };
        private static readonly string[] _right = new[] { Y, RIGHT };

        public abstract object? InstanceValue { get; }

        private protected PSComparingVariable()
        {
        }

        internal static PSComparingVariable<T> Left<T>()
        {
            return new PSComparingVariable<T>(_left);
        }
        internal static PSComparingVariable<T> Right<T>()
        {
            return new PSComparingVariable<T>(_right);
        }
    }
    internal sealed class PSComparingVariable<T> : PSComparingVariable
    {
        private readonly PSVariable[] _allVars;
        private readonly T _value = default!;

        internal T Value => _value;
        public override object? InstanceValue => this.Value;

        internal PSComparingVariable(string[] names)
        {
            PopulateVariables(ref _allVars, names);
        }

        private static void PopulateVariables([NotNull] ref PSVariable[]? allVars, string[] names)
        {
#if NET5_0_OR_GREATER
            allVars = new PSVariable[names.Length];
            ref string f = ref MemoryMarshal.GetArrayDataReference(names);
            ref PSVariable fVar = ref MemoryMarshal.GetArrayDataReference(allVars);

            for (int i = 0; i < names.Length; i++)
            {
                Unsafe.Add(ref fVar, i) = new PSVariable(Unsafe.Add(ref f, i), value: null);
            }
#else
            allVars = Array.ConvertAll(names, x => new PSVariable(x, value: null));
#endif
        }

        internal void AddToVarList(T value, List<PSVariable> variables)
        {
            object? val = value;
#if NET5_0_OR_GREATER
            ref PSVariable f = ref MemoryMarshal.GetArrayDataReference(_allVars);
            for (int i = 0; i < _allVars.Length; i++)
            {
                variables.Add(Unsafe.Add(ref f, i));
            }
#else
            foreach (PSVariable v in _allVars)
            {
                v.Value = val;
                variables.Add(v);
            }
#endif
        }
    }
}
