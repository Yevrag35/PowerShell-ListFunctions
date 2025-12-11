using ListFunctions.Internal;
using ListFunctions.Modern.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Text;
using ZLinq;

namespace ListFunctions.Modern;

public interface IEqualityBlock : IEqualityComparer, IEqualityComparer<object>
{
    IHashBlock HashCodeBlock { get; }
}

public sealed class EqualityBlock : ComparingBase, IEqualityBlock
{
    private readonly PSVariable[] _additionalVariables;
    private readonly List<PSVariable> _varList;
    private readonly ObjVariable _left;
    private readonly ObjVariable _right;

    public IHashBlock HashCodeBlock { get; }

    public EqualityBlock(ScriptBlock equalityBlock, IHashBlock hashCodeBlock) : this(equalityBlock, hashCodeBlock, additionalVariables: null)
    {
    }
    public EqualityBlock(ScriptBlock equalityBlock, IHashBlock hashCodeBlock, IEnumerable<PSVariable>? additionalVariables) : base(equalityBlock, preValidated: false)
    {
        _additionalVariables = additionalVariables is not null
            ? additionalVariables.AsValueEnumerable().ToArray()
            : [];

        _varList = new(3 + _additionalVariables.Length);
        this.HashCodeBlock = hashCodeBlock;
        _left = new(isLeft: true);
        _right = new(isLeft: false);
    }
#if NET9_0_OR_GREATER
    public EqualityBlock(ScriptBlock equalityBlock, IHashBlock hashCodeBlock, params ReadOnlySpan<PSVariable> variables) : base(equalityBlock, preValidated: false)
    {
        _additionalVariables = !variables.IsEmpty
            ? variables.AsValueEnumerable().ToArray()
            : [];

        _varList = new(3 + _additionalVariables.Length);
        this.HashCodeBlock = hashCodeBlock;
        _left = new(isLeft: true);
        _right = new(isLeft: false);
    }

#endif

    public new bool Equals(object? x, object? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        _varList.Clear();
        _left.AddToList(x, _varList);
        _right.AddToList(y, _varList);
        _varList.AddRange(_additionalVariables);

        return this.Script.InvokeWithContext(_varList, LanguagePrimitives.IsTrue);
    }

    public int GetHashCode([DisallowNull] object obj)
    {
        Guard.NotNull(obj);
        return this.HashCodeBlock.GetHashCode(obj, _additionalVariables);
    }

    private sealed class ObjVariable : PSComparingVariable
    {
        private readonly PSVariable[] _variables;

        internal object? Value { get; set; }
        public override object? InstanceValue => this.Value;
        internal ObjVariable(bool isLeft)
        {
            ReadOnlySpan<string> names = (isLeft ? LeftNames : RightNames).AsSpan();
            _variables = new PSVariable[names.Length];
            
            for (int i = 0; i < names.Length; i++)
            {
                _variables[i] = new(names[i]);
            }
        }

        internal void AddToList(object? value, List<PSVariable> list)
        {
#if NETCOREAPP
            list.EnsureCapacity(_variables.Length);
#endif

            foreach (PSVariable psVar in _variables)
            {
                psVar.Value = value;
                list.Add(psVar);
            }
        }
    }
}
