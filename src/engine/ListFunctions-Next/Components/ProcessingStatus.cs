namespace ListFunctions.Components;

[Flags]
public enum CmdletRunFlags : uint
{
	None = 0,
	IsStopping = 1,
	FoundMatch = 2,
	BeginFailed = 4,
	ProcessFailed = 8,
	/// <summary>The end phase ran, either from EndProcessing or early, when the cmdlet stopped its upstream commands.</summary>
	Ended = 16,
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct CmdletRunState
{
	private readonly uint _flags;

	public CmdletRunFlags Flags => (CmdletRunFlags)_flags;

	public bool IsStopping => (_flags & (uint)CmdletRunFlags.IsStopping) != 0;
	public bool FoundMatch => (_flags & (uint)CmdletRunFlags.FoundMatch) != 0;
	public bool HadError => (_flags & (uint)(CmdletRunFlags.BeginFailed | CmdletRunFlags.ProcessFailed)) != 0;
	public bool BeginFailed => (_flags & (uint)CmdletRunFlags.BeginFailed) != 0;
	public bool ProcessFailed => (_flags & (uint)CmdletRunFlags.ProcessFailed) != 0;
	public bool Ended => (_flags & (uint)CmdletRunFlags.Ended) != 0;

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

	internal CmdletRunState(CmdletRunFlags flags)
	{
		_flags = (uint)flags;
	}

	internal CmdletRunState With(CmdletRunFlags add)
	{
		return new((CmdletRunFlags)(_flags | (uint)add));
	}
}
