using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Tests whether at least one input object satisfies a condition.
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
/// With the default <see cref="ScriptBlockErrorAction"/>, <see cref="ActionPreference.SilentlyContinue"/>, and with
/// <see cref="ActionPreference.Ignore"/>, the cmdlet writes errors from the condition script block as warnings. The
/// first error that the script block doesn't handle itself ends the test of an element, and the element doesn't satisfy
/// the condition.
/// </para>
/// <para>
/// With any other value, such as <see cref="ActionPreference.Stop"/> or <see cref="ActionPreference.Continue"/>, errors
/// from the condition script block reach PowerShell unchanged, the way they do from a <c>ForEach-Object</c> script
/// block. With <see cref="ActionPreference.Stop"/>, an error that the script block writes ends the script that runs the
/// cmdlet, as <c>-ErrorAction Stop</c> does, and a failed method call ends only the statement. A <c>throw</c> ends the
/// script with either value, and <c>break</c> leaves the loop around the cmdlet with any value.
/// </para>
/// </remarks>
[Cmdlet(VerbsDiagnostic.Test, "AnyObject")]
[Alias("Assert-AnyObject", "Assert-Any", "Any-Object", "Any")]
[OutputType(typeof(bool))]
public sealed class TestAnyObjectCmdlet : TestObjectCmdlet
{
	/// <summary>
	/// Gets or sets the objects to test. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none. The parameter can't be combined with
	/// pipeline input. <see langword="null"/> and empty-string elements are evaluated like any other element when a
	/// condition is set.
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
	/// including an empty one, and one that the cmdlet can't run, such as one that has a <c>begin</c> block. Its output
	/// is converted to a <see cref="bool"/> by using PowerShell's truthiness rules. Without a condition, the cmdlet tests
	/// whether any element isn't <see langword="null"/>, and <see langword="null"/> is the same as no condition.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/>, or <see langword="null"/> when none is set.</value>
	[Parameter(Position = 0)]
	[Alias("ScriptBlock", "FilterScript")]
	[PSAllowNull, AllowEmptyString, MaybeNull, AllowsNull]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public override ScriptBlock Condition
	{
		get => base.Condition;
		set => base.Condition = value;
	}
	/// <summary>
	/// Gets or sets the error action preference that decides what happens to errors in the condition script block.
	/// </summary>
	/// <remarks>
	/// <see cref="ActionPreference.SilentlyContinue"/> and <see cref="ActionPreference.Ignore"/> turn the errors into
	/// warnings: the script block runs with <c>$ErrorActionPreference</c> set to <see cref="ActionPreference.Stop"/>, and
	/// the cmdlet writes the message of the first error that the script block doesn't handle itself as a warning. Any
	/// other value is assigned to <c>$ErrorActionPreference</c> in the script block's scope. The value doesn't change the
	/// cmdlet's own <c>-ErrorAction</c> behavior.
	/// </remarks>
	/// <value>The error action preference for the condition script block. Defaults to <see cref="ActionPreference.SilentlyContinue"/>.</value>
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
	/// Writes the result of the test to the pipeline.
	/// </summary>
	/// <param name="scriptResult"><see langword="true"/> when a pipeline record contained a matching element; otherwise, <see langword="false"/>.</param>
	protected override void End(bool scriptResult)
	{
		this.WriteObject(scriptResult);
	}
}
