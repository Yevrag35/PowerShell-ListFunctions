using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Asserts that all elements in the provided input sequence satisfy the specified condition.
/// </summary>
/// <remarks>
/// The cmdlet evaluates the configured <see cref="Condition"/> script block against each element of
/// <see cref="InputObject"/>. If every element satisfies the condition the cmdlet writes <see langword="true"/>;
/// otherwise it writes <see langword="false"/>. A condition is required for this cmdlet; attempting to run
/// without a condition results in an <see cref="ArgumentException"/>.
/// </remarks>
[Cmdlet(VerbsLifecycle.Assert, "AllObject")]
[Alias("Assert-AllObjects", "Assert-All", "All", "All-Object", "All-Objects")]
[OutputType(typeof(bool))]
public sealed class AssertAllObjectsCmdlet : AssertObjectCmdlet
{
	/// <summary>
	/// Gets or sets the condition script block applied to each input object.
	/// </summary>
	/// <value>The condition <see cref="ScriptBlock"/> to evaluate; must not be empty for this cmdlet.</value>
	[Parameter(Mandatory = true, Position = 0)]
	[Alias("ScriptBlock", "FilterScript")]
	[AllowNull, AllowEmptyString]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public override ScriptBlock? Condition
	{
		get => base.Condition;
		set => base.Condition = value;
	}

	/// <summary>
	/// Gets or sets the input sequence to evaluate.
	/// </summary>
	/// <value>An array of objects to test; may be empty or contain nulls.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[AllowNull, AllowEmptyCollection, AllowEmptyString]
	public object?[]? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the <see cref="ActionPreference"/> used when the condition script block throws an error.
	/// </summary>
	/// <value>The error action preference applied to script block execution. Defaults to <see cref="ActionPreference.SilentlyContinue"/>.</value>
	[Parameter]
	public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

	/// <summary>
	/// Evaluates the condition against the input objects and returns whether a non-matching element exists.
	/// </summary>
	/// <param name="filter">The compiled filter to execute.</param>
	/// <returns><see langword="true"/> when processing should continue (a non-matching element was found); otherwise <see langword="false"/>.</returns>
	protected override bool Process(ScriptBlockFilter filter)
	{
		return !filter.All(this.InputObject);
	}

	/// <summary>
	/// Throws when no condition is supplied because this cmdlet requires an explicit condition.
	/// </summary>
	/// <returns>Never returns; always throws <see cref="ArgumentException"/>.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0009:Member access should be qualified.", Justification = "Used in nameof()")]
	protected override bool ProcessWhenNoCondition()
	{
		throw new ArgumentException("Asserting an all-true condition requires a condition to be specified.", nameof(Condition));
	}

	/// <summary>
	/// Writes the final boolean result to the output pipeline.
	/// </summary>
	/// <param name="scriptResult">The intermediate evaluation result (true when a non-matching element was found).</param>
	protected override void End(bool scriptResult)
	{
		this.WriteObject(!scriptResult);
	}
}
