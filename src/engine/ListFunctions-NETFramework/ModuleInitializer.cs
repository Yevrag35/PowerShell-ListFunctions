using System.IO;
using System.Threading;

namespace ListFunctions;

/// <summary>
/// Registers an assembly resolver that loads the module's dependencies from the module's own folder when Windows PowerShell 5.1 imports the module.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell creates an instance of this class and calls <see cref="OnImport"/> when <c>Import-Module</c> loads this assembly. From then on, when the runtime cannot bind an assembly that one of the module's own assemblies references, the resolver loads the file with that name from the folder that contains this assembly.
/// </para>
/// <para>
/// The module needs the resolver because <c>ListFunctions.Engine</c> and <c>ZLinq</c> target <c>netstandard2.0</c>, so they reference the assembly versions of the <c>netstandard2.0</c> builds of packages such as <c>System.Memory</c>. The <c>net462</c> builds that ship beside them have higher assembly versions. An application adds binding redirects for such references, but a module cannot, so the runtime fails to bind them. The resolver loads the shipped file instead, whatever version the reference names.
/// </para>
/// <para>
/// The resolver answers only when an assembly loaded from the module's folder references the exact requested identity, meaning its name, version, culture, and public key token. It declines every other request, such as another module's request for a different version of an assembly that this module also ships. In Windows PowerShell 5.1, <see cref="ResolveEventArgs.RequestingAssembly"/> is <see langword="null"/> when the runtime binds a reference from a module's assembly, so the resolver cannot tell which assembly makes a request. As a result, another module that requests exactly an identity that this module references also receives the shipped file.
/// </para>
/// </remarks>
public sealed class ModuleInitializer : IModuleAssemblyInitializer
{
	private static readonly string s_assLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
	private const string DLL = ".dll";
	private const string BACK = "\\";
	private static int _registered;

	/// <summary>
	/// Registers the assembly resolver with the current application domain.
	/// </summary>
	/// <remarks>
	/// <para>
	/// PowerShell calls this method every time it imports the module, including <c>Import-Module -Force</c> and an import that follows <c>Remove-Module</c>. Only the first call registers the resolver, so the application domain holds at most one copy of it. The method is thread-safe.
	/// </para>
	/// <para>
	/// The resolver stays registered for the lifetime of the application domain, and removing the module does not unregister it. The runtime cannot unload the module's assemblies, and objects that the module created, such as collections that use script comparers, can still run the module's code after the module is removed. That code can need dependencies that the runtime has not bound yet.
	/// </para>
	/// </remarks>
	public void OnImport()
	{
		if (Interlocked.Exchange(ref _registered, 1) == 0)
		{
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
		}
	}

	/// <summary>
	/// Loads a requested assembly from the folder that contains this assembly when one of the module's own assemblies references it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Handles <see cref="AppDomain.AssemblyResolve"/>, which the runtime raises after its own probing fails. The handler strips the version, culture, and public key token from the requested display name and looks for a file named after the remaining simple name, with a <c>.dll</c> extension, in the folder that contains this assembly. When the file exists, the handler loads it only if <see cref="IsReferencedByModule(string)"/> finds the full requested identity.
	/// </para>
	/// <para>
	/// The file check runs first because it is cheap and fails for almost every request, such as the satellite resource lookups that PowerShell makes often.
	/// </para>
	/// <para>
	/// The runtime can raise the event on any thread. The handler reads only static, read-only state, so concurrent calls are safe.
	/// </para>
	/// </remarks>
	/// <param name="sender">The application domain that raises the event.</param>
	/// <param name="e">The event data, whose <see cref="ResolveEventArgs.Name"/> holds the display name of the requested assembly.</param>
	/// <returns>
	/// The assembly loaded from the matching file, or <see langword="null"/> when no file in the folder matches the requested name or no assembly loaded from the folder references the requested identity, so that other handlers or the runtime can handle the request.
	/// </returns>
	/// <exception cref="FileLoadException">Thrown when the matching file exists but cannot be loaded.</exception>
	/// <exception cref="BadImageFormatException">Thrown when the matching file is not a valid assembly.</exception>
	private static Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs e)
	{
		string name = e.Name;
		int index = name.IndexOf(',');
		if (index != -1)
		{
			name = name.Substring(0, index);
		}

		string fullPath = string.Concat(s_assLocation, BACK, name, DLL);
		if (File.Exists(fullPath) && IsReferencedByModule(new AssemblyName(e.Name).FullName))
		{
			return Assembly.LoadFile(fullPath);
		}

		return null;
	}

	/// <summary>
	/// Determines whether an assembly loaded from the folder that contains this assembly references the specified assembly identity.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method checks every assembly in the current application domain whose file is in that folder, and skips dynamic assemblies and assemblies loaded from memory, which have no location. It compares full display names, so the name, version, culture, and public key token must all match.
	/// </para>
	/// <para>
	/// The method scans the loaded assemblies on every call instead of caching their references, because the module's assemblies load lazily: right after the module is imported, only this assembly is loaded. The runtime reuses the assembly that the resolver returns for an identity, so for the module's own references, the method runs about once per identity in a session.
	/// </para>
	/// </remarks>
	/// <param name="requestedFullName">The full display name of the requested assembly, in the format of <see cref="AssemblyName.FullName"/>.</param>
	/// <returns>
	/// <see langword="true"/> if an assembly loaded from the folder references <paramref name="requestedFullName"/>; otherwise, <see langword="false"/>.
	/// </returns>
	private static bool IsReferencedByModule(string requestedFullName)
	{
		foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (isApplicable(loaded))
			{
				foreach (AssemblyName reference in loaded.GetReferencedAssemblies())
				{
					if (string.Equals(reference.FullName, requestedFullName, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
		}

		return false;

		static bool isApplicable(Assembly assembly)
		{
			return !assembly.IsDynamic
				&& assembly.Location is { Length: > 0 } loc
				&& string.Equals(Path.GetDirectoryName(loc), s_assLocation, StringComparison.OrdinalIgnoreCase);
		}
	}
}
