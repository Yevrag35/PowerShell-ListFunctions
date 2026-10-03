using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Asserts that every input object satisfies a condition.
/// </summary>
/// <remarks>
/// <para>
/// The cmdlet evaluates the <see cref="Condition"/> script block against each element of <see cref="InputObject"/>,
/// across every pipeline record. It writes <see langword="true"/> when every element satisfies the condition and
/// <see langword="false"/> otherwise. A <see langword="null"/> or empty <see cref="InputObject"/> array fails the
/// assertion. When the pipeline sends no input at all, the cmdlet writes <see langword="true"/>.
/// </para>
/// <para>
/// After the first element that fails, the cmdlet writes <see langword="false"/> and stops evaluating the condition.
/// When its input comes from the pipeline, it also stops the commands that send the input, the way
/// <c>Select-Object -First</c> does.
/// </para>
/// <para>
/// The condition is required. When <see cref="Condition"/> is <see langword="null"/>, the cmdlet ends with a
/// terminating error that wraps an <see cref="ArgumentException"/>. A terminating error thrown by the condition script
/// block also ends the cmdlet with a terminating error.
/// </para>
/// </remarks>
[Cmdlet(VerbsLifecycle.Assert, "AllObject")]
[Alias("Assert-AllObjects", "Assert-All", "All", "All-Object", "All-Objects")]
[OutputType(typeof(bool))]
public sealed class AssertAllObjectsCmdlet : AssertObjectCmdlet
{
	/// <summary>
	/// Gets or sets the script block that tests each input object.
	/// </summary>
	/// <remarks>
	/// The script block receives the current element as <c>$_</c>, <c>$PSItem</c>, <c>$this</c>, and <c>$args[0]</c>,
	/// and must reference at least one of them. Parameter validation rejects a script block that references none,
	/// including an empty one. Its output is converted to a <see cref="bool"/> by using PowerShell's truthiness rules.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/>. A <see langword="null"/> value passes parameter binding but produces a terminating error.</value>
	[Parameter(Mandatory = true, Position = 0)]
	[Alias("ScriptBlock", "FilterScript")]
	[PSAllowNull, AllowEmptyString]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public override ScriptBlock? Condition
	{
		get => base.Condition;
		set => base.Condition = value;
	}

	/// <summary>
	/// Gets or sets the objects to test. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// <see langword="null"/> and empty-string elements are evaluated like any other element.
	/// </remarks>
	/// <value>The array of objects to test, or <see langword="null"/>. A <see langword="null"/> or empty array fails the assertion.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[PSAllowNull, AllowEmptyCollection, AllowEmptyString]
	public object?[]? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the error action preference applied while the condition script block runs.
	/// </summary>
	/// <remarks>
	/// The value is assigned to <c>$ErrorActionPreference</c> in the script block's scope. It controls how
	/// non-terminating errors written by the script block are handled and doesn't change the cmdlet's own
	/// <c>-ErrorAction</c> behavior.
	/// </remarks>
	/// <value>The error action preference for script block execution. Defaults to <see cref="ActionPreference.SilentlyContinue"/>.</value>
	[Parameter]
	public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

	/// <summary>
	/// Tests the elements of the current <see cref="InputObject"/> array with the condition.
	/// </summary>
	/// <remarks>
	/// The method stops at the first element that fails the condition.
	/// </remarks>
	/// <param name="filter">The filter that tests objects with <see cref="Condition"/>.</param>
	/// <returns>
	/// <see langword="true"/> when an element fails the condition or the array is <see langword="null"/> or empty, so
	/// the assertion fails; otherwise, <see langword="false"/>.
	/// </returns>
	protected override bool Process(ScriptBlockFilter filter)
	{
		return !filter.All(this.InputObject);
	}

	/// <summary>
	/// Throws, because the cmdlet requires a condition.
	/// </summary>
	/// <returns>The method doesn't return.</returns>
	/// <exception cref="ArgumentException">Thrown always, because no condition is set.</exception>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0009:Member access should be qualified.", Justification = "Used in nameof()")]
	protected override bool ProcessWhenNoCondition()
	{
		throw new ArgumentException("Asserting an all-true condition requires a condition to be specified.", nameof(Condition));
	}

	/// <summary>
	/// Writes the result of the assertion to the pipeline.
	/// </summary>
	/// <param name="scriptResult">
	/// <see langword="true"/> when a pipeline record failed the assertion; otherwise, <see langword="false"/>. The
	/// method writes the opposite value.
	/// </param>
	protected override void End(bool scriptResult)
	{
		this.WriteObject(!scriptResult);
	}
}
