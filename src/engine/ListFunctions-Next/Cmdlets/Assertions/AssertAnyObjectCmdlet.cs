using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using AllowsNull = System.Diagnostics.CodeAnalysis.AllowNullAttribute;
using PSAllowNull = System.Management.Automation.AllowNullAttribute;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Asserts that any element in the provided input sequence satisfies the specified condition.
/// </summary>
/// <remarks>
/// The cmdlet accepts an array of objects from the pipeline and evaluates the configured
/// <see cref="Condition"/> script block against each element. If any element satisfies the
/// condition the cmdlet writes <see langword="true"/>; otherwise it writes <see langword="false"/>.
/// When no condition is supplied the cmdlet returns <see langword="true"/> when any non-null
/// element is present in the input array.
/// </remarks>
[Cmdlet(VerbsLifecycle.Assert, "AnyObject")]
[Alias("Assert-Any", "Any-Object", "Any")]
[OutputType(typeof(bool))]
public sealed class AssertAnyObjectCmdlet : AssertObjectCmdlet
{
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
	/// <summary>
	/// Gets or sets the input objects to evaluate. The value is accepted from the pipeline.
	/// </summary>
	/// <value>
	/// An array of objects to test; may be <see langword="null"/> when no values are supplied.
	/// </value>
	public object?[]? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the condition script block used to evaluate each input item.
	/// </summary>
	/// <remarks>
	/// The script block executes with the standard automatic variables (for example <c>$_</c> and <c>$this</c>).
	/// If no condition is provided the cmdlet falls back to <see cref="ProcessWhenNoCondition"/> behavior.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/> to evaluate against each input element.</value>
	[Parameter(Position = 0)]
	[Alias("ScriptBlock", "FilterScript")]
	[PSAllowNull, AllowEmptyString, MaybeNull, AllowsNull]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public override ScriptBlock Condition
	{
		get => base.Condition;
		set => base.Condition = value;
	}
	/// <summary>
	/// Gets or sets the <see cref="ActionPreference"/> used when the condition script block throws an error.
	/// </summary>
	/// <value>The error action preference applied to script block execution. Defaults to <see cref="ActionPreference.SilentlyContinue"/>.</value>
	[Parameter, Alias("ScriptErrorAction")]
	public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

	/// <summary>
	/// Evaluates the condition filter against the configured input objects.
	/// </summary>
	/// <param name="filter">The compiled script block filter to use for evaluation. This value is not <see langword="null"/> when called.</param>
	/// <returns><see langword="true"/> when the filter finds a matching element; otherwise <see langword="false"/>.</returns>
	protected override bool Process(ScriptBlockFilter filter)
	{
		return filter.Any(this.InputObject);
	}
	/// <summary>
	/// Evaluates input objects when no condition is supplied. The default behavior returns true if any
	/// non-null element exists in the input array.
	/// </summary>
	/// <returns><see langword="true"/> when any non-null item exists; otherwise <see langword="false"/>.</returns>
	protected override bool ProcessWhenNoCondition()
	{
		if (this.InputObject is not null)
		{
			foreach (object? item in this.InputObject)
			{
				if (item is not null)
				{
					return true;
				}
			}
		}

		return false;
	}
	/// <summary>
	/// Writes the boolean script result to the output pipeline.
	/// </summary>
	/// <param name="scriptResult">The evaluation result produced by the cmdlet's logic.</param>
	protected override void End(bool scriptResult)
	{
		this.WriteObject(scriptResult);
	}
}
