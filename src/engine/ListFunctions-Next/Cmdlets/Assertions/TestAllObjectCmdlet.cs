using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Tests whether every input object satisfies a condition.
/// </summary>
/// <remarks>
/// <para>
/// The cmdlet evaluates the <see cref="Condition"/> script block against each element of <see cref="InputObject"/>,
/// across every pipeline object. It writes <see langword="true"/> when every element satisfies the condition and
/// <see langword="false"/> otherwise. When there are no elements, the cmdlet writes <see langword="true"/>, the way
/// <see cref="List{T}.TrueForAll(Predicate{T})"/> does. That's the case when the pipeline sends no input, and when
/// <see cref="InputObject"/> is <see langword="null"/> or an empty array.
/// </para>
/// <para>
/// After the first element that fails, the cmdlet writes <see langword="false"/> and stops evaluating the condition.
/// When its input comes from the pipeline, it also stops the commands that send the input, the way
/// <c>Select-Object -First</c> does.
/// </para>
/// <para>
/// The condition is required, so PowerShell rejects a <see langword="null"/> condition when it binds the parameter, as
/// it does for any mandatory parameter. That happens even when there's no input to test.
/// </para>
/// <para>
/// With the default <see cref="ScriptBlockErrorAction"/>, <see cref="ActionPreference.SilentlyContinue"/>, and with
/// <see cref="ActionPreference.Ignore"/>, the cmdlet writes errors from the condition script block as warnings. The
/// first error that the script block doesn't handle itself ends the test of an element, and the element doesn't satisfy
/// the condition, so the cmdlet writes <see langword="false"/>.
/// </para>
/// <para>
/// With any other value, such as <see cref="ActionPreference.Stop"/> or <see cref="ActionPreference.Continue"/>, errors
/// from the condition script block reach PowerShell unchanged, the way they do from a <c>ForEach-Object</c> script
/// block. With <see cref="ActionPreference.Stop"/>, an error that the script block writes ends the script that runs the
/// cmdlet, as <c>-ErrorAction Stop</c> does, and a failed method call ends only the statement. A <c>throw</c> ends the
/// script with either value, and <c>break</c> leaves the loop around the cmdlet with any value.
/// </para>
/// </remarks>
[Cmdlet(VerbsDiagnostic.Test, "AllObject")]
[Alias("Assert-AllObject", "Assert-AllObjects", "Assert-All", "All", "All-Object", "All-Objects")]
[OutputType(typeof(bool))]
public sealed class TestAllObjectCmdlet : TestObjectCmdlet
{
	/// <summary>
	/// Gets or sets the script block that tests each input object.
	/// </summary>
	/// <remarks>
	/// The script block receives the current element as <c>$_</c>, <c>$PSItem</c>, <c>$this</c>, and <c>$args[0]</c>,
	/// and must reference at least one of them. Parameter validation rejects a script block that references none,
	/// including an empty one, and one that the cmdlet can't run, such as one that has a <c>begin</c> block. Its output
	/// is converted to a <see cref="bool"/> by using PowerShell's truthiness rules.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/>. PowerShell rejects <see langword="null"/> when it binds the parameter.</value>
	[Parameter(Mandatory = true, Position = 0)]
	[Alias("ScriptBlock", "FilterScript")]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public override ScriptBlock? Condition
	{
		get => base.Condition;
		set => base.Condition = value;
	}

	/// <summary>
	/// Gets or sets the objects to test. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array. An array passed to the
	/// parameter supplies its elements, and <see langword="null"/> supplies none. The parameter can't be combined with
	/// pipeline input. <see langword="null"/> and empty-string elements are evaluated like any other element.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[PSAllowNull, AllowEmptyCollection, AllowEmptyString]
	public object? InputObject { get; set; }

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
	/// The method stops at the first element that fails the condition.
	/// </remarks>
	/// <param name="filter">The filter that tests objects with <see cref="Condition"/>.</param>
	/// <returns>
	/// <see langword="true"/> when an element fails the condition, so the test fails; otherwise,
	/// <see langword="false"/>, including when <see cref="InputObject"/> supplies no elements.
	/// </returns>
	protected override bool Process(ScriptBlockFilter filter)
	{
		return !filter.All(this.GetInputElements(this.InputObject));
	}

	/// <summary>
	/// Throws, because the cmdlet requires a condition.
	/// </summary>
	/// <remarks>
	/// PowerShell rejects a <see langword="null"/> condition when it binds the parameter, so the method runs only when
	/// <see cref="Condition"/> is set to <see langword="null"/> some other way.
	/// </remarks>
	/// <returns>The method doesn't return.</returns>
	/// <exception cref="ArgumentException">Thrown always, because no condition is set.</exception>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0009:Member access should be qualified.", Justification = "Used in nameof()")]
	protected override bool ProcessWhenNoCondition()
	{
		throw new ArgumentException("Asserting an all-true condition requires a condition to be specified.", nameof(Condition));
	}

	/// <summary>
	/// Writes the result of the test to the pipeline.
	/// </summary>
	/// <param name="scriptResult">
	/// <see langword="true"/> when a pipeline record failed the test; otherwise, <see langword="false"/>. The
	/// method writes the opposite value.
	/// </param>
	protected override void End(bool scriptResult)
	{
		this.WriteObject(!scriptResult);
	}
}
