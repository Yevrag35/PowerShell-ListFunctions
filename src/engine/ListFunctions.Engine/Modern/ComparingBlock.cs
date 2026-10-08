using ListFunctions.Extensions;
using ListFunctions.Modern.Exceptions;
using ListFunctions.Modern.Variables;
using ZLinq;

namespace ListFunctions.Modern;

/// <summary>
/// Provides factory methods that create <see cref="ComparingBlock{T}"/> instances.
/// </summary>
internal static class ComparingBlock
{
	/// <summary>
	/// Creates a <see cref="ComparingBlock{T}"/> for the specified element type from the specified script block.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method closes <see cref="Create{T}(ScriptBlock, IEnumerable{PSVariable})"/> over
	/// <paramref name="genericType"/> and calls it through reflection.
	/// </para>
	/// <para>
	/// The method doesn't validate the script block. The caller is expected to have validated it.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block that compares <c>$x</c> (or <c>$left</c>) with <c>$y</c> (or <c>$right</c>). This value must not be <see langword="null"/>.</param>
	/// <param name="genericType">The type of the objects to compare. This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the operands, or <see langword="null"/> for none.</param>
	/// <returns>A <see cref="ComparingBlock{T}"/> closed over <paramref name="genericType"/>.</returns>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="genericType"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="genericType"/> can't be used as a generic type argument.</exception>
	/// <exception cref="TargetInvocationException">Thrown when <paramref name="scriptBlock"/> is null. The inner exception is an <see cref="System.ArgumentNullException"/>.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the created object isn't an <see cref="IComparer"/>.</exception>
	public static IComparer Create(ScriptBlock scriptBlock, Type genericType, IEnumerable<PSVariable>? additionalVariables)
	{
		MethodInfo genMeth = _getInit.Value.MakeGenericMethod(genericType);
		return genMeth.Invoke(null, [scriptBlock, additionalVariables!]) as IComparer
			?? throw new InvalidOperationException("Unable to create generic comparing block instance.");
	}
	/// <summary>
	/// Creates a <see cref="ComparingBlock{T}"/> from the specified script block.
	/// </summary>
	/// <remarks>
	/// The method doesn't validate the script block. The caller is expected to have validated it.
	/// </remarks>
	/// <typeparam name="T">The type of the objects to compare.</typeparam>
	/// <param name="scriptBlock">The script block that compares <c>$x</c> (or <c>$left</c>) with <c>$y</c> (or <c>$right</c>). This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the operands, or <see langword="null"/> for none.</param>
	/// <returns>A new <see cref="ComparingBlock{T}"/>.</returns>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	public static ComparingBlock<T> Create<T>(ScriptBlock scriptBlock, IEnumerable<PSVariable>? additionalVariables)
	{
		return new ComparingBlock<T>(scriptBlock, additionalVariables);
	}

	private static readonly Lazy<MethodInfo> _getInit = new Lazy<MethodInfo>(InitializeLazyMethod);
	/// <summary>
	/// Gets the generic method definition of <see cref="Create{T}(ScriptBlock, IEnumerable{PSVariable})"/>.
	/// </summary>
	/// <remarks>
	/// The method reads the definition from an expression tree instead of looking it up by name, so the lookup can't
	/// match the non-generic overload.
	/// </remarks>
	/// <returns>The open generic definition of the factory method.</returns>
	private static MethodInfo InitializeLazyMethod()
	{
		Expression<Action> action = () => Create<object>(null!, null);
		return ((MethodCallExpression)action.Body).Method.GetGenericMethodDefinition();
	}
}

/// <summary>
/// Represents a comparer that orders objects by running a PowerShell script block.
/// </summary>
/// <remarks>
/// <para>
/// The script block sees the first operand as <c>$x</c>, <c>$left</c>, and <c>$args[0]</c>, and the second operand as
/// <c>$y</c>, <c>$right</c>, and <c>$args[1]</c>. Its first output is converted to an <see cref="int"/> by PowerShell's
/// conversion rules and is read the same way as the result of <see cref="IComparer{T}.Compare(T, T)"/>. When the
/// script block returns no value, <see langword="null"/>, or a value that can't be converted, the comparison throws a
/// <see cref="ComparingScriptException"/>.
/// </para>
/// <para>
/// Instances aren't thread-safe, because every comparison reuses the same list of script block variables.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the objects to compare.</typeparam>
internal sealed class ComparingBlock<T> : ComparingBase, IComparer<T>, IComparer
{
	private readonly PSVariable[] _additionalVariables;
	private readonly ScriptBlock _compareScript;
	private readonly PSComparingVariable<T> _left;
	private readonly PSComparingVariable<T> _right;
	private readonly List<PSVariable> _varList;

	/// <summary>
	/// Gets the left operand of the current comparison.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TODO: Nothing assigns the value this property returns, so it's always the default value of
	/// <typeparamref name="T"/>.
	/// </para>
	/// </remarks>
	/// <value>The left operand.</value>
	public T CurrentLeft => _left.Value;
	/// <summary>
	/// Gets the right operand of the current comparison.
	/// </summary>
	/// <remarks>
	/// <para>
	/// TODO: Nothing assigns the value this property returns, so it's always the default value of
	/// <typeparamref name="T"/>.
	/// </para>
	/// </remarks>
	/// <value>The right operand.</value>
	public T CurrentRight => _right.Value;

	/// <summary>
	/// Initializes a new <see cref="ComparingBlock{T}"/> instance with the specified script block and additional
	/// variables.
	/// </summary>
	/// <remarks>
	/// The constructor doesn't validate the script block. The caller is expected to have validated it.
	/// </remarks>
	/// <param name="scriptBlock">The script block that compares <c>$x</c> (or <c>$left</c>) with <c>$y</c> (or <c>$right</c>). This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the operands, or <see langword="null"/> for none. The constructor copies them.</param>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	internal ComparingBlock(ScriptBlock scriptBlock, IEnumerable<PSVariable>? additionalVariables)
		: base(scriptBlock, preValidated: true)
	{
		_additionalVariables = additionalVariables is null
			? Array.Empty<PSVariable>()
			: additionalVariables.AsValueEnumerable().ToArray();

		_varList = new List<PSVariable>(4);
		_left = PSComparingVariable.Left<T>();
		_right = PSComparingVariable.Right<T>();
		_compareScript = scriptBlock;
	}

	/// <summary>
	/// Compares two objects by running the script block and returns a value that indicates their relative order.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method doesn't run the script block when either operand is <see langword="null"/>. Two
	/// <see langword="null"/> operands are equal, and a <see langword="null"/> operand sorts before any other value.
	/// </para>
	/// <para>
	/// The first output of the script block is converted to an <see cref="int"/> by PowerShell's conversion rules, so
	/// the string <c>'-1'</c> means that <paramref name="left"/> sorts first. Any later output is ignored.
	/// </para>
	/// </remarks>
	/// <param name="left">The first object to compare, or <see langword="null"/>.</param>
	/// <param name="right">The second object to compare, or <see langword="null"/>.</param>
	/// <returns>
	/// A negative value if <paramref name="left"/> sorts before <paramref name="right"/>, 0 if they're equal, or a
	/// positive value if <paramref name="left"/> sorts after <paramref name="right"/>.
	/// </returns>
	/// <exception cref="ComparingScriptException">Thrown when the script block has no output, or when its first output is null or can't be converted to an <see cref="int"/>.</exception>
	/// <exception cref="RuntimeException">Thrown when the script block throws.</exception>
	public int Compare(T? left, T? right)
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
		_varList.AddRange(_additionalVariables);

		Collection<PSObject> output = _compareScript.InvokeWithContext(null, _varList, [left, right]);
		if (output.Count == 0 || !output[0].TryGetBaseObject(out object? result))
		{
			throw this.CreateNoResultException(left);
		}

		try
		{
			return LanguagePrimitives.ConvertTo<int>(result);
		}
		catch (PSInvalidCastException e)
		{
			throw ComparingScriptException.FromBlockException(e, in left, _varList);
		}
	}
	/// <summary>
	/// Compares two objects by converting them to <typeparamref name="T"/> and running the script block.
	/// </summary>
	/// <remarks>
	/// The method returns 0 without converting the objects when they're the same reference or both
	/// <see langword="null"/>. Otherwise, it converts both objects by PowerShell's conversion rules and calls
	/// <see cref="Compare(T, T)"/>.
	/// </remarks>
	/// <param name="x">The first object to compare, or <see langword="null"/>.</param>
	/// <param name="y">The second object to compare, or <see langword="null"/>.</param>
	/// <returns>
	/// A negative value if <paramref name="x"/> sorts before <paramref name="y"/>, 0 if they're equal, or a positive
	/// value if <paramref name="x"/> sorts after <paramref name="y"/>.
	/// </returns>
	/// <exception cref="InvalidCastException">Thrown when <paramref name="x"/> or <paramref name="y"/> can't be converted to <typeparamref name="T"/>.</exception>
	/// <exception cref="ComparingScriptException">Thrown when the script block has no output, or when its first output is null or can't be converted to an <see cref="int"/>.</exception>
	/// <exception cref="RuntimeException">Thrown when the script block throws.</exception>
	int IComparer.Compare(object? x, object? y)
	{
		if (ReferenceEquals(x, y))
		{
			return 0;
		}

		if (LanguagePrimitives.TryConvertTo(x, out T isX) && LanguagePrimitives.TryConvertTo(y, out T isY))
		{
			return this.Compare(isX, isY);
		}
		else
		{
			throw new InvalidCastException($"Unable to cast either x or y as {typeof(T).GetTypeName()}.");
		}
	}

	/// <summary>
	/// Creates the exception that reports a script block with no output or a <see langword="null"/> first output.
	/// </summary>
	/// <remarks>
	/// The exception records the variables of the comparison that just ran, so call this method before the next
	/// comparison changes them.
	/// </remarks>
	/// <param name="left">The first object that the script block compared.</param>
	/// <returns>A <see cref="ComparingScriptException"/> whose inner exception describes the missing result.</returns>
	private ComparingScriptException CreateNoResultException(T left)
	{
		var noResult = new PSInvalidOperationException(
			"The comparing script block returned no value or $null. It must return an [int] that is less than zero, zero, or greater than zero.");

		return ComparingScriptException.FromBlockException(noResult, in left, _varList);
	}
}
