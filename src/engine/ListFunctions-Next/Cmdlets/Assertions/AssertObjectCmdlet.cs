using ListFunctions.Components;
using ListFunctions.Modern;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Provides the base class for cmdlets that assert a condition over their input objects.
/// </summary>
/// <remarks>
/// <para>
/// The begin, process, and end phases are sealed. The begin phase creates a <see cref="ScriptBlockFilter"/> from
/// <see cref="Condition"/>. For each pipeline record, the class calls <see cref="Process(ScriptBlockFilter)"/>, or
/// <see cref="ProcessWhenNoCondition"/> when no condition is set, and the end phase passes the outcome to
/// <see cref="End(bool)"/>.
/// </para>
/// <para>
/// When <see cref="Process(ScriptBlockFilter)"/> or <see cref="ProcessWhenNoCondition"/> returns
/// <see langword="true"/>, the result of the assertion is decided. The cmdlet processes no more input and, when its
/// input comes from the pipeline, stops the commands that send it.
/// </para>
/// </remarks>
public abstract class AssertObjectCmdlet : ListFunctionCmdletBase
{
	/// <summary>
	/// Gets or sets the script block that tests each input object.
	/// </summary>
	/// <remarks>
	/// A script block that is <see langword="null"/>, empty, or only white space counts as no condition, and the cmdlet
	/// calls <see cref="ProcessWhenNoCondition"/> instead of testing input with it. Derived classes override the
	/// property to make it a parameter.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/>, or <see langword="null"/> when none is set.</value>
	public virtual ScriptBlock? Condition
	{
		get;
		set
		{
			field = value;
			this.HasCondition = !(value is null || string.IsNullOrWhiteSpace(value.ToString()));
		}
	}
	/// <summary>
	/// Gets or sets the error action preference applied while the condition script block runs.
	/// </summary>
	/// <remarks>
	/// The value is assigned to <c>$ErrorActionPreference</c> in the script block's scope. It doesn't change the
	/// cmdlet's own <c>-ErrorAction</c> behavior. Derived classes override the property to make it a parameter and
	/// give it a default value.
	/// </remarks>
	/// <value>The error action preference for script block execution.</value>
	public abstract ActionPreference ScriptBlockErrorAction { get; set; }

	/// <summary>
	/// Gets the filter that tests input objects with <see cref="Condition"/>.
	/// </summary>
	/// <remarks>
	/// <see cref="BeginCore"/> creates the filter when <see cref="HasCondition"/> is <see langword="true"/>.
	/// </remarks>
	/// <value>The condition filter, or <see langword="null"/> when no condition is set.</value>
	[AllowsNull]
	private protected ScriptBlockFilter? Filter { get; private set; }

	/// <summary>
	/// Gets a value that indicates whether <see cref="Condition"/> is a script block with content.
	/// </summary>
	/// <remarks>
	/// The <see cref="Condition"/> setter updates the value. When it is <see langword="true"/>, <see cref="Filter"/>
	/// isn't <see langword="null"/> after <see cref="BeginCore"/> runs.
	/// </remarks>
	/// <value>
	/// <see langword="true"/> when <see cref="Condition"/> isn't <see langword="null"/>, empty, or only white space;
	/// otherwise, <see langword="false"/>.
	/// </value>
	[MemberNotNullWhen(true, nameof(Condition), nameof(Filter))]
	private protected bool HasCondition { get; set; }

	/// <summary>
	/// Creates the <see cref="ScriptBlockFilter"/> for <see cref="Condition"/> when a condition is set.
	/// </summary>
	/// <remarks>
	/// The filter runs the condition with <c>$ErrorActionPreference</c> set to <see cref="ScriptBlockErrorAction"/>.
	/// When no condition is set, the method creates nothing.
	/// </remarks>
	protected sealed override void BeginCore()
	{
		base.BeginCore();

		if (this.HasCondition)
		{
			this.Filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
		}

		// # Maybe in the future.
		//try
		//{
		//    this.Begin();
		//}
		//catch
		//{
		//    this.Cleanup();
		//    throw;
		//}
	}

	/// <summary>
	/// Tests the current pipeline input by calling <see cref="Process(ScriptBlockFilter)"/>, or
	/// <see cref="ProcessWhenNoCondition"/> when no condition is set.
	/// </summary>
	/// <returns>
	/// <see langword="false"/> when the called method returns <see langword="true"/> and the cmdlet stops processing
	/// input; otherwise, <see langword="true"/>.
	/// </returns>
	protected sealed override bool ProcessCore()
	{
		return this.HasCondition
			? !this.Process(this.Filter)
			: !this.ProcessWhenNoCondition();
	}
	/// <summary>
	/// When implemented in a derived class, tests the current pipeline input with the condition filter.
	/// </summary>
	/// <remarks>
	/// The base class calls this method only when <see cref="Condition"/> is a script block with content. An error from
	/// the condition script block reaches PowerShell unchanged, and any other exception from this method becomes a
	/// terminating error.
	/// </remarks>
	/// <param name="filter">The filter that tests objects with <see cref="Condition"/>. This value isn't <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when the result of the assertion is decided and the cmdlet stops processing input;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	protected abstract bool Process(ScriptBlockFilter filter);
	/// <summary>
	/// When implemented in a derived class, processes the current pipeline input when no condition is set.
	/// </summary>
	/// <remarks>
	/// The base class calls this method when <see cref="Condition"/> is <see langword="null"/>, empty, or only white
	/// space. An exception from this method becomes a terminating error.
	/// </remarks>
	/// <returns>
	/// <see langword="true"/> when the result of the assertion is decided and the cmdlet stops processing input;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	protected abstract bool ProcessWhenNoCondition();

	/// <summary>
	/// Passes the outcome of the process phase to <see cref="End(bool)"/>.
	/// </summary>
	/// <param name="state">
	/// The run state of the cmdlet. Its <see cref="CmdletRunState.FoundMatch"/> value indicates whether
	/// <see cref="Process(ScriptBlockFilter)"/> or <see cref="ProcessWhenNoCondition"/> returned <see langword="true"/>.
	/// </param>
	protected sealed override void EndCore(CmdletRunState state)
	{
		this.End(state.FoundMatch);
	}
	/// <summary>
	/// When implemented in a derived class, writes the result of the assertion to the pipeline.
	/// </summary>
	/// <param name="scriptResult">
	/// <see langword="true"/> when <see cref="Process(ScriptBlockFilter)"/> or <see cref="ProcessWhenNoCondition"/>
	/// returned <see langword="true"/> for a pipeline record; otherwise, <see langword="false"/>.
	/// </param>
	protected abstract void End(bool scriptResult);
}

