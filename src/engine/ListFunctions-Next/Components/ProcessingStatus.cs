using ListFunctions.Cmdlets;

namespace ListFunctions.Components;

/// <summary>
/// Specifies the outcomes that a <see cref="ListFunctionCmdletBase"/> cmdlet records as it moves through its begin,
/// process, and end phases.
/// </summary>
/// <remarks>
/// The values are bit flags, and a run can record several at once. <see cref="CmdletRunState"/> exposes
/// <see cref="FoundMatch"/> and <see cref="Ended"/> as <see cref="bool"/> properties, and combines the flags that make
/// the cmdlet skip its remaining input in <see cref="CmdletRunState.ShouldSkipProcess"/>.
/// </remarks>
[Flags]
internal enum CmdletRunFlags : uint
{
	/// <summary>No outcome is recorded.</summary>
	None = 0,
	/// <summary>PowerShell is stopping the pipeline, for example because the user pressed Ctrl+C.</summary>
	IsStopping = 1,
	/// <summary>
	/// <see cref="ListFunctionCmdletBase.ProcessCore"/> returned <see langword="false"/>, so the cmdlet processes no
	/// further pipeline input. For the search and assertion cmdlets, this means an element matched.
	/// </summary>
	FoundMatch = 2,
	/// <summary><see cref="ListFunctionCmdletBase.BeginCore"/> threw an exception.</summary>
	BeginFailed = 4,
	/// <summary><see cref="ListFunctionCmdletBase.ProcessCore"/> threw an exception.</summary>
	ProcessFailed = 8,
	/// <summary>
	/// The end phase ran, either from <see cref="ListFunctionCmdletBase.EndProcessing"/> or early, when the cmdlet
	/// stopped the commands that send it pipeline input.
	/// </summary>
	Ended = 16,
}

/// <summary>
/// Represents the outcomes that a <see cref="ListFunctionCmdletBase"/> cmdlet has recorded so far in its run.
/// </summary>
/// <remarks>
/// <para>
/// The base class passes this state to <see cref="ListFunctionCmdletBase.EndCore(CmdletRunState)"/>. The state is
/// immutable, so a copy that a derived class receives doesn't change when the run records more outcomes.
/// </para>
/// <para>
/// A failure in the begin or process phase becomes a terminating error, and PowerShell doesn't run the end phase
/// after one. In practice, the state that <see cref="ListFunctionCmdletBase.EndCore(CmdletRunState)"/> receives holds
/// no failure flag, so the state exposes the stopping and failure flags only through <see cref="ShouldSkipProcess"/>.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CmdletRunState
{
	private readonly uint _flags;

	/// <summary>
	/// Gets a value that indicates whether the cmdlet stopped processing pipeline input before the input ran out.
	/// </summary>
	/// <remarks>For the search and assertion cmdlets, this means an element matched.</remarks>
	/// <value><see langword="true"/> when <see cref="CmdletRunFlags.FoundMatch"/> is set; otherwise, <see langword="false"/>.</value>
	public bool FoundMatch => (_flags & (uint)CmdletRunFlags.FoundMatch) != 0;
	/// <summary>
	/// Gets a value that indicates whether the end phase ran.
	/// </summary>
	/// <remarks>
	/// The base class sets this flag before it calls <see cref="ListFunctionCmdletBase.EndCore(CmdletRunState)"/>, so
	/// it is always <see langword="true"/> in the state that method receives.
	/// </remarks>
	/// <value><see langword="true"/> when <see cref="CmdletRunFlags.Ended"/> is set; otherwise, <see langword="false"/>.</value>
	public bool Ended => (_flags & (uint)CmdletRunFlags.Ended) != 0;

	/// <summary>
	/// Gets a value that indicates whether the cmdlet ignores the current pipeline input object.
	/// </summary>
	/// <value>
	/// <see langword="true"/> when PowerShell is stopping the pipeline, an earlier phase threw an exception, or the cmdlet
	/// already stopped processing input; otherwise, <see langword="false"/>.
	/// </value>
	public bool ShouldSkipProcess
	{
		get
		{
			return (_flags & (uint)(CmdletRunFlags.IsStopping
							  | CmdletRunFlags.BeginFailed
							  | CmdletRunFlags.ProcessFailed
							  | CmdletRunFlags.FoundMatch)) != 0;
		}
	}

	/// <summary>
	/// Initializes a new instance of <see cref="CmdletRunState"/> with the specified flags.
	/// </summary>
	/// <param name="flags">The outcomes to record.</param>
	internal CmdletRunState(CmdletRunFlags flags)
	{
		_flags = (uint)flags;
	}

	/// <summary>
	/// Returns a copy of this state with the specified flags added.
	/// </summary>
	/// <remarks>This state doesn't change. Flags that are already set stay set.</remarks>
	/// <param name="add">The outcomes to add.</param>
	/// <returns>A new <see cref="CmdletRunState"/> that contains the flags of this state and <paramref name="add"/>.</returns>
	internal CmdletRunState With(CmdletRunFlags add)
	{
		return new((CmdletRunFlags)(_flags | (uint)add));
	}
}
