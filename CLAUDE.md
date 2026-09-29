# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

ListFunctions is a PowerShell binary module (published to the PowerShell Gallery) that provides cmdlets for asserting over, searching, and constructing generic .NET collections (`List[T]`, `HashSet[T]`, `SortedSet[T]`, `Dictionary[K,V]`). Its main feature is equality comparers, hash functions, and comparers written as PowerShell ScriptBlocks. It ships for both Windows PowerShell 5.1 and PowerShell 7.

## Build

The solution is `src/engine/ListFunctions.Engine.slnx`. Run commands from the repo root in Bash:

```bash
dotnet build src/engine/ListFunctions.Engine.slnx -c Debug
dotnet build src/engine/ListFunctions-Next/ListFunctions-Next.csproj -c Debug   # PS 7 module only
```

- There are no test projects. To verify a change, build it and run it in a real PowerShell session.
- All three projects are SDK-style and use central package management. Package versions live only in `src/engine/Directory.Packages.props`.
- `ListFunctions-NETFramework` gets its runtime dependencies (ZLinq, System.Memory, System.Collections.Immutable, and so on) from Engine's `netstandard2.0` package references. Its only direct package references are `Microsoft.PowerShell.5.ReferenceAssemblies`, with `ExcludeAssets="runtime"`, and `PolySharp`. As of PolySharp 1.16.0, Engine's generated polyfills (the nullable attributes and others) are not visible to it through InternalsVisibleTo, so it generates its own.
- `Directory.Build.props` sets `CopyLocalLockFileAssemblies` to `true`, so every Debug and Release output folder contains its NuGet runtime dependencies, such as `ZLinq.dll`. By default the SDK copies them only for `net48`.
- Keep PowerShell itself out of the build outputs. `System.Management.Automation` is referenced with `ExcludeAssets="runtime;native"`. Without `native`, PowerShell's native binaries still land under `runtimes/`. Engine's `PowerShellStandard.Library` uses `ExcludeAssets="runtime"`, and `PrivateAssets="all"` so that it doesn't flow to `ListFunctions-NETFramework`.
- `.build/build.ps1` and `.debug/debug.ps1` are left over from the old script-based module. They build `src/ListFunctions.psm1` and read from `src/assemblies`, and neither path exists anymore. Don't use them to build the current module.

## Debugging

`ListFunctions.Engine`, `ListFunctions-Next`, and `ListFunctions-NETFramework` each contain a `Debug.ps1` that is copied to the output folder in non-Release builds. The `Properties/launchSettings.json` files start `Debug.ps1` in the output folder: `ListFunctions-Next` uses `pwsh -NoExit`, and `ListFunctions-NETFramework` uses Windows PowerShell 5.1 (`powershell.exe -NoExit -NoProfile`). `ListFunctions-Next`'s script imports `ListFunctions.Engine.dll` and `ListFunctions.Next.dll`. The build already puts their dependencies in the output folder, so none of the scripts copy files.

## Architecture

There are three projects under `src/engine/`:

- **`ListFunctions.Engine`** targets `netstandard2.0` and `net10.0`. It holds the reusable, non-cmdlet core:
	- `Modern/EqualityBlock`, `HashBlock`, and `ComparingBlock` (base `ComparingBase`) turn user ScriptBlocks into `IEqualityComparer` and `IComparer` implementations.
	- `ScriptBlockFilter` evaluates predicates for `Assert-Any`/`Assert-All`.
	- `Modern/Constructors/*Ctor` build closed generic collection types through reflection. `AddMethodInvoker` calls their `Add` method.
	- `Modern/Variables` injects the per-item context variables into the ScriptBlocks: `$_`, `$this`, and `$psitem` for single items, and `$left`/`$right` or `$x`/`$y` for equality. `Modern/Pools` holds the pools for these objects.
	- Its internals are exposed to `ListFunctions.Next` and `ListFunctions.NETFramework` through `<AssemblyAttribute>` InternalsVisibleTo items in the csproj.
	- `Internal/VarList.cs` is excluded from compilation on purpose.
- **`ListFunctions-Next`** targets `net10.0` and builds `ListFunctions.Next.dll`, the PowerShell 7 binary module. All cmdlets live here, under `Cmdlets/Assertions`, `Cmdlets/Constructs`, and `Cmdlets/Finds`.
- **`ListFunctions-NETFramework`** is an SDK-style project that targets `net48` and builds `ListFunctions.NETFramework.dll`, the Windows PowerShell 5.1 module. It has almost no code of its own: a `ModuleInitializer` that adds an `AssemblyResolve` hook to load dependencies from its own folder, plus a `ValidateNotNullOrWhiteSpace` polyfill. It compiles **every `.cs` file in `ListFunctions-Next`** through a wildcard `<Compile Include>`.
	- As a result, all code in `ListFunctions-Next` must also compile for .NET Framework 4.8 against the PowerShell 5 reference assemblies.
	- Wrap newer BCL or PowerShell 7 APIs in `#if NETCOREAPP` or `#if NET9_0_OR_GREATER`, as the existing code does.
	- Building only `ListFunctions-Next` does not catch these errors. Build the full solution.

### Cmdlet lifecycle

Every cmdlet derives from `Cmdlets/ListFunctionCmdletBase`.

- The base class seals `BeginProcessing`, `ProcessRecord`, and `EndProcessing`. Subclasses override these instead:
	- `BeginCore()`
	- `ProcessCore()`, which returns `false` to stop processing further pipeline input. The base class records this as `CmdletRunFlags.FoundMatch`.
	- `EndCore(CmdletRunState)`
	- `Cleanup()`
- If `BeginCore` or `ProcessCore` throws, the exception becomes a terminating error after `Cleanup` runs.
- The collection-building cmdlets derive from `EqualityConstructingCmdlet<T>`. It builds the collection in a sealed `BeginCore`, supports a custom ScriptBlock equality comparer, and adds a dynamic `-CaseSensitive` parameter when the element type is `string`.

### Shipped module layout

The `ListFunctions/` directory is the publishable module, and it contains committed build outputs.

- `ListFunctions.psm1` checks `$PSVersionTable.PSVersion.Major`. On 5 it imports `Desk/ListFunctions.NETFramework.dll`; on 7 it imports `Core/ListFunctions.Next.dll`.
- `Core/` holds the `net10.0` builds of Engine, Next, and ZLinq. `Desk/` holds the `netstandard2.0` Engine and the NETFramework DLL.
- Copy these DLLs in by hand from the Release output folders. Those folders include the NuGet dependencies, so nothing has to come from the NuGet cache.
- When cmdlets, aliases, or shipped files change, update `CmdletsToExport`, `AliasesToExport`, and `FileList` in `ListFunctions.psd1`.
- The version (currently `4.0.0`) is set in two places: `<Version>` in `src/engine/Directory.Build.props` and `ModuleVersion` in the `.psd1`. Change both together. `AssemblyVersion` and `FileVersion` are set to `$(Version)` so they stay exactly three-part.
- `Directory.Build.props` also holds the shared authorship and repository metadata and the common compiler settings (`RootNamespace`, `LangVersion`, `ImplicitUsings`, `AllowUnsafeBlocks`), plus `CopyLocalLockFileAssemblies`. Each csproj keeps only what differs between projects: target frameworks, `Nullable`, assembly name, and title/product.

`src/public/*.ps1` and `src/private/*.ps1` contain the legacy script implementation. The module does not load them. `Remove-All` and `Remove-At` exist only there and have not been ported to cmdlets.

## Code style

`src/engine/.editorconfig` is the authority. **Most existing C# does not follow it yet:** files use 4-space indentation, CRLF line endings, block-scoped namespaces, and sometimes a BOM. Don't treat that as the house style, and don't copy it from nearby code. Code you write or change must use:

- Tabs for indentation (tab width 4) in C#.
- LF line endings (`.gitattributes` also sets `* text=auto eol=lf`) and UTF-8.
- File-scoped namespaces, with `using` directives outside the namespace.
- `this.` qualification on methods, properties, and events, and never on fields (`dotnet_style_qualification_for_field = false:error`).
- Regular constructors, not primary constructors (`csharp_style_prefer_primary_constructors = false:warning`).
- Block-bodied methods, constructors, and operators. Expression bodies are allowed for single-line properties, indexers, and accessors.

Ask before reformatting whole files that you are not otherwise changing.

XML documentation follows `.github/copilot-instructions.md`. It covers tag order, `<see langword>` rules (never inside `<exception>`), public-surface hygiene, C# 14 `extension(...)` block documentation, and present-tense, active-voice wording. Read it before writing or editing doc comments.

Commit titles use third-person singular simple present tense, for example "Updates …" or "Adds …".
