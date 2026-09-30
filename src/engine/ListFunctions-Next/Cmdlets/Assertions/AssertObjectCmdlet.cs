using ListFunctions.Components;
using ListFunctions.Modern;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions;

/// <summary>
/// Base implementation for object-assertion cmdlets that evaluate a condition against a sequence of objects.
/// </summary>
/// <remarks>
/// This abstract class encapsulates the common lifecycle for assertion-style cmdlets: preparing a compiled
/// <see cref="ScriptBlockFilter"/>, processing each pipeline input, and finalizing the result. Derived types
/// provide the specific evaluation semantics by implementing <see cref="Process(ScriptBlockFilter)"/>
/// and <see cref="ProcessWhenNoCondition()"/>.
/// </remarks>
public abstract class AssertObjectCmdlet : ListFunctionCmdletBase
{
	/// <summary>
	/// Gets or sets the condition script block that will be evaluated for each input object.
	/// </summary>
	/// <remarks>
	/// When set, the implementation creates a <see cref="ScriptBlockFilter"/> during <see cref="BeginCore"/>.
	/// The property setter also updates <see cref="HasCondition"/> based on whether the provided script block
	/// contains executable content.
	/// </remarks>
	/// <value>The condition <see cref="ScriptBlock"/>, or <see langword="null"/> when none was provided.</value>
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
	/// Gets or sets the <see cref="ActionPreference"/> applied when the condition script block raises an error.
	/// </summary>
	/// <remarks>Derived cmdlets must provide a default value for this preference.</remarks>
	public abstract ActionPreference ScriptBlockErrorAction { get; set; }

	[AllowsNull]
	/// <summary>
	/// Gets the compiled filter used to evaluate the <see cref="Condition"/> script block.
	/// </summary>
	/// <remarks>
	/// The filter is created during <see cref="BeginCore"/> when <see cref="HasCondition"/> is true.
	/// It may be <see langword="null"/> when no condition was supplied.
	/// </remarks>
	private protected ScriptBlockFilter? Filter { get; private set; }

	[MemberNotNullWhen(true, nameof(Condition), nameof(Filter))]
	/// <summary>
	/// Gets a value that indicates whether a non-empty condition has been provided.
	/// </summary>
	/// <remarks>
	/// When <see langword="true"/>, <see cref="Filter"/> is guaranteed to be non-null after <see cref="BeginCore"/>.
	/// </remarks>
	private protected bool HasCondition { get; set; }

	/// <summary>
	/// Prepares resources required for processing, creating the <see cref="Filter"/> when a condition exists.
	/// </summary>
	/// <remarks>
	/// The method constructs a <see cref="ScriptBlockFilter"/> using the configured <see cref="Condition"/>
	/// and the configured <see cref="ScriptBlockErrorAction"/> preference.
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
	/// Implements the process logic used by the base lifecycle. The result indicates whether processing
	/// should continue or stop.
	/// </summary>
	/// <returns><see langword="true"/> to continue processing; <see langword="false"/> to stop.</returns>
	protected sealed override bool ProcessCore()
	{
		return this.HasCondition
			? !this.Process(this.Filter)
			: !this.ProcessWhenNoCondition();
	}
	/// <summary>
	/// Evaluates the compiled <see cref="ScriptBlockFilter"/> against the current input set.
	/// </summary>
	/// <param name="filter">The compiled filter to execute. Will not be <see langword="null"/> when <see cref="HasCondition"/> is true.</param>
	/// <returns><see langword="true"/> when a match was found; otherwise <see langword="false"/>.</returns>
	protected abstract bool Process(ScriptBlockFilter filter);
	/// <summary>
	/// Performs evaluation when no condition script block is supplied.
	/// </summary>
	/// <returns>Behavior depends on the concrete implementation; implementations must indicate whether a match exists.</returns>
	protected abstract bool ProcessWhenNoCondition();

	/// <summary>
	/// Translates the internal run state into the public end-phase call for derived classes.
	/// </summary>
	/// <param name="state">The current cmdlet run state.</param>
	protected sealed override void EndCore(CmdletRunState state)
	{
		this.End(state.FoundMatch);
	}
	/// <summary>
	/// Called during the end phase to allow the derived cmdlet to write the final result to the pipeline.
	/// </summary>
	/// <param name="scriptResult">The logical result of the assertion evaluation.</param>
	protected abstract void End(bool scriptResult);
}

