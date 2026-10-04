using ListFunctions.Extensions;
using ListFunctions.Modern.Variables;
using ZLinq;

namespace ListFunctions.Modern;

/// <summary>
/// Defines an equality comparer that also exposes the hash code provider it uses.
/// </summary>
/// <remarks>
/// The interface extends both <see cref="IEqualityComparer"/> and <see cref="IEqualityComparer{T}"/> of
/// <see cref="object"/>, so an implementation works with generic and non-generic collections.
/// </remarks>
public interface IEqualityBlock : IEqualityComparer, IEqualityComparer<object>
{
	/// <summary>
	/// Gets the hash code provider that the comparer uses.
	/// </summary>
	/// <value>The provider that computes hash codes for <see cref="IEqualityComparer{T}.GetHashCode(T)"/>.</value>
	IHashBlock HashCodeBlock { get; }
}

/// <summary>
/// Represents an equality comparer that compares objects by running a PowerShell script block.
/// </summary>
/// <remarks>
/// <para>
/// The equality script block sees the first object as <c>$x</c>, <c>$left</c>, and <c>$args[0]</c>, and the second
/// object as <c>$y</c>, <c>$right</c>, and <c>$args[1]</c>. Hash codes come from a separate <see cref="IHashBlock"/>.
/// Both script blocks also see the additional variables passed to the constructor.
/// </para>
/// <para>
/// Instances aren't thread-safe, because every comparison reuses the same list of script block variables.
/// </para>
/// </remarks>
public sealed class EqualityBlock : ComparingBase, IEqualityBlock
{
	private readonly PSVariable[] _additionalVariables;
	private readonly List<PSVariable> _varList;
	private readonly ObjVariable _left;
	private readonly ObjVariable _right;

	/// <inheritdoc/>
	public IHashBlock HashCodeBlock { get; }

	/// <summary>
	/// Initializes a new <see cref="EqualityBlock"/> instance with the specified equality script block and hash code
	/// provider.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TODO: The constructor doesn't check <paramref name="hashCodeBlock"/>, so a <see langword="null"/> value fails
	/// only when <see cref="GetHashCode(object)"/> is called.
	/// </para>
	/// </remarks>
	/// <param name="equalityBlock">The script block that determines whether <c>$x</c> and <c>$y</c> are equal. This value must not be <see langword="null"/>.</param>
	/// <param name="hashCodeBlock">The provider that computes hash codes. This value must not be <see langword="null"/>.</param>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="equalityBlock"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="equalityBlock"/> has no statements to run, or has a <c>begin</c> block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block.</exception>
	public EqualityBlock(ScriptBlock equalityBlock, IHashBlock hashCodeBlock) : this(equalityBlock, hashCodeBlock, additionalVariables: null)
	{
	}
	/// <summary>
	/// Initializes a new <see cref="EqualityBlock"/> instance with the specified equality script block, hash code
	/// provider, and additional variables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TODO: The constructor doesn't check <paramref name="hashCodeBlock"/>, so a <see langword="null"/> value fails
	/// only when <see cref="GetHashCode(object)"/> is called.
	/// </para>
	/// </remarks>
	/// <param name="equalityBlock">The script block that determines whether <c>$x</c> and <c>$y</c> are equal. This value must not be <see langword="null"/>.</param>
	/// <param name="hashCodeBlock">The provider that computes hash codes. This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the scope of both script blocks, or <see langword="null"/> for none. The constructor copies them.</param>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="equalityBlock"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="equalityBlock"/> has no statements to run, or has a <c>begin</c> block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block.</exception>
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
	/// Initializes a new <see cref="EqualityBlock"/> instance with the specified equality script block, hash code
	/// provider, and span of additional variables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TODO: The constructor doesn't check <paramref name="hashCodeBlock"/>, so a <see langword="null"/> value fails
	/// only when <see cref="GetHashCode(object)"/> is called.
	/// </para>
	/// </remarks>
	/// <param name="equalityBlock">The script block that determines whether <c>$x</c> and <c>$y</c> are equal. This value must not be <see langword="null"/>.</param>
	/// <param name="hashCodeBlock">The provider that computes hash codes. This value must not be <see langword="null"/>.</param>
	/// <param name="variables">The variables to define in the scope of both script blocks. The span can be empty. The constructor copies it.</param>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="equalityBlock"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="equalityBlock"/> has no statements to run, or has a <c>begin</c> block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block.</exception>
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
	/// Determines whether the specified objects are equal by running the equality script block.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method returns <see langword="true"/> without running the script block when the objects are the same
	/// reference or both <see langword="null"/>. When only one is <see langword="null"/>, the script block still runs
	/// and sees <see langword="null"/> for that object.
	/// </para>
	/// <para>
	/// The first output of the script block is converted to a <see cref="bool"/> by PowerShell's rules. A script block
	/// with no output means the objects aren't equal.
	/// </para>
	/// </remarks>
	/// <param name="x">The first object to compare, or <see langword="null"/>.</param>
	/// <param name="y">The second object to compare, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if the objects are equal; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws.</exception>
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

		return this.Script.InvokeWithContext(_varList, [x, y], LanguagePrimitives.IsTrue);
	}

	/// <summary>
	/// Computes the hash code of the specified object with the hash code provider.
	/// </summary>
	/// <remarks>
	/// The method passes the additional variables from the constructor to the provider.
	/// </remarks>
	/// <param name="obj">The object to compute the hash code of. This value must not be <see langword="null"/>.</param>
	/// <returns>The hash code that <see cref="HashCodeBlock"/> computes for <paramref name="obj"/>.</returns>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
	/// <exception cref="Exceptions.HashCodeScriptException">Thrown when <see cref="HashCodeBlock"/> is a <see cref="HashBlock"/> and its script block fails.</exception>
	public int GetHashCode([DisallowNull] object obj)
	{
		Guard.NotNull(obj);
		return this.HashCodeBlock.GetHashCode(obj, _additionalVariables);
	}

	/// <summary>
	/// Represents one operand of the equality script block as the pair of variables <c>$x</c> and <c>$left</c>, or
	/// <c>$y</c> and <c>$right</c>.
	/// </summary>
	/// <remarks>
	/// The instance creates its <see cref="PSVariable"/> objects once and reuses them for every comparison.
	/// </remarks>
	private sealed class ObjVariable : PSComparingVariable
	{
		private readonly PSVariable[] _variables;

		/// <summary>
		/// Gets or sets the operand value.
		/// </summary>
		/// <remarks>
		/// <para>
		/// TODO: Nothing assigns this value. <see cref="AddToList(object, List{PSVariable})"/> sets only the values of
		/// the variables, so this property is always <see langword="null"/>.
		/// </para>
		/// </remarks>
		/// <value>The operand value.</value>
		internal object? Value { get; set; }
		/// <summary>
		/// Gets the operand value.
		/// </summary>
		/// <value>The value of <see cref="Value"/>.</value>
		public override object? InstanceValue => this.Value;
		/// <summary>
		/// Initializes a new <see cref="ObjVariable"/> instance for the left or right operand.
		/// </summary>
		/// <param name="isLeft"><see langword="true"/> to create the <c>$x</c> and <c>$left</c> variables; <see langword="false"/> to create the <c>$y</c> and <c>$right</c> variables.</param>
		internal ObjVariable(bool isLeft)
		{
			ReadOnlySpan<string> names = (isLeft ? LeftNames : RightNames).AsSpan();
			_variables = new PSVariable[names.Length];

			for (int i = 0; i < names.Length; i++)
			{
				_variables[i] = new(names[i]);
			}
		}

		/// <summary>
		/// Sets every variable of this operand to the specified value and appends them to the specified list.
		/// </summary>
		/// <remarks>
		/// The method adds the same <see cref="PSVariable"/> instances on every call. Clear the list between comparisons,
		/// or it collects duplicates.
		/// </remarks>
		/// <param name="value">The operand value to assign, or <see langword="null"/>.</param>
		/// <param name="list">The list to append the variables to.</param>
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
