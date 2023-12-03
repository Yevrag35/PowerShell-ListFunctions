using ListFunctions.Extensions;
using ListFunctions.Modern.Variables;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ListFunctions.Modern
{
    public sealed class ComparingBlock<T> : ComparingBase<T>, IComparer<T>
    {
        readonly ScriptBlock _compareScript;
        readonly PSComparingVariable<T> _left;
        readonly PSComparingVariable<T> _right;
        readonly List<PSVariable> _varList;

        public T CurrentLeft => _left.Value;
        public T CurrentRight => _right.Value;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scriptBlock"></param>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentNullException"/>
        public ComparingBlock(ScriptBlock scriptBlock)
            : this(scriptBlock, preValidated: false)
        {
        }
        internal ComparingBlock(ScriptBlock scriptBlock, bool preValidated)
            : base(scriptBlock, preValidated)
        {
            _varList = new List<PSVariable>(4);
            _left = PSComparingVariable<T>.Left();
            _right = PSComparingVariable<T>.Right();
            _compareScript = scriptBlock;
        }

        public int Compare(T left, T right)
        {
            if (left is null && right is null)
            {
                return 0;
            }
            else if (left is null)
            {
                return -1;
            }
            else if (right is null)
            {
                return 1;
            }

            _varList.Clear();
            _left.AddToVarList(left, _varList);
            _right.AddToVarList(right, _varList);

            var results = _compareScript.InvokeWithContext(null, _varList, Array.Empty<object>());
            if (results.Count <= 0)
            {
                return 0;
            }

            return results.GetFirstValue(x => Convert.ToInt32(x));
        }
    }
}
