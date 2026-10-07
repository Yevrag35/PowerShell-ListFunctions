using ListFunctions.Extensions;
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
/// By default, an exception from the script block reaches the caller. A filter created with an error handler passes
/// the error of a <see cref="RuntimeException"/> to the handler instead, and the object fails the test. To have every
/// error reported this way, define <c>$ErrorActionPreference</c> as <see cref="ActionPreference.Stop"/> in the
/// additional variables: an error that the script block doesn't handle itself, including one that a command writes, then
/// ends the script block as a <see cref="RuntimeException"/>.
/// </para>
/// <para>
/// Instances aren't thread-safe, because every test reuses the same list of script block variables.
/// </para>
/// </remarks>
public sealed class ScriptBlockFilter
{
	private readonly PSThisVariable _constants;
	private readonly Action<ErrorRecord>? _errorHandler;
	private readonly List<PSVariable> _extraVariables;
	private readonly ScriptBlock _scriptBlock;
	private readonly List<PSVariable> _variables;

	/// <summary>
	/// Initializes a new <see cref="ScriptBlockFilter"/> instance with the specified script block and additional
	/// variables.
	/// </summary>
	/// <remarks>
	/// An exception from the script block reaches the caller of the test.
	/// </remarks>
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
		ArgumentNullException.ThrowIfNull(scriptBlock);
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
	/// Initializes a new <see cref="ScriptBlockFilter"/> instance with the specified script block, error handler, and
	/// additional variables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When the script block throws a <see cref="RuntimeException"/> while it tests an object, the filter passes the
	/// exception's <see cref="RuntimeException.ErrorRecord"/> to <paramref name="errorHandler"/>, and the object fails the
	/// test. For an <see cref="ActionPreferenceStopException"/>, that's the record of the error that stopped the script
	/// block. A collection test goes on with the next element.
	/// </para>
	/// <para>
	/// A <see cref="PipelineStoppedException"/>, which means that PowerShell is stopping the pipeline, still reaches the
	/// caller, and so does any exception that isn't a <see cref="RuntimeException"/>, such as the
	/// <see cref="FlowControlException"/> of a <c>break</c> statement. So does an exception from
	/// <paramref name="errorHandler"/> itself.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block that tests each object. This value must not be <see langword="null"/>.</param>
	/// <param name="errorHandler">The method that receives the error of each object whose test fails with an error. This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object under test. The constructor copies them.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> or <paramref name="errorHandler"/> is null.</exception>
	public ScriptBlockFilter(ScriptBlock scriptBlock, Action<ErrorRecord> errorHandler, params
#if NET9_0_OR_GREATER
				ReadOnlySpan<PSVariable>
#else
			PSVariable[]
#endif
			additionalVariables)
		: this(scriptBlock, additionalVariables)
	{
		ArgumentNullException.ThrowIfNull(errorHandler);
		_errorHandler = errorHandler;
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
	/// Like <see cref="System.Linq.Enumerable.All{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/> and
	/// <see cref="List{T}.TrueForAll(Predicate{T})"/>, the method returns <see langword="true"/> for an empty collection,
	/// without running the script block, and it treats <see langword="null"/> as an empty collection. It stops at the
	/// first element that fails, including one whose test fails with an error that the error handler receives.
	/// </remarks>
	/// <param name="collection">The collection to test, or <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if every element of <paramref name="collection"/> passes, including when it has no elements
	/// or is <see langword="null"/>; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws, and the filter has no error handler or the exception is a PipelineStoppedException.</exception>
	/// <exception cref="FlowControlException">Thrown when the script block runs a statement such as <c>break</c> that leaves it.</exception>
	public bool All(ICollection? collection)
	{
		if (collection is null)
			return true;

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
	/// The method stops at the first element that passes. An element whose test fails with an error that the error
	/// handler receives doesn't pass, so the method goes on with the next one.
	/// </remarks>
	/// <param name="collection">The collection to test, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if at least one element of <paramref name="collection"/> passes; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws, and the filter has no error handler or the exception is a PipelineStoppedException.</exception>
	/// <exception cref="FlowControlException">Thrown when the script block runs a statement such as <c>break</c> that leaves it.</exception>
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
	/// <remarks>
	/// When the filter has an error handler and the script block throws a <see cref="RuntimeException"/> other than a
	/// <see cref="PipelineStoppedException"/>, the method passes the exception's error record to the handler and returns
	/// <see langword="false"/>.
	/// </remarks>
	/// <param name="value">The object to test, or <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if the first output of the script block is true by PowerShell's rules; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws, and the filter has no error handler or the exception is a PipelineStoppedException.</exception>
	/// <exception cref="FlowControlException">Thrown when the script block runs a statement such as <c>break</c> that leaves it.</exception>
	public bool IsTrue(object? value)
	{
		List<PSVariable> variables = this.InitializeContext(value);

		try
		{
			return _scriptBlock.InvokeWithContext(
				variables: variables,
				args: [value],
				selectAs: LanguagePrimitives.IsTrue);
		}
		catch (RuntimeException e) when (_errorHandler is not null && e is not PipelineStoppedException)
		{
			_errorHandler(e.ErrorRecord);
			return false;
		}
	}
}