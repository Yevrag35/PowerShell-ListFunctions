using ListFunctions.Components;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;

#nullable enable

namespace ListFunctions.Cmdlets.Finds;

/// <summary>
/// Finds the zero-based index of the last input element that satisfies a condition.
/// </summary>
/// <remarks>
/// <para>
/// The cmdlet collects every input element and then evaluates the <see cref="Condition"/> script block starting
/// from the last element and moving backward. It writes the index of the first element, in that reverse order, for
/// which the script block returns a value that PowerShell treats as <see langword="true"/>. When no element matches,
/// the cmdlet writes -1. The behavior mirrors <see cref="List{T}.FindLastIndex(System.Predicate{T})"/>.
/// </para>
/// <para>
/// The index is the position of the match in the full input sequence. Each pipeline object is one element, even when
/// it's <see langword="null"/> or an array, and a collection passed to <see cref="InputObject"/>, such as an array or a
/// set, supplies its elements. The condition doesn't run until all pipeline input is received.
/// </para>
/// <para>
/// With the default <see cref="ScriptBlockErrorAction"/>, <see cref="ActionPreference.SilentlyContinue"/>, and with
/// <see cref="ActionPreference.Ignore"/>, the cmdlet writes errors from the condition script block as warnings. The
/// first error that the script block doesn't handle itself ends the test of an element, and the element doesn't match,
/// so the search goes on with the element before it.
/// </para>
/// <para>
/// With any other value, such as <see cref="ActionPreference.Stop"/> or <see cref="ActionPreference.Continue"/>, errors
/// from the condition script block reach PowerShell unchanged, the way they do from a <c>ForEach-Object</c> script
/// block. With <see cref="ActionPreference.Stop"/>, an error that the script block writes ends the script that runs the
/// cmdlet, as <c>-ErrorAction Stop</c> does, and a failed method call ends only the statement. A <c>throw</c> ends the
/// script with either value, and <c>break</c> leaves the loop around the cmdlet with any value.
/// </para>
/// <para><b>Performance:</b> The cmdlet buffers all input before evaluating, so memory use grows
/// with the size of the input. The condition runs only for the elements from the end of the sequence through the
/// last match.</para>
/// </remarks>
[Cmdlet(VerbsCommon.Find, "LastIndex")]
[Alias("Find-LastIndexOf", "LastIndexOf")]
[OutputType(typeof(int))]
public sealed class FindLastIndexCmdlet : ListFunctionCmdletBase
{
	private ScriptBlockFilter _filter = null!;
	private readonly List<object?> _list = new(0);

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
	[Parameter(Mandatory = true, Position = 0), Alias("ScriptBlock", "FilterScript")]
	[IsScriptBlock, ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public ScriptBlock Condition { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to search. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline object is one element, even when it's <see langword="null"/> or an array, and is appended to the
	/// elements from the previous objects. A value passed to the parameter supplies the elements that piping it sends: a
	/// collection, such as an array or a set, supplies its elements, and <see langword="null"/> supplies none. Any other
	/// value is one element, and for one that isn't a string, such as a number or a dictionary, the cmdlet writes a
	/// warning. The parameter can't be combined with pipeline input. <see langword="null"/> and empty-string elements are
	/// evaluated like any other element.
	/// </remarks>
	/// <value>The current pipeline object, or the argument of the parameter. The value can be <see langword="null"/>.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true), Alias("List")]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
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
	/// Appends the elements of the current <see cref="InputObject"/> to the input buffer.
	/// </summary>
	/// <remarks>
	/// The condition is not evaluated here. Evaluation happens in the end phase, after all input is received.
	/// </remarks>
	/// <returns>Always <see langword="true"/>, so that all pipeline input is collected.</returns>
	protected override bool ProcessCore()
	{
		_list.AddRange(this.GetSearchElements(this.InputObject));
		return true;
	}
	/// <summary>
	/// Searches the buffered input backward and writes the index of the last matching element, or -1 when no element matches.
	/// </summary>
	/// <remarks>
	/// The method writes nothing when <paramref name="state"/> reports that a match was already found.
	/// </remarks>
	/// <param name="state">The run state of the cmdlet.</param>
	private protected override void EndCore(CmdletRunState state)
	{
		if (state.FoundMatch)
			return;

		for (int i = _list.Count - 1; i >= 0; i--)
		{
			if (_filter.IsTrue(_list[i]))
			{
				this.WriteObject(i);
				return;
			}
		}

		this.WriteObject(-1);
	}
}

