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
/// pipeline object is one element, even when it's <see langword="null"/> or an array, and a collection passed to
/// <see cref="InputObject"/>, such as an array or a set, supplies its elements.
/// </para>
/// <para>
/// After the first match, the cmdlet writes the index and stops evaluating the condition. When its input comes from
/// the pipeline, it also stops the commands that send the input, the way <c>Select-Object -First</c> does. Those
/// commands don't run their end blocks.
/// </para>
/// <para>
/// With the default <see cref="ScriptBlockErrorAction"/>, <see cref="ActionPreference.SilentlyContinue"/>, and with
/// <see cref="ActionPreference.Ignore"/>, the cmdlet writes errors from the condition script block as warnings. The
/// first error that the script block doesn't handle itself ends the test of an element, and the element doesn't match,
/// so the search goes on with the next one.
/// </para>
/// <para>
/// With any other value, such as <see cref="ActionPreference.Stop"/> or <see cref="ActionPreference.Continue"/>, errors
/// from the condition script block reach PowerShell unchanged, the way they do from a <c>ForEach-Object</c> script
/// block. With <see cref="ActionPreference.Stop"/>, an error that the script block writes ends the script that runs the
/// cmdlet, as <c>-ErrorAction Stop</c> does, and a failed method call ends only the statement. A <c>throw</c> ends the
/// script with either value, and <c>break</c> leaves the loop around the cmdlet with any value.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.Find, "Index")]
[Alias("Find-IndexOf", "IndexOf")]
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
	[Alias("ScriptBlock", "FilterScript")]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public ScriptBlock Condition { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to search. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array, and continues the
	/// sequence from the previous objects. A value passed to the parameter supplies the elements that piping it sends: a
	/// collection, such as an array or a set, supplies its elements, and <see langword="null"/> supplies none. Any other
	/// value is one element, and for one that isn't a string, such as a number or a dictionary, the cmdlet writes a
	/// warning. The parameter can't be combined with pipeline input. <see langword="null"/> and empty-string elements are
	/// evaluated like any other element.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[Alias("List")]
	[AllowEmptyCollection, AllowEmptyString, PSAllowNull]
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
	public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

	/// <summary>
	/// Creates the filter that evaluates <see cref="Condition"/> with the configured <see cref="ScriptBlockErrorAction"/>.
	/// </summary>
	/// <remarks>
	/// When <see cref="ScriptBlockErrorAction"/> is <see cref="ActionPreference.SilentlyContinue"/> or
	/// <see cref="ActionPreference.Ignore"/>, the filter runs the condition with <c>$ErrorActionPreference</c> set to
	/// <see cref="ActionPreference.Stop"/>, so that the cmdlet can write each error as a warning.
	/// </remarks>
	protected override void BeginCore()
	{
		_filter = this.CreateConditionFilter(this.Condition, this.ScriptBlockErrorAction);
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
		object?[] elements = this.GetSearchElements(this.InputObject);
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

