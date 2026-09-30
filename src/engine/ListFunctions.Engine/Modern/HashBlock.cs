using ListFunctions.Internal;
using ListFunctions.Modern.Exceptions;
using ListFunctions.Modern.Variables;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace ListFunctions.Modern;

/// <summary>
/// Represents a script-based hash code provider that computes hash codes for objects using a PowerShell script block
/// and optional variable context.
/// </summary>
/// <remarks>Use this class to customize hash code generation for objects by supplying a PowerShell script block
/// that defines the hash logic. The script block can access the target object and additional variables, enabling
/// advanced or domain-specific hash code strategies.</remarks>
public sealed class HashBlock : ComparingBase, IHashBlock
{
	private readonly PSThisVariable _thisVar;
	private readonly List<PSVariable> _varList;

	/// <summary>
	/// Initializes a new instance of the <see cref="HashBlock"/> class using the specified script block.
	/// </summary>
	/// <param name="scriptBlock">The ScriptBlock to associate with this HashBlock. Cannot be null.</param>
	public HashBlock(ScriptBlock scriptBlock) : base(scriptBlock, preValidated: false)
	{
		_thisVar = new();
		_varList = new(4);
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="HashBlock"/> class with the specified script block and optional variable list.
	/// </summary>
	/// <param name="scriptBlock">The script block to be executed by this HashBlock. Cannot be null.</param>
	/// <param name="variables">An optional list of variables to be used within the script block. If null, an empty list is used.</param>
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
	/// The script block receives <paramref name="obj"/> as <c>$_</c>, <c>$this</c>, and <c>$PSItem</c>. Only its first
	/// output is used, and that output is converted to an <see cref="int"/> by PowerShell's conversion rules, so the
	/// string <c>'42'</c> gives the hash code 42.
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
	/// <param name="obj">The object to pass to the script block as <c>$_</c>, <c>$this</c>, and <c>$PSItem</c>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object, or <see langword="null"/> for none.</param>
	/// <returns>The first output of the script block.</returns>
	/// <exception cref="HashCodeScriptException">Thrown when the script block throws, or when it has no output or its first output is null.</exception>
	private object? GetHashObject(object obj, IEnumerable<PSVariable>? additionalVariables)
	{
		List<PSVariable> variables = this.SetContextVariables(obj, additionalVariables);
		if (!this.Script.TryInvokeWithContext(variables, out object? hashObj, out Exception? exception))
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
	/// Prepares and returns a list of context variables for use in PowerShell script execution.
	/// </summary>
	/// <param name="obj">The object to assign as the value of the special context variable. May be null.</param>
	/// <param name="additionalVariables">An optional collection of additional PowerShell variables to include in the context. If null, no additional
	/// variables are added.</param>
	/// <returns>A list of PowerShell variables representing the current script context, including the special context variable
	/// and any additional variables provided.</returns>
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
