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

/// <summary>
/// Defines a contract for objects that provide custom equality comparison logic and expose a hash code computation
/// block.
/// </summary>
/// <remarks>Implementations of this interface enable advanced scenarios where equality and hash code logic can be
/// encapsulated and reused, such as in dynamic or scriptable comparison strategies. This interface extends both <see
/// cref="IEqualityComparer"/> and <see cref="IEqualityComparer{Object}"/>, allowing it to be used in generic and
/// non-generic contexts.</remarks>
public interface IEqualityBlock : IEqualityComparer, IEqualityComparer<object>
{
    /// <summary>
    /// Gets the hash code block retrieval script associated with the <see cref="IEqualityBlock"/>.
    /// </summary>
    IHashBlock HashCodeBlock { get; }
}

/// <summary>
/// Represents a block that defines custom equality and hash code logic using PowerShell script blocks and variables.
/// </summary>
/// <remarks>Use this class to encapsulate equality comparison and hash code generation for objects, where the
/// logic is provided by user-defined PowerShell script blocks. This enables advanced or dynamic comparison scenarios,
/// such as those required in PowerShell-based data processing or custom collections.</remarks>
public sealed class EqualityBlock : ComparingBase, IEqualityBlock
{
    private readonly PSVariable[] _additionalVariables;
    private readonly List<PSVariable> _varList;
    private readonly ObjVariable _left;
    private readonly ObjVariable _right;

    /// <inheritdoc/>
    public IHashBlock HashCodeBlock { get; }

    /// <summary>
    /// Initializes a new instance of the EqualityBlock class with the specified equality and hash code blocks.
    /// </summary>
    /// <param name="equalityBlock">The script block that defines the logic for determining equality between objects. Cannot be null.</param>
    /// <param name="hashCodeBlock">The hash block that provides the logic for computing hash codes. Cannot be null.</param>
    /// <inheritdoc cref="ComparingBase(ScriptBlock, bool)" path="/exception"/>
    public EqualityBlock(ScriptBlock equalityBlock, IHashBlock hashCodeBlock) : this(equalityBlock, hashCodeBlock, additionalVariables: null)
    {
    }
    /// <summary>
    /// Initializes a new instance of the EqualityBlock class with the specified equality and hash code script blocks,
    /// and optional additional variables.
    /// </summary>
    /// <param name="equalityBlock">The script block that defines the equality comparison logic. Cannot be null.</param>
    /// <param name="hashCodeBlock">The hash block used to compute hash codes for objects being compared. Cannot be null.</param>
    /// <param name="additionalVariables">An optional collection of variables to be made available within the equality and hash code script blocks. If
    /// null, no additional variables are provided.</param>
    /// <inheritdoc cref="ComparingBase(ScriptBlock, bool)" path="/exception"/>
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
    /// <summary>
    /// Initializes a new instance of the EqualityBlock class with the specified equality and hash code script blocks
    /// and an optional set of additional variables.
    /// </summary>
    /// <param name="equalityBlock">The script block used to determine equality between objects. Cannot be null.</param>
    /// <param name="hashCodeBlock">The hash code block used to compute hash codes for objects. Cannot be null.</param>
    /// <param name="variables">A read-only span of additional variables to be used within the equality and hash code blocks. May be empty.</param>
    /// <inheritdoc cref="ComparingBase(ScriptBlock, bool)" path="/exception"/>
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

    /// <summary>
    /// Determines whether the specified objects are considered equal according to the configured equality script block.
    /// </summary>
    /// <remarks>This method uses the equality script block provided to the <see cref="EqualityBlock"/> instance to evaluate
    /// equality. Both objects and any additional variables are passed to the script block for comparison. Null values
    /// are supported and are considered equal if both parameters are null.</remarks>
    /// <param name="x">The first object to compare. May be null.</param>
    /// <param name="y">The second object to compare. May be null.</param>
    /// <returns>true if the objects are considered equal; otherwise, false.</returns>
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

    /// <summary>
    /// Returns a hash code for the specified object using the configured hash code computation logic.
    /// </summary>
    /// <param name="obj">The object for which to compute the hash code. Cannot be null.</param>
    /// <returns>An integer hash code for the specified object.</returns>
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
