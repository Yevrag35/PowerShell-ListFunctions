using System.Management.Automation.Runspaces;

namespace ListFunctions.Engine.Tests;

/// <summary>
/// Provides an open PowerShell runspace that the tests in one test class share.
/// </summary>
/// <remarks>
/// <para>
/// A script block runs only on a thread whose <see cref="Runspace.DefaultRunspace"/> is set, and that property is
/// thread-static. xUnit.net creates the fixture once for the whole class and doesn't promise to run each test on the
/// thread that created it, so every test calls <see cref="Enter"/> before it runs a script block.
/// </para>
/// <para>
/// The runspace isn't thread-safe: it must not run two script blocks at the same time. xUnit.net runs the tests of one
/// class one at a time, and tests in different classes run in parallel, each class with its own fixture.
/// </para>
/// <para>
/// The runspace starts from <see cref="InitialSessionState.CreateDefault"/>, so the built-in cmdlets, such as
/// <c>Write-Error</c>, are available. The .NET 10 build hosts PowerShell 7 from the Microsoft.PowerShell.SDK package, and
/// the .NET Framework 4.8 build hosts the Windows PowerShell 5.1 that is installed with Windows.
/// </para>
/// </remarks>
public sealed class RunspaceFixture : IDisposable
{
	private readonly Runspace _runspace;

	/// <summary>
	/// Gets the engine intrinsics of the fixture's runspace, which PowerShell passes to the transformation and validation
	/// attributes of each parameter that it binds.
	/// </summary>
	/// <remarks>
	/// The object is the runspace's <c>$ExecutionContext</c>. Its <see cref="EngineIntrinsics.SessionState"/> property
	/// returns the session state that is current when the property is read: a module's session state while code in that
	/// module runs, and otherwise the top-level session state, whose <see cref="SessionState.Module"/> is
	/// <see langword="null"/>.
	/// </remarks>
	/// <value>The value of the runspace's <c>$ExecutionContext</c> variable.</value>
	public EngineIntrinsics EngineIntrinsics { get; }

	/// <summary>
	/// Initializes a new <see cref="RunspaceFixture"/> instance and opens its runspace.
	/// </summary>
	public RunspaceFixture()
	{
		_runspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault());
		_runspace.Open();
		this.EngineIntrinsics = (EngineIntrinsics)_runspace.SessionStateProxy.GetVariable("ExecutionContext");
	}

	/// <summary>
	/// Makes the fixture's runspace the default runspace of the current thread until the returned scope is disposed.
	/// </summary>
	/// <remarks>
	/// Call this method in a <see langword="using"/> declaration at the start of a test, before the test creates or runs
	/// any script block.
	/// </remarks>
	/// <returns>A scope that restores the thread's previous default runspace when it's disposed.</returns>
	public RunspaceScope Enter()
	{
		return new RunspaceScope(_runspace);
	}

	/// <summary>
	/// Closes the runspace and releases its resources.
	/// </summary>
	public void Dispose()
	{
		_runspace.Dispose();
	}
}
