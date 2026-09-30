using ListFunctions.Internal;
using ListFunctions.Modern.Variables;

#nullable enable

namespace ListFunctions.Modern;

/// <summary>
/// Represents a predicate, written as a PowerShell script block, that tests objects and collections.
/// </summary>
/// <remarks>
/// <para>
/// The script block sees the object under test as <c>$_</c>, <c>$this</c>, <c>$PSItem</c>, and <c>$args[0]</c>. Its
/// first output is converted to a <see cref="bool"/> by PowerShell's rules, so any output that PowerShell treats as
/// true passes. A script block with no output fails the test.
/// </para>
/// <para>
/// Instances aren't thread-safe, because every test reuses the same list of script block variables.
/// </para>
/// </remarks>
public sealed class ScriptBlockFilter
{
	private readonly PSThisVariable _constants;
	private readonly List<PSVariable> _extraVariables;
	private readonly ScriptBlock _scriptBlock;
	private readonly List<PSVariable> _variables;

	/// <summary>
	/// Initializes a new <see cref="ScriptBlockFilter"/> instance with the specified script block and additional
	/// variables.
	/// </summary>
	/// <param name="scriptBlock">The script block that tests each object. This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object under test. The constructor copies them.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	public ScriptBlockFilter(ScriptBlock scriptBlock, params
#if NET9_0_OR_GREATER
				ReadOnlySpan<PSVariable>
#else
			PSVariable[]
#endif
			additionalVariables)
	{
		Guard.NotNull(scriptBlock);
		_scriptBlock = scriptBlock;
		_extraVariables = new();
		_extraVariables.AddRange(additionalVariables
#if !NET9_0_OR_GREATER
			?? Array.Empty<PSVariable>()
#endif
		);
		_variables = new();
		_constants = new();
	}

	/// <summary>
	/// Rebuilds the list of script block variables for the specified object under test.
	/// </summary>
	/// <param name="value">The object to expose as <c>$_</c>, <c>$this</c>, and <c>$PSItem</c>.</param>
	/// <returns>The shared variable list, which holds the object's variables followed by the additional variables.</returns>
	private List<PSVariable> InitializeContext(object? value)
	{
		_variables.Clear();
		_constants.SetValue(value);
		_constants.InsertIntoList(_variables);
		_variables.AddRange(_extraVariables);
		return _variables;
	}

	/// <summary>
	/// Determines whether every element of the specified collection passes the test.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="System.Linq.Enumerable.All{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>, the method returns
	/// <see langword="false"/> for an empty collection. It stops at the first element that fails.
	/// </remarks>
	/// <param name="collection">The collection to test, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if <paramref name="collection"/> has at least one element and every element passes; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws.</exception>
	public bool All(ICollection? collection)
	{
		if (collection is null || collection.Count == 0)
			return false;

		foreach (object? item in collection)
		{
			if (!this.IsTrue(item))
				return false;
		}

		return true;
	}
	/// <summary>
	/// Determines whether any element of the specified collection passes the test.
	/// </summary>
	/// <remarks>
	/// The method stops at the first element that passes.
	/// </remarks>
	/// <param name="collection">The collection to test, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if at least one element of <paramref name="collection"/> passes; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws.</exception>
	public bool Any(ICollection? collection)
	{
		if (collection is null || collection.Count == 0)
			return false;

		foreach (object? item in collection)
		{
			if (this.IsTrue(item))
				return true;
		}

		return false;
	}
	/// <summary>
	/// Determines whether the specified object passes the test.
	/// </summary>
	/// <param name="value">The object to test, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if the first output of the script block is true by PowerShell's rules; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws.</exception>
	public bool IsTrue(object? value)
	{
		List<PSVariable> variables = this.InitializeContext(value);

		return _scriptBlock.InvokeWithContext(
			variables: variables,
			args: [value],
			selectAs: LanguagePrimitives.IsTrue);
	}
}