using ListFunctions.Components;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections.Generic;
using System.Management.Automation;

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
/// The index counts elements across every pipeline record, so it is the position of the match in the full input
/// sequence rather than within a single <see cref="InputObject"/> array. After the first match, the cmdlet stops
/// evaluating the condition and ignores the remaining pipeline input.
/// </para>
/// <para>
/// A terminating error thrown by the condition script block ends the cmdlet with a terminating error.
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
	/// and must reference at least one of them. Parameter validation rejects a script block that references none.
	/// The script block's output is converted to a <see cref="bool"/> by using PowerShell's truthiness rules.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/> to evaluate against each input element.</value>
	[Parameter(Mandatory = true, Position = 0)]
	[Alias("ScriptBlock")]
	[ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
	public ScriptBlock Condition { get; set; } = null!;

	/// <summary>
	/// Gets or sets the elements to search. The value is accepted from the pipeline.
	/// </summary>
	/// <remarks>
	/// Each pipeline record continues the sequence from the previous records. A <see langword="null"/> or empty
	/// array contributes no elements and does not advance the index. <see langword="null"/> and empty-string
	/// elements are evaluated like any other element.
	/// </remarks>
	/// <value>The array of elements to evaluate, or <see langword="null"/> when no elements are supplied.</value>
	[Parameter(Mandatory = true, ValueFromPipeline = true)]
	[Alias("List")]
	[AllowEmptyCollection, AllowEmptyString, AllowNull]
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
	/// Evaluates the elements of the current <see cref="InputObject"/> array and advances the running index.
	/// </summary>
	/// <remarks>
	/// When an element matches, the running index is set to that element's position in the full input sequence.
	/// Otherwise, the running index advances by the length of the array.
	/// </remarks>
	/// <returns><see langword="false"/> when an element matches and processing stops; otherwise <see langword="true"/>.</returns>
	protected override bool ProcessCore()
	{
		if (this.InputObject is null || this.InputObject.Length == 0)
			return true;    // keep going

		for (int i = 0; i < this.InputObject.Length; i++)
		{
			if (_filter.IsTrue(this.InputObject[i]))
			{
				_currentIndex += i;
				return false;   // stop processing
			}
		}

		_currentIndex += this.InputObject.Length;
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

	/// <summary>
	/// Releases the filter created in <see cref="BeginCore"/>.
	/// </summary>
	protected override void Cleanup()
	{
		if (_filter is not null)
		{
			_filter.Dispose();
			_filter = null!;
		}
	}
}

