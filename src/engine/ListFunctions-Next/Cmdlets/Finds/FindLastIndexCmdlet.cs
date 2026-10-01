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
/// The index is the position of the match in the full input sequence across every pipeline record, not within a
/// single <see cref="InputObject"/> array. The condition does not run until all pipeline input is received.
/// </para>
/// <para>
/// A terminating error thrown by the condition script block ends the cmdlet with a terminating error.
/// </para>
/// <para><b>Performance:</b> The cmdlet buffers all input before evaluating, so memory use grows
/// with the size of the input. The condition runs only for the elements from the end of the sequence through the
/// last match.</para>
/// </remarks>
[Cmdlet(VerbsCommon.Find, "LastIndexOf")]
[Alias("Find-LastIndex", "LastIndexOf")]
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
	/// and must reference at least one of them. Parameter validation rejects a script block that references none.
	/// The script block's output is converted to a <see cref="bool"/> by using PowerShell's truthiness rules.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/> to evaluate against each input element.</value>
	[Parameter(Mandatory = true, Position = 0), Alias("ScriptBlock")]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public ScriptBlock Condition { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to search. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline record is appended to the elements from the previous records. A <see langword="null"/> or
	/// empty array contributes no elements. <see langword="null"/> and empty-string elements are evaluated like any
	/// other element.
	/// </remarks>
	/// <value>The array of elements to evaluate, or <see langword="null"/> when no elements are supplied.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true), Alias("List")]
	[AllowEmptyCollection, PSAllowNull, AllowEmptyString]
	public object?[]? InputObject { get; set; }

	/// <summary>
	/// Gets or sets the error action preference applied while the condition script block runs.
	/// </summary>
	/// <remarks>
	/// The value is assigned to <c>$ErrorActionPreference</c> in the script block's scope. It controls how
	/// non-terminating errors written by the script block are handled and does not change the cmdlet's own
	/// <c>-ErrorAction</c> behavior.
	/// </remarks>
	/// <value>The error action preference for script block execution. Defaults to <see cref="ActionPreference.SilentlyContinue"/>.</value>
	[Parameter]
	public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

	/// <summary>
	/// Creates the filter that evaluates <see cref="Condition"/> with the configured <see cref="ScriptBlockErrorAction"/>.
	/// </summary>
	protected override void BeginCore()
	{
		_filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
	}
	/// <summary>
	/// Appends the elements of the current <see cref="InputObject"/> array to the input buffer.
	/// </summary>
	/// <remarks>
	/// The condition is not evaluated here. Evaluation happens in <see cref="EndCore(CmdletRunState)"/> after all
	/// input is received.
	/// </remarks>
	/// <returns>Always <see langword="true"/>, so that all pipeline input is collected.</returns>
	protected override bool ProcessCore()
	{
		if (this.InputObject is not null)
		{
			_list.AddRange(this.InputObject);
		}

		return true;
	}
	/// <summary>
	/// Searches the buffered input backward and writes the index of the last matching element, or -1 when no element matches.
	/// </summary>
	/// <remarks>
	/// The method writes nothing when <paramref name="state"/> reports that a match was already found.
	/// </remarks>
	/// <param name="state">The run state of the cmdlet.</param>
	protected override void EndCore(CmdletRunState state)
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

