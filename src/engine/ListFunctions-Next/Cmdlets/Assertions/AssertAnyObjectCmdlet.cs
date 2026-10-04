using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Asserts that at least one input object satisfies a condition.
/// </summary>
/// <remarks>
/// <para>
/// The cmdlet evaluates the <see cref="Condition"/> script block against each element of <see cref="InputObject"/>,
/// across every pipeline object. It writes <see langword="true"/> when any element satisfies the condition and
/// <see langword="false"/> otherwise.
/// </para>
/// <para>
/// When <see cref="Condition"/> is omitted or <see langword="null"/>, the cmdlet writes <see langword="true"/> when
/// any element isn't <see langword="null"/>.
/// </para>
/// <para>
/// After the first element that satisfies the test, the cmdlet writes <see langword="true"/> and stops evaluating.
/// When its input comes from the pipeline, it also stops the commands that send the input, the way
/// <c>Select-Object -First</c> does.
/// </para>
/// <para>
/// Errors from the condition script block reach PowerShell unchanged, the way they do from a <c>ForEach-Object</c>
/// script block. When <see cref="ScriptBlockErrorAction"/> is <see cref="ActionPreference.Stop"/>, an error that the
/// script block writes ends the script that runs the cmdlet, as <c>-ErrorAction Stop</c> does. A <c>throw</c> does too
/// unless the errors are suppressed. A failed method call ends only the statement, and <c>break</c> leaves the loop
/// around the cmdlet.
/// </para>
/// </remarks>
[Cmdlet(VerbsLifecycle.Assert, "AnyObject")]
[Alias("Assert-Any", "Any-Object", "Any")]
[OutputType(typeof(bool))]
public sealed class AssertAnyObjectCmdlet : AssertObjectCmdlet
{
	/// <summary>
	/// Gets or sets the objects to test. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none. <see langword="null"/> and
	/// empty-string elements are evaluated like any other element when a condition is set.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
	public object? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the script block that tests each input object.
	/// </summary>
	/// <remarks>
	/// The script block receives the current element as <c>$_</c>, <c>$PSItem</c>, <c>$this</c>, and <c>$args[0]</c>,
	/// and must reference at least one of them. Parameter validation rejects a script block that references none,
	/// including an empty one. Its output is converted to a <see cref="bool"/> by using PowerShell's truthiness rules.
	/// Without a condition, the cmdlet tests whether any element isn't <see langword="null"/>.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/>, or <see langword="null"/> when none is set.</value>
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
	/// Gets or sets the error action preference applied while the condition script block runs.
	/// </summary>
	/// <remarks>
	/// The value is assigned to <c>$ErrorActionPreference</c> in the script block's scope. It controls how
	/// non-terminating errors written by the script block are handled and doesn't change the cmdlet's own
	/// <c>-ErrorAction</c> behavior.
	/// </remarks>
	/// <value>The error action preference for script block execution. Defaults to <see cref="ActionPreference.SilentlyContinue"/>.</value>
	[Parameter, Alias("ScriptErrorAction")]
	public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

	/// <summary>
	/// Tests the elements of the current <see cref="InputObject"/> with the condition.
	/// </summary>
	/// <remarks>
	/// The method stops at the first element that satisfies the condition.
	/// </remarks>
	/// <param name="filter">The filter that tests objects with <see cref="Condition"/>.</param>
	/// <returns><see langword="true"/> when an element satisfies the condition; otherwise, <see langword="false"/>.</returns>
	protected override bool Process(ScriptBlockFilter filter)
	{
		return filter.Any(this.GetInputElements(this.InputObject));
	}
	/// <summary>
	/// Determines whether the current <see cref="InputObject"/> has an element that isn't <see langword="null"/>.
	/// </summary>
	/// <returns><see langword="true"/> when an element isn't <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
	protected override bool ProcessWhenNoCondition()
	{
		foreach (object? item in this.GetInputElements(this.InputObject))
		{
			if (item is not null)
			{
				return true;
			}
		}

		return false;
	}
	/// <summary>
	/// Writes the result of the assertion to the pipeline.
	/// </summary>
	/// <param name="scriptResult"><see langword="true"/> when a pipeline record contained a matching element; otherwise, <see langword="false"/>.</param>
	protected override void End(bool scriptResult)
	{
		this.WriteObject(scriptResult);
	}
}
