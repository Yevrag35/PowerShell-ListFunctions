using ListFunctions.Extensions;
using ListFunctions.Modern.Exceptions;
using ListFunctions.Modern.Variables;

namespace ListFunctions.Modern;

/// <summary>
/// Represents a hash code provider that computes the hash codes of objects by running a PowerShell script block.
/// </summary>
/// <remarks>
/// <para>
/// The script block sees the object to hash as <c>$_</c>, <c>$this</c>, <c>$PSItem</c>, and <c>$args[0]</c>, along
/// with any additional variables that the caller passes to <see cref="GetHashCode(object, IEnumerable{PSVariable})"/>.
/// </para>
/// <para>
/// Instances aren't thread-safe, because every call reuses the same list of script block variables.
/// </para>
/// </remarks>
public sealed class HashBlock : ComparingBase, IHashBlock
{
	private readonly PSThisVariable _thisVar;
	private readonly List<PSVariable> _varList;

	/// <summary>
	/// Initializes a new <see cref="HashBlock"/> instance with the specified script block.
	/// </summary>
	/// <param name="scriptBlock">The script block that computes the hash code of <c>$_</c>. This value must not be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="scriptBlock"/> contains no statements.</exception>
	public HashBlock(ScriptBlock scriptBlock) : base(scriptBlock, preValidated: false)
	{
		_thisVar = new();
		_varList = new(4);
	}
	/// <summary>
	/// Initializes a new <see cref="HashBlock"/> instance with the specified script block and variable list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The instance keeps <paramref name="variables"/> without copying it and uses it as its working list of script
	/// block variables. Every call to <see cref="GetHashCode(object, IEnumerable{PSVariable})"/> clears and refills the
	/// list, so the variables it holds beforehand never reach the script block. Pass variables to
	/// <see cref="GetHashCode(object, IEnumerable{PSVariable})"/> instead.
	/// </para>
	/// <para>
	/// TODO: Clarify whether the variables in <paramref name="variables"/> are meant to reach the script block.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block that computes the hash code of <c>$_</c>. This value must not be <see langword="null"/>.</param>
	/// <param name="variables">The list to use as the working list of script block variables, or <see langword="null"/> to create one.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="scriptBlock"/> contains no statements.</exception>
	public HashBlock(ScriptBlock scriptBlock, List<PSVariable>? variables) : base(scriptBlock, preValidated: false)
	{
		_varList = variables ?? new(4);
		_thisVar = new();
	}

	/// <summary>
	/// Computes the hash code of the specified object by running the hash code script block.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The script block receives <paramref name="obj"/> as <c>$_</c>, <c>$this</c>, <c>$PSItem</c>, and <c>$args[0]</c>.
	/// Only its first output is used, and that output is converted to an <see cref="int"/> by PowerShell's conversion
	/// rules, so the string <c>'42'</c> gives the hash code 42.
	/// </para>
	/// <para>
	/// The method isn't thread-safe, because every call reuses the same list of script block variables.
	/// </para>
	/// </remarks>
	/// <param name="obj">The object to compute the hash code of. This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object, or <see langword="null"/> for none.</param>
	/// <returns>The first output of the script block, converted to an <see cref="int"/>.</returns>
	/// <exception cref="HashCodeScriptException">Thrown when <paramref name="obj"/> is null, when the script block throws, or when its first output is missing, null, or can't be converted to an <see cref="int"/>.</exception>
	public int GetHashCode([DisallowNull] object obj, IEnumerable<PSVariable>? additionalVariables)
	{
		if (obj is null)
		{
			var argNull = new ArgumentNullException(nameof(obj));
			throw HashCodeScriptException.FromBlockException(argNull, obj);
		}

		object? hashObj = this.GetHashObject(obj, additionalVariables);
		try
		{
			return LanguagePrimitives.ConvertTo<int>(hashObj);
		}
		catch (PSInvalidCastException e)
		{
			throw HashCodeScriptException.FromBlockException(e, obj, this.SetContextVariables(obj, additionalVariables));
		}
	}

	/// <summary>
	/// Runs the hash code script block for the specified object and returns the script block's first output.
	/// </summary>
	/// <remarks>
	/// The method returns only when the script block succeeds and its first output isn't <see langword="null"/>. The
	/// caller converts that output to the hash code.
	/// </remarks>
	/// <param name="obj">The object to pass to the script block as <c>$_</c>, <c>$this</c>, <c>$PSItem</c>, and <c>$args[0]</c>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object, or <see langword="null"/> for none.</param>
	/// <returns>The first output of the script block.</returns>
	/// <exception cref="HashCodeScriptException">Thrown when the script block throws, or when it has no output or its first output is null.</exception>
	private object? GetHashObject(object obj, IEnumerable<PSVariable>? additionalVariables)
	{
		List<PSVariable> variables = this.SetContextVariables(obj, additionalVariables);
		if (!this.Script.TryInvokeWithContext(variables, [obj], out object? hashObj, out Exception? exception))
		{
			if (exception is null)
			{
				return this.ThrowNullHashCode(obj, additionalVariables);
			}

			// InvokeWithContext removes $_ and $this from the list it's given, so the list is rebuilt for the exception.
			throw HashCodeScriptException.FromBlockException(exception, obj, this.SetContextVariables(obj, additionalVariables));
		}

		return hashObj;
	}

	/// <summary>
	/// Rebuilds the list of script block variables for the specified object.
	/// </summary>
	/// <param name="obj">The object to expose as <c>$_</c>, <c>$this</c>, and <c>$PSItem</c>, or <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to append after the object's variables, or <see langword="null"/> for none.</param>
	/// <returns>The shared variable list, which holds the object's variables followed by <paramref name="additionalVariables"/>.</returns>
	private List<PSVariable> SetContextVariables(object? obj, IEnumerable<PSVariable>? additionalVariables)
	{
		_varList.Clear();
		_thisVar.SetValue(obj);
		_thisVar.InsertIntoList(_varList);

		if (additionalVariables is not null)
		{
			_varList.AddRange(additionalVariables);
		}

		return _varList;
	}

	/// <summary>
	/// Throws the exception that reports a script block with no output or a <see langword="null"/> first output.
	/// </summary>
	/// <remarks>
	/// The method never returns. Its return type lets callers use it in a <see langword="return"/> statement.
	/// </remarks>
	/// <param name="obj">The object whose hash code the script block failed to compute.</param>
	/// <param name="additionalVariables">The additional variables that were passed to the script block, or <see langword="null"/> for none.</param>
	/// <returns>The method doesn't return.</returns>
	/// <exception cref="HashCodeScriptException">Always thrown. Its inner exception is a <see cref="RuntimeException"/> that wraps an <see cref="ArgumentOutOfRangeException"/>.</exception>
	[DoesNotReturn]
	private object? ThrowNullHashCode(object? obj, IEnumerable<PSVariable>? additionalVariables)
	{
		var baseEx = new ArgumentOutOfRangeException(nameof(obj), "The hash code script block returned a null value when an non-null one was expected.");

		string? objStr = obj?.ToString();
		var rec = new ErrorRecord(baseEx, "NullObjHashCode", ErrorCategory.InvalidResult, obj);
		rec.CategoryInfo.Activity = "GetHashCode(T obj, IEnumerable<PSVariable> additionalVariables)";
		rec.CategoryInfo.Reason = "A hash code script block should never return a 'null' value.";
		rec.CategoryInfo.TargetName = objStr ?? string.Empty;

		var inner = new RuntimeException($"Unable to retrieve an integer hash code from object, \"{objStr}\", using the supplied script block.", baseEx, rec);

		throw HashCodeScriptException.FromBlockException(inner, in obj,
			this.SetContextVariables(obj, additionalVariables));
	}
}
