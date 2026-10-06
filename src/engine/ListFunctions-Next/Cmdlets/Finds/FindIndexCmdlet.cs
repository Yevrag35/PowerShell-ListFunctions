using ListFunctions.Components;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Finds;

/// <summary>
/// Finds the zero-based index of the first input element that satisfies a condition.
/// </summary>
/// <remarks>
/// <para>
/// The cmdlet evaluates the <see cref="Condition"/> script block against each element in input order and writes the
/// index of the first element for which the script block returns a value that PowerShell treats as
/// <see langword="true"/>. When no element matches, the cmdlet writes -1. The behavior mirrors
/// <see cref="List{T}.FindIndex(Predicate{T})"/>.
/// </para>
/// <para>
/// The index counts every element of the input, so it's the position of the match in the full input sequence. Each
/// pipeline object is one element, even when it's <see langword="null"/> or an array, and an array passed to
/// <see cref="InputObject"/> supplies its elements.
/// </para>
/// <para>
/// After the first match, the cmdlet writes the index and stops evaluating the condition. When its input comes from
/// the pipeline, it also stops the commands that send the input, the way <c>Select-Object -First</c> does. Those
/// commands don't run their end blocks.
/// </para>
/// <para>
/// Errors from the condition script block reach PowerShell unchanged, the way they do from a <c>ForEach-Object</c>
/// script block. When <see cref="ScriptBlockErrorAction"/> is <see cref="ActionPreference.Stop"/>, an error that the
/// script block writes ends the script that runs the cmdlet, as <c>-ErrorAction Stop</c> does. A <c>throw</c> does too
/// unless the errors are suppressed. A failed method call ends only the statement, and <c>break</c> leaves the loop
/// around the cmdlet.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.Find, "IndexOf")]
[Alias("Find-Index", "IndexOf")]
[OutputType(typeof(int))]
public sealed class FindIndexCmdlet : ListFunctionCmdletBase
{
	private ScriptBlockFilter _filter = null!;
	private int _currentIndex;

	/// <summary>
	/// Gets or sets the script block that tests each input element.
	/// </summary>
	/// <remarks>
	/// The script block receives the current element as <c>$_</c>, <c>$PSItem</c>, <c>$this</c>, and <c>$args[0]</c>,
	/// and must reference at least one of them. Parameter validation rejects a script block that references none, and
	/// one that the cmdlet can't run, such as one that has a <c>begin</c> block. The script block's output is converted
	/// to a <see cref="bool"/> by using PowerShell's truthiness rules.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/> to evaluate against each input element. PowerShell rejects <see langword="null"/> when it binds the parameter.</value>
	[Parameter(Mandatory = true, Position = 0)]
	[Alias("ScriptBlock")]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public ScriptBlock Condition { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to search. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array, and continues the
	/// sequence from the previous objects. An array passed to the parameter supplies its elements, and
	/// <see langword="null"/> supplies none. <see langword="null"/> and empty-string elements are evaluated like any
	/// other element.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[Alias("List")]
	[AllowEmptyCollection, AllowEmptyString, PSAllowNull]
	public object? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the error action preference applied while the condition script block runs.
	/// </summary>
	/// <remarks>
	/// The value is assigned to <c>$ErrorActionPreference</c> in the script block's scope. It controls how
	/// non-terminating errors written by the script block are handled and does not change the cmdlet's own
	/// <c>-ErrorAction</c> behavior.
	/// </remarks>
	/// <value>The error action preference for script block execution. Defaults to <see cref="ActionPreference.SilentlyContinue"/>.</value>
	[Parameter, Alias("ScriptErrorAction")]
	public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

	/// <summary>
	/// Creates the filter that evaluates <see cref="Condition"/> with the configured <see cref="ScriptBlockErrorAction"/>.
	/// </summary>
	protected override void BeginCore()
	{
		_filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
	}
	/// <summary>
	/// Evaluates the elements of the current <see cref="InputObject"/> and advances the running index.
	/// </summary>
	/// <remarks>
	/// When an element matches, the running index is set to that element's position in the full input sequence.
	/// Otherwise, the running index advances by the number of elements, which is 1 for a pipeline object.
	/// </remarks>
	/// <returns><see langword="false"/> when an element matches and processing stops; otherwise <see langword="true"/>.</returns>
	protected override bool ProcessCore()
	{
		object?[] elements = this.GetInputElements(this.InputObject);
		for (int i = 0; i < elements.Length; i++)
		{
			if (_filter.IsTrue(elements[i]))
			{
				_currentIndex += i;
				return false;   // stop processing
			}
		}

		_currentIndex += elements.Length;
		return true;
	}

	/// <summary>
	/// Writes the index of the first matching element, or -1 when no element matches.
	/// </summary>
	/// <param name="state">The run state of the cmdlet. Its <see cref="CmdletRunState.FoundMatch"/> value indicates whether an element matched.</param>
	protected override void EndCore(CmdletRunState state)
	{
		int index = state.FoundMatch ? _currentIndex : -1;

		this.WriteObject(index);
	}
}

