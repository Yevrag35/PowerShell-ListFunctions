using System.Management.Automation.Runspaces;

namespace ListFunctions.Engine.Tests;

/// <summary>
/// Represents the period during which a runspace is the default runspace of the current thread.
/// </summary>
/// <remarks>
/// <para>
/// Get an instance from <see cref="RunspaceFixture.Enter"/> in a <see langword="using"/> declaration. Disposing it restores
/// the default runspace that the thread had before, which is usually <see langword="null"/>, so a later test on the same
/// thread can't run script blocks in this runspace by accident.
/// </para>
/// <para>
/// The type is a <see langword="ref"/> struct because <see cref="Runspace.DefaultRunspace"/> is thread-static. The
/// compiler rejects a scope that stays alive across an <see langword="await"/>, after which the code could resume on a
/// thread that has no default runspace, and disposing the scope would change that thread's value instead.
/// </para>
/// </remarks>
public readonly ref struct RunspaceScope
{
	private readonly Runspace? _previous;

	/// <summary>
	/// Initializes a new <see cref="RunspaceScope"/> instance that makes the specified runspace the default runspace of
	/// the current thread.
	/// </summary>
	/// <param name="runspace">The runspace to make the default. This value must not be <see langword="null"/>.</param>
	internal RunspaceScope(Runspace runspace)
	{
		_previous = Runspace.DefaultRunspace;
		Runspace.DefaultRunspace = runspace;
	}

	/// <summary>
	/// Restores the default runspace that the current thread had before this scope began.
	/// </summary>
	public void Dispose()
	{
		Runspace.DefaultRunspace = _previous;
	}
}
