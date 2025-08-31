using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Modern.Variables
{
    public class PSComparingVariable
    {
        public const string X = "x";
        public const string Y = "y";
        public const string LEFT = "left";
        public const string RIGHT = "right";

        private protected PSComparingVariable()
        {
        }
    }
    internal sealed class PSComparingVariable<T> : PSComparingVariable
    {
        static readonly IReadOnlyList<string> _left = new string[] { X, LEFT };
        static readonly IReadOnlyList<string> _right = new string[] { Y, RIGHT };

        readonly PSVariable[] _allVars;
        T _value = default!;

        internal T Value => _value;

        private PSComparingVariable(IReadOnlyList<string> names)
            : base()
        {
            _allVars = new PSVariable[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                _allVars[i] = new PSVariable(names[i], null);
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

        internal static PSComparingVariable<T> Left()
        {
            return new PSComparingVariable<T>(_left);
        }
        internal static PSComparingVariable<T> Right()
        {
            return new PSComparingVariable<T>(_right);
        }
    }
}
