using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ListFunctions.Modern.Variables
{
    /// <summary>
    /// Represents a variable used in comparison operations, such as the left or right operand in a comparison
    /// expression.
    /// </summary>
    /// <remarks>This abstract base class provides a common interface for variables that participate in
    /// comparison logic, typically representing the left or right side of a comparison. Derived types specify the
    /// actual value and context for the variable. Common names for left and right variables are provided as constants
    /// for use in comparison scenarios.</remarks>
    public abstract class PSComparingVariable
    {
        public const string X = "x";
        public const string Y = "y";
        public const string LEFT = "left";
        public const string RIGHT = "right";
        private static readonly string[] _left = [X, LEFT];
        private static readonly string[] _right = [Y, RIGHT];
        protected static readonly ImmutableArray<string> LeftNames = ImmutableCollectionsMarshal.AsImmutableArray(_left);
        protected static readonly ImmutableArray<string> RightNames = ImmutableCollectionsMarshal.AsImmutableArray(_right);

        /// <summary>
        /// Gets the value represented by this comparing variable instance.
        /// </summary>
        public abstract object? InstanceValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PSComparingVariable"/> class.
        /// </summary>
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
            allVars = new PSVariable[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                allVars[i] = new PSVariable(names[i], value: null);
            }
        }

        internal void AddToVarList(T value, List<PSVariable> variables)
        {
            foreach (PSVariable v in _allVars)
            {
                v.Value = value;
                variables.Add(v);
            }
        }
    }
}
