# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

ListFunctions is a PowerShell binary module (published to the PowerShell Gallery) that provides cmdlets for asserting over, searching, and constructing generic .NET collections (`List[T]`, `HashSet[T]`, `SortedSet[T]`, `Dictionary[K,V]`). Its main feature is equality comparers, hash functions, and comparers written as PowerShell ScriptBlocks. It ships for both Windows PowerShell 5.1 and PowerShell 7.

## Build

The solution is `src/engine/ListFunctions.Engine.slnx`. Run commands from the repo root in Bash:

```bash
dotnet build src/engine/ListFunctions.Engine.slnx -c Debug
dotnet build src/engine/ListFunctions-Next/ListFunctions-Next.csproj -c Debug   # PS 7 module only
```

- All four projects are SDK-style and use central package management. Package versions live only in `src/engine/Directory.Packages.props`.
- `ListFunctions-NETFramework` gets its runtime dependencies (ZLinq, System.Memory, System.Collections.Immutable, and so on) from Engine's `netstandard2.0` package references. Its only direct package references are `Microsoft.PowerShell.5.ReferenceAssemblies`, with `ExcludeAssets="runtime"`, and `PolySharp`. As of PolySharp 1.16.0, Engine's generated polyfills (the nullable attributes and others) are not visible to it through InternalsVisibleTo, so it generates its own.
- `Directory.Build.props` sets `CopyLocalLockFileAssemblies` to `true`, so every Debug and Release output folder contains its NuGet runtime dependencies, such as `ZLinq.dll`. By default the SDK copies them only for `net48`.
- Keep PowerShell itself out of the build outputs. `System.Management.Automation` is referenced with `ExcludeAssets="runtime;native"`. Without `native`, PowerShell's native binaries still land under `runtimes/`. Engine's `PowerShellStandard.Library` uses `ExcludeAssets="runtime"`, and `PrivateAssets="all"` so that it doesn't flow to `ListFunctions-NETFramework`. `ListFunctions.Engine.Tests` is the exception: it hosts PowerShell to run its tests.
- `.build/build.ps1` and `.debug/debug.ps1` are left over from the old script-based module. They build `src/ListFunctions.psm1` and read from `src/assemblies`, and neither path exists anymore. Don't use them to build the current module.

## Tests

`tests/` holds the Pester 6 tests, which run the cmdlets. `src/engine/ListFunctions.Engine.Tests/` holds the xUnit.net v3 tests, which call `ListFunctions.Engine` directly (see Engine tests below). Build first, then run the Pester tests from the repo root in Bash:

```bash
pwsh -NoProfile -File tests/Invoke-Tests.ps1                              # Debug build, both editions
pwsh -NoProfile -File tests/Invoke-Tests.ps1 -Tag Bug06 -Edition Core     # one bug's tests, PowerShell 7 only
pwsh -NoProfile -File tests/Invoke-Tests.ps1 -Configuration Release -Output Detailed
```

- `Invoke-Tests.ps1` runs the tests in a new `powershell.exe` process and a new `pwsh` process. The imported DLLs unload when those processes exit, so they don't block the next build. The script exits with 1 when a test fails in either edition.
- Every test file calls `tests/Import-ListFunctions.ps1` in a top-level `BeforeAll`. It imports `ListFunctions.Next.dll` (PowerShell 7) or `ListFunctions.NETFramework.dll` (Windows PowerShell 5.1) from the build output, never the committed DLLs under `ListFunctions/`.
- Each cmdlet gets its own `<Cmdlet>.Tests.ps1` file. `Module.Tests.ps1` checks that the build exports exactly the manifest's `CmdletsToExport` and `AliasesToExport`.
- Fix each item in `.work/bugs.md` test-first. Turn its repro into a test tagged `BugNN`, watch the test fail, and then fix the bug. When the fix goes in `ListFunctions.Engine`, the test goes in the Engine tests instead, as `.work/bugs.md` explains.
- Test files also run in Windows PowerShell 5.1, so they can't use PowerShell 7 syntax such as `??`, the ternary operator, or `&&`. Keep them ASCII, because 5.1 reads a UTF-8 file without a BOM as ANSI.
- Use Pester 6's `Should-*` commands, not the older `Should -Be` form. `Should-BeCollection` can't take a collection of value types, such as a `List[int]`, as `-Actual`. Pass `([object[]]$list)` instead. Don't use `@($list)`: for a `List[object]` that a command outputs, which PowerShell wraps in a PSObject, `@()` throws "Argument types do not match". That's a PowerShell bug in both 5.1 and 7.

### Engine tests

`dotnet test` builds the project first. Run it from the repo root in Bash:

```bash
dotnet test src/engine/ListFunctions.Engine.Tests/ListFunctions.Engine.Tests.csproj -c Debug                                            # both targets
dotnet test src/engine/ListFunctions.Engine.Tests/ListFunctions.Engine.Tests.csproj -c Debug -f net48 --filter-trait "Category=Bug10"  # one bug's tests, Windows PowerShell 5.1 only
```

- Never pass `--nologo` or `--no-incremental` to `dotnet test`. It hands options it doesn't recognize to the test app, which stops with "Zero tests ran" and exit code 5 without naming the option. Both flags are still right for `dotnet build`, and `-v q` works with either command.
- `xunit.v3` 4.x runs on Microsoft.Testing.Platform v2, so the project builds an executable, and the root `global.json` switches `dotnet test` to that platform. Don't add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`. Without `global.json`, `dotnet test` uses VSTest, and the build fails with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later."
- The project targets `net10.0` and `net48`. `net10.0` tests Engine's `net10.0` build in PowerShell 7, hosted from the `Microsoft.PowerShell.SDK` package. Keep that package at the same version as `System.Management.Automation`. `net48` tests Engine's `netstandard2.0` build in the Windows PowerShell 5.1 that is installed with Windows, loaded from the GAC.
- A script block runs only on a thread whose `Runspace.DefaultRunspace` is set, and that property is thread-static. Each test class takes a `RunspaceFixture` through `IClassFixture<RunspaceFixture>`, and each test that runs a script block starts with `using RunspaceScope scope = _runspace.Enter();`.
- The test folders mirror Engine's. The tests for `Modern/HashBlock.cs` are in `Modern/HashBlockTests.cs`, in the namespace `ListFunctions.Engine.Tests.Modern`.
- A bug's tests get `[Trait("Category", "BugNN")]`, which matches its Pester tag.

## Debugging

`ListFunctions.Engine`, `ListFunctions-Next`, and `ListFunctions-NETFramework` each contain a `Debug.ps1` that is copied to the output folder in non-Release builds. The `Properties/launchSettings.json` files start `Debug.ps1` in the output folder: `ListFunctions-Next` uses `pwsh -NoExit`, and `ListFunctions-NETFramework` uses Windows PowerShell 5.1 (`powershell.exe -NoExit -NoProfile`). `ListFunctions-Next`'s script imports `ListFunctions.Engine.dll` and `ListFunctions.Next.dll`. The build already puts their dependencies in the output folder, so none of the scripts copy files.

## Architecture

There are four projects under `src/engine/`:

- **`ListFunctions.Engine`** targets `netstandard2.0` and `net10.0`. It holds the reusable, non-cmdlet core:
	- `Modern/EqualityBlock`, `HashBlock`, and `ComparingBlock` (base `ComparingBase`) turn user ScriptBlocks into `IEqualityComparer` and `IComparer` implementations.
	- `ScriptBlockFilter` evaluates predicates for `Assert-Any`/`Assert-All`.
	- `Modern/Constructors/*Ctor` build closed generic collection types through reflection. `AddMethodInvoker` calls their `Add` method.
	- `Modern/Variables` injects the per-item context variables into the ScriptBlocks: `$_`, `$this`, and `$psitem` for single items, and `$left`/`$right` or `$x`/`$y` for equality.
	- Its internals are exposed to `ListFunctions.Next`, `ListFunctions.NETFramework`, and `ListFunctions.Engine.Tests` through `<AssemblyAttribute>` InternalsVisibleTo items in the csproj.
	- `Internal/VarList.cs` is excluded from compilation on purpose.
- **`ListFunctions-Next`** targets `net10.0` and builds `ListFunctions.Next.dll`, the PowerShell 7 binary module. All cmdlets live here, under `Cmdlets/Assertions`, `Cmdlets/Constructs`, and `Cmdlets/Finds`.
- **`ListFunctions-NETFramework`** is an SDK-style project that targets `net48` and builds `ListFunctions.NETFramework.dll`, the Windows PowerShell 5.1 module. It has almost no code of its own: a `ModuleInitializer` that adds an `AssemblyResolve` hook to load dependencies from its own folder, plus a `ValidateNotNullOrWhiteSpace` polyfill. It compiles **every `.cs` file in `ListFunctions-Next`** through a wildcard `<Compile Include>`.
	- As a result, all code in `ListFunctions-Next` must also compile for .NET Framework 4.8 against the PowerShell 5 reference assemblies.
	- Wrap newer BCL or PowerShell 7 APIs in `#if NETCOREAPP` or `#if NET9_0_OR_GREATER`, as the existing code does.
	- Building only `ListFunctions-Next` does not catch these errors. Build the full solution.
- **`ListFunctions.Engine.Tests`** targets `net10.0` and `net48` and holds the xUnit.net v3 tests for Engine (see Engine tests under Tests). It isn't part of the module.

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
- `Directory.Build.props` also holds the shared authorship and repository metadata and the common compiler settings (`RootNamespace`, `LangVersion`, `ImplicitUsings`, `AllowUnsafeBlocks`), plus `CopyLocalLockFileAssemblies`.
	- It declares the global usings as `<Using>` items: `System`, `System.Collections`, `System.Collections.Generic`, `System.Diagnostics`, `System.Diagnostics.CodeAnalysis`, and `System.Management.Automation`. It also declares the `AllowsNull` and `PSAllowNull` aliases (see Code style). `ImplicitUsings` stays disabled. The `<Using>` items are not conditioned on target framework, so every target gets the same set, and they sit in this shared file because `ListFunctions-NETFramework` compiles Next's files but doesn't inherit Next's MSBuild items.
- Each csproj keeps only what differs between projects: target frameworks, `Nullable`, assembly name, and title/product.

`src/public/*.ps1` and `src/private/*.ps1` contain the legacy script implementation. The module does not load them. `Remove-All` and `Remove-At` exist only there and have not been ported to cmdlets.

## Code style

`src/engine/.editorconfig` is the authority. **Most existing C# does not follow it yet:** files use 4-space indentation, CRLF line endings, block-scoped namespaces, and sometimes a BOM. Don't treat that as the house style, and don't copy it from nearby code. Code you write or change must use:

- Tabs for indentation (tab width 4) in C#.
- LF line endings (`.gitattributes` also sets `* text=auto eol=lf`) and UTF-8.
- File-scoped namespaces, with `using` directives outside the namespace.
- `this.` qualification on methods, properties, and events, and never on fields (`dotnet_style_qualification_for_field = false:error`).
- Regular constructors, not primary constructors (`csharp_style_prefer_primary_constructors = false:warning`).
- Block-bodied methods, constructors, and operators. Expression bodies are allowed for single-line properties, indexers, and accessors.
- `[PSAllowNull]` for PowerShell's parameter attribute (`System.Management.Automation.AllowNullAttribute`) and `[AllowsNull]` for the nullable-analysis attribute (`System.Diagnostics.CodeAnalysis.AllowNullAttribute`). Both namespaces are global usings, so a bare `[AllowNull]` fails with CS0104. The aliases are global too, so don't redeclare them in a file.

Ask before reformatting whole files that you are not otherwise changing.

XML documentation follows `.github/copilot-instructions.md`. It covers tag order, `<see langword>` rules (never inside `<exception>`), public-surface hygiene, C# 14 `extension(...)` block documentation, and present-tense, active-voice wording. Read it before writing or editing doc comments.

Commit titles use third-person singular simple present tense, for example "Updates …" or "Adds …".
