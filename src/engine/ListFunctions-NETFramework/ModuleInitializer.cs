using System.IO;

namespace ListFunctions;

/// <summary>
/// Registers an assembly resolver that loads the module's dependencies from the module's own folder when Windows PowerShell 5.1 imports the module.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell creates an instance of this class and calls <see cref="OnImport"/> when <c>Import-Module</c> loads this assembly. From then on, when the runtime cannot locate an assembly that ships in the same folder as this assembly, such as <c>ListFunctions.Engine</c>, the resolver loads it from that folder.
/// </para>
/// <para>
/// The resolver matches a requested assembly by its simple name only and ignores the requested version, culture, and public key token. As a result, it also satisfies a request for a different version of a shipped dependency, which would otherwise need a binding redirect that a module cannot supply.
/// </para>
/// </remarks>
public sealed class ModuleInitializer : IModuleAssemblyInitializer
{
	private static readonly string s_assLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
	private const string DLL = ".dll";
	private const string BACK = "\\";

	/// <summary>
	/// Registers the assembly resolver with the current application domain.
	/// </summary>
	/// <remarks>
	/// PowerShell calls this method when it imports the module. The resolver stays registered for the lifetime of the application domain, and removing the module does not unregister it.
	/// </remarks>
	public void OnImport()
	{
		AppDomain.CurrentDomain.AssemblyResolve += this.CurrentDomain_AssemblyResolve;
	}

	/// <summary>
	/// Loads a requested assembly from the folder that contains this assembly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Handles <see cref="AppDomain.AssemblyResolve"/>, which the runtime raises after its own probing fails. The handler strips the version, culture, and public key token from the requested display name, and then looks for a file named after the remaining simple name, with a <c>.dll</c> extension, in the folder that contains this assembly.
	/// </para>
	/// <para>
	/// The runtime can raise the event on any thread. The handler reads only static, read-only state, so concurrent calls are safe.
	/// </para>
	/// </remarks>
	/// <param name="sender">The application domain that raises the event.</param>
	/// <param name="e">The event data, whose <see cref="ResolveEventArgs.Name"/> holds the display name of the requested assembly.</param>
	/// <returns>
	/// The assembly loaded from the matching file, or <see langword="null"/> when no file in the folder matches the requested name, so that other handlers or the runtime can report the failure.
	/// </returns>
	/// <exception cref="FileLoadException">Thrown when the matching file exists but cannot be loaded.</exception>
	/// <exception cref="BadImageFormatException">Thrown when the matching file is not a valid assembly.</exception>
	private Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
	{
		string name = e.Name;
		int index = name.IndexOf(',');
		if (index != -1)
		{
			name = name.Substring(0, index);
		}

		string fullPath = string.Concat(s_assLocation, BACK, name, DLL);
		if (File.Exists(fullPath))
		{
			return Assembly.LoadFile(fullPath);
		}

		return null;
	}
}
