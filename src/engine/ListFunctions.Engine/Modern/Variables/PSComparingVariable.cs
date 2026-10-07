namespace ListFunctions.Modern.Variables;

/// <summary>
/// Represents one operand of a comparison script block, which the script block sees as a pair of variables such as
/// <c>$x</c> and <c>$left</c>.
/// </summary>
/// <remarks>
/// <para>
/// The left operand is exposed under the names <see cref="X"/> and <see cref="LEFT"/>, and the right operand under
/// <see cref="Y"/> and <see cref="RIGHT"/>. Every name of an operand refers to the same value.
/// </para>
/// <para>
/// Only types in this assembly can derive from this class.
/// </para>
/// </remarks>
internal abstract class PSComparingVariable
{
	/// <summary>
	/// The short name of the variable that holds the left operand, <c>$x</c>.
	/// </summary>
	internal const string X = "x";
	/// <summary>
	/// The short name of the variable that holds the right operand, <c>$y</c>.
	/// </summary>
	internal const string Y = "y";
	/// <summary>
	/// The long name of the variable that holds the left operand, <c>$left</c>.
	/// </summary>
	internal const string LEFT = "left";
	/// <summary>
	/// The long name of the variable that holds the right operand, <c>$right</c>.
	/// </summary>
	internal const string RIGHT = "right";
	private static readonly string[] s_left = [X, LEFT];
	private static readonly string[] s_right = [Y, RIGHT];
	/// <summary>
	/// The names of the variables that hold the left operand, <see cref="X"/> and <see cref="LEFT"/>.
	/// </summary>
	/// <remarks>
	/// The array wraps a shared private array without copying it.
	/// </remarks>
	protected static readonly ImmutableArray<string> LeftNames = ImmutableCollectionsMarshal.AsImmutableArray(s_left);
	/// <summary>
	/// The names of the variables that hold the right operand, <see cref="Y"/> and <see cref="RIGHT"/>.
	/// </summary>
	/// <remarks>
	/// The array wraps a shared private array without copying it.
	/// </remarks>
	protected static readonly ImmutableArray<string> RightNames = ImmutableCollectionsMarshal.AsImmutableArray(s_right);

	/// <summary>
	/// Gets the operand value that this variable represents.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TODO: Neither implementation in this assembly assigns the value it returns, so this property always returns
	/// <see langword="null"/> or the default value of the operand type, even after the variables are given a value.
	/// </para>
	/// </remarks>
	/// <value>The operand value, as an <see cref="object"/>.</value>
	public abstract object? InstanceValue { get; }

	/// <summary>
	/// Initializes a new <see cref="PSComparingVariable"/> instance.
	/// </summary>
	private protected PSComparingVariable()
	{
	}

	/// <summary>
	/// Creates the variables for the left operand of a comparison.
	/// </summary>
	/// <typeparam name="T">The type of the operand.</typeparam>
	/// <returns>A new variable set named <see cref="X"/> and <see cref="LEFT"/>.</returns>
	internal static PSComparingVariable<T> Left<T>()
	{
		return new PSComparingVariable<T>(s_left);
	}
	/// <summary>
	/// Creates the variables for the right operand of a comparison.
	/// </summary>
	/// <typeparam name="T">The type of the operand.</typeparam>
	/// <returns>A new variable set named <see cref="Y"/> and <see cref="RIGHT"/>.</returns>
	internal static PSComparingVariable<T> Right<T>()
	{
		return new PSComparingVariable<T>(s_right);
	}
}
/// <summary>
/// Represents one typed operand of a comparison script block as a set of reusable PowerShell variables.
/// </summary>
/// <remarks>
/// <para>
/// The instance creates its <see cref="PSVariable"/> objects once and reuses them for every comparison, so
/// <see cref="AddToVarList(T, List{PSVariable})"/> doesn't allocate new variables.
/// </para>
/// <para>
/// Instances aren't thread-safe. Adding a value changes the variables that earlier calls added to other lists.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the operand.</typeparam>
internal sealed class PSComparingVariable<T> : PSComparingVariable
{
	private readonly PSVariable[] _allVars;
	private readonly T _value = default!;

	/// <summary>
	/// Gets the operand value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TODO: Nothing assigns this value, so it is always the default value of <typeparamref name="T"/>.
	/// <see cref="AddToVarList(T, List{PSVariable})"/> sets only the values of the variables.
	/// </para>
	/// </remarks>
	/// <value>The operand value.</value>
	internal T Value => _value;
	/// <summary>
	/// Gets the operand value as an <see cref="object"/>.
	/// </summary>
	/// <value>The value of <see cref="Value"/>, boxed when <typeparamref name="T"/> is a value type.</value>
	public override object? InstanceValue => this.Value;

	/// <summary>
	/// Initializes a new <see cref="PSComparingVariable{T}"/> instance with one variable for each of the specified
	/// names.
	/// </summary>
	/// <param name="names">The names of the variables, without the <c>$</c> sigil.</param>
	internal PSComparingVariable(string[] names)
	{
		PopulateVariables(ref _allVars, names);
	}

	/// <summary>
	/// Creates one variable for each of the specified names, each with a <see langword="null"/> value.
	/// </summary>
	/// <param name="allVars">The array that receives the new variables. It is replaced, never reused.</param>
	/// <param name="names">The names of the variables.</param>
	private static void PopulateVariables([NotNull] ref PSVariable[]? allVars, string[] names)
	{
		allVars = new PSVariable[names.Length];
		for (int i = 0; i < names.Length; i++)
		{
			allVars[i] = new PSVariable(names[i], value: null);
		}
	}

	/// <summary>
	/// Sets every variable of this operand to the specified value and appends them to the specified list.
	/// </summary>
	/// <remarks>
	/// The method adds the same <see cref="PSVariable"/> instances on every call. Clear the list between comparisons,
	/// or it collects duplicates.
	/// </remarks>
	/// <param name="value">The operand value to assign.</param>
	/// <param name="variables">The list to append the variables to.</param>
	internal void AddToVarList(T value, List<PSVariable> variables)
	{
		foreach (PSVariable v in _allVars)
		{
			v.Value = value;
			variables.Add(v);
		}
	}
}
