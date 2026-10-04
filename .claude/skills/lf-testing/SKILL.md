---
name: lf-testing
description: How to run and write the ListFunctions tests in Windows PowerShell 5.1 and PowerShell 7. The Pester 6 tests in `tests/` run the cmdlets, and the xUnit.net v3 tests in `src/engine/ListFunctions.Engine.Tests/` call `ListFunctions.Engine` directly. Covers the commands and their traps, which PowerShell versions to use, when a test is worth writing and which suite it goes in, and how to write tests. Use it before you run, write, change, or debug a test, fix a bug, add or change a cmdlet, parameter, or Engine type, try a cmdlet in a repro or smoke test, or report test results, even when the request only asks to check that something works.
---

# Testing ListFunctions

ListFunctions has two test suites, and both run every test in Windows PowerShell 5.1 and in PowerShell 7:

- The Pester 6 tests in `tests/` run the cmdlets, the way a user does. They import the build output, so build the solution first (see Build in `CLAUDE.md`).
- The xUnit.net v3 tests in `src/engine/ListFunctions.Engine.Tests/` call `ListFunctions.Engine` directly. `dotnet test` builds the project first.

## PowerShell versions

Test only on stable PowerShell releases: Windows PowerShell 5.1, and PowerShell 7.6 or a later stable release. Use a preview only when you're asked to. That goes for test runs, repros, and experiments, and when you report results, name the versions you used.

- `Invoke-Tests.ps1` starts whichever `pwsh` comes first on PATH, which can be a preview or a release candidate. Each edition's output starts with its version, such as `PowerShell 7.6.6 (Core), Debug build`, so check that line.
- Stable PowerShell 7 installs to `$env:PROGRAMFILES\PowerShell\7`, and previews and release candidates install to `$env:PROGRAMFILES\PowerShell\7-preview`. When PATH finds a preview, run the PowerShell 7 tests in the stable `pwsh.exe` with `-InProcess`. Bash sees Windows environment variables with uppercase names, so the command is `"$PROGRAMFILES/PowerShell/7/pwsh.exe" -NoProfile -File tests/Invoke-Tests.ps1 -InProcess`. Run the Windows PowerShell tests with `-Edition Desktop`.

## Running the Pester tests

Build first, then run the tests from the repo root in Bash:

```bash
pwsh -NoProfile -File tests/Invoke-Tests.ps1                              # Debug build, both editions
pwsh -NoProfile -File tests/Invoke-Tests.ps1 -Tag Bug06 -Edition Core     # one bug's tests, PowerShell 7 only
pwsh -NoProfile -File tests/Invoke-Tests.ps1 -Configuration Release -Output Detailed
pwsh -NoProfile -Command "& ./tests/Invoke-Tests.ps1 -Tag Bug01,Bug02; exit \$LASTEXITCODE"   # several tags
```

- `Invoke-Tests.ps1` runs the tests in a new `powershell.exe` process and a new `pwsh` process. The imported DLLs unload when those processes exit, so they don't block the next build. The script exits with 1 when a test fails in either edition.
- The script doesn't build anything. It tests whatever the last build of the configuration left in the output folders, so rebuild after every code change.
- Pass several tags through `-Command`, as in the last example. `-File` passes each argument as a single string, so `-Tag Bug01,Bug02` reaches the script as one tag named `Bug01,Bug02`. No test has that tag, so Pester reports every test as NotRun, with 0 passed and 0 failed, and the run looks clean. A summary in which every test is NotRun means the filter matched nothing. It says nothing about the code.
- `-Path` runs only the files or folders you name, such as `-Path tests/New-List.Tests.ps1`.
- Every test file calls `tests/Import-ListFunctions.ps1` in a top-level `BeforeAll`. It imports `ListFunctions.Next.dll` (PowerShell 7) or `ListFunctions.NETFramework.dll` (Windows PowerShell 5.1) from the build output, never the committed DLLs under `ListFunctions/`.

## Running the Engine tests

`dotnet test` builds the project first. Run it from the repo root in Bash:

```bash
dotnet test src/engine/ListFunctions.Engine.Tests/ListFunctions.Engine.Tests.csproj -c Debug                                            # both targets
dotnet test src/engine/ListFunctions.Engine.Tests/ListFunctions.Engine.Tests.csproj -c Debug -f net48 --filter-trait "Category=Bug10"  # one bug's tests, Windows PowerShell 5.1 only
```

- Never pass `--nologo` or `--no-incremental` to `dotnet test`. It hands options it doesn't recognize to the test app, which stops with "Zero tests ran" and exit code 5 without naming the option. Both flags are still right for `dotnet build`, and `-v q` works with either command.
- Check the exit code, because a rejected option and a filter that matches nothing both end with "Zero tests ran". Exit code 5 means the test app rejected an option, so check the command line first. Exit code 8 means a filter matched no tests.
- `-f net10.0` or `-f net48` runs one target. Besides `--filter-trait`, xUnit.net's `--filter-class` and `--filter-method` narrow a run.
- `xunit.v3` 4.x runs on Microsoft.Testing.Platform v2, so the project builds an executable, and the root `global.json` switches `dotnet test` to that platform. Don't add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`. Without `global.json`, `dotnet test` uses VSTest, and the build fails with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later."
	- Microsoft doesn't support VSTest and Microsoft.Testing.Platform test projects in the same solution, so a new test project has to use the platform too.
	- If the platform itself ever stops the tests from running, the fallback is the `xunit.v3.mtp-off` package, the same xUnit.net framework on VSTest. VSTest support is going away, so the fallback only buys time. Ask before you switch.
- The project targets `net10.0` and `net48`. `net10.0` tests Engine's `net10.0` build in PowerShell 7, hosted from the `Microsoft.PowerShell.SDK` package. Keep that package on a stable release, at the same version as `System.Management.Automation`. `net48` tests Engine's `netstandard2.0` build in the Windows PowerShell 5.1 that is installed with Windows, loaded from the GAC.

## Reporting results

Name the versions each run used: the version line that each Pester edition prints, and the target framework of each Engine run. Give the passed and failed counts for each edition, and quote the message of each failure. A run in which every Pester test is NotRun, or a `dotnet test` run that exits with 8, matched no tests, so report it as a filter mistake, not as a pass.

## When to write a test

Write a test only when it has merit. Don't write a failing test before every fix, and don't give every change a test.

- **Has merit:** the test pins down behavior that users rely on and that a later change could plausibly break, such as a fixed bug whose cause could come back. A test also has merit when it records a decision that the code doesn't make obvious.
- **Has no merit:** the test only restates the code, an existing test already covers the behavior, or the change can't regress in a way that users would notice.
- **Usually needs no new test:** a refactor that doesn't change behavior, removing dead code, making a type internal, a rename, a change to comments or docs, or a release step such as copying the shipped DLLs into `ListFunctions/`. Run the existing tests instead, and if none of them reaches the code you changed, say so. The tests import the build output, not the DLLs under `ListFunctions/`, so they can't check a release.

When a test has merit, write it before or after the change, whichever helps, and don't give it a `BugNN` tag. Whether or not a change gets tests, `Module.Tests.ps1` checks that the build exports exactly the manifest's `CmdletsToExport` and `AliasesToExport`, so it fails until `ListFunctions.psd1` lists a new cmdlet and its aliases.

## Which suite a test goes in

- **Pester**, in `tests/<Cmdlet>.Tests.ps1`: anything that can surface during normal use of the module. Test it through the cmdlet, the way a user runs into it.
- **Engine**, in `src/engine/ListFunctions.Engine.Tests/`: anything whose cause is in `src/engine/ListFunctions.Engine/`. Test the Engine type directly.
- **Both**, only when each test has merit of its own: an Engine problem that also surfaces through a cmdlet, where the Engine test pins down the type's behavior and the Pester test shows that the cmdlet passes it on to the user.

Nothing tests `ListFunctions-Next` directly, so a cause there, such as a cmdlet's parameter sets or `ListFunctionCmdletBase`, gets Pester tests only.

## Writing a Pester test

Each cmdlet gets its own `tests/<Cmdlet>.Tests.ps1` file, named after the cmdlet, not an alias. Follow the shape of the existing files:

```powershell
BeforeAll {
	& "$PSScriptRoot/Import-ListFunctions.ps1"
}

Describe 'New-List' {
	Context 'Pipeline input' {
		It 'adds a piped array as one element' {
			$list = @(1, @(2, 3)) | New-List
			$list.Count | Should-Be 2
			Should-BeCollection -Expected @(2, 3) -Actual ([object[]]$list[1])
		}
	}
}
```

- `Describe` names the cmdlet, and each `Context` names a parameter or a feature, such as `'Capacity'` or `'Pipeline input'`. Each `It` states one behavior, in lowercase and the present tense, such as `'skips $null elements when -IncludeNullElements is absent'`.
- For cases that differ only in their data, give `-ForEach` one hashtable per case, and put a key such as `<Name>` in the `It` text so that each case reports on its own.
- Put a helper that test files share in its own script in `tests/`, with comment-based help, and dot-source it in the top-level `BeforeAll`, the way `New-HashSet.Tests.ps1` loads `Get-BucketCount.ps1`.
- Use Pester 6's `Should-*` commands, not the older `Should -Be` form. `Should-BeCollection` can't take a collection of value types, such as a `List[int]`, as `-Actual`. Pass `([object[]]$list)` instead. Don't use `@($list)`: for a `List[object]` that a command outputs, which PowerShell wraps in a PSObject, `@()` throws "Argument types do not match". That's a PowerShell bug in both 5.1 and 7.
- For a terminating error, `{ ... } | Should-Throw` returns the error record, and its `-ExceptionMessage` and `-FullyQualifiedErrorId` take wildcards. For a non-terminating error, run the cmdlet with `-ErrorVariable err -ErrorAction SilentlyContinue`, and then check `$err.Count`, `$err[0].Exception`, and `$err[0].TargetObject`.
- When a test covers input, test piped input and `-InputObject` separately, because PowerShell binds them differently: a piped array is one element, and an array passed to `-InputObject` supplies its elements. Include `$null` and nested arrays when the behavior depends on them.
- Before you write a call to a cmdlet, read the transformation and validation attributes on the parameters it uses. The module defines its own in `src/engine/ListFunctions.Engine/Validation/`. For example, `[ValidateScriptVariable]` rejects a `-HashCodeScript` that doesn't use `$_`, `$this`, `$PSItem`, or `$args[0]`, even a placeholder such as `{ 0 }` in a test that isn't about hash codes. A call that fails on input the cmdlet rejects says nothing about the code under test. The same goes for repros and smoke tests.
- Take each expected value from how the module is meant to work, as `README.md` describes it, and from what native PowerShell does in the same situation in both editions, not from what the code returns today, which may be the bug. For example, a typed collection stores what `$list.Add($x)` stores for the same type, so `$null` becomes `0` in a `List[int]`. When an expected value isn't obvious, say in a comment where it comes from, as the `New-List` tests do.
- Write each test to pass unchanged in both editions. None of the existing tests skips an edition. When the runtimes differ underneath, hide the difference in a helper, the way `Get-BucketCount.ps1` finds the bucket array under a different private field name in each runtime. If a behavior can't match in both editions, ask before you write a test that skips one.
- Test files also run in Windows PowerShell 5.1, so they can't use PowerShell 7 syntax such as `??`, the ternary operator, or `&&`. Keep them ASCII, because 5.1 reads a UTF-8 file without a BOM as ANSI. Indent with tabs, as the existing files do.

## Writing an Engine test

The test folders mirror Engine's. The tests for `Modern/HashBlock.cs` are in `Modern/HashBlockTests.cs`, in the namespace `ListFunctions.Engine.Tests.Modern`. A type that has no test class yet gets a new one, in this shape:

```csharp
using ListFunctions.Modern;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class HashBlockTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public HashBlockTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Fact]
	public void GetHashCode_PassesTheObjectAsTheFirstArgument()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create("$args[0].Length"));

		Assert.Equal(4, block.GetHashCode("abcd", additionalVariables: null));
	}
}
```

- A script block runs only on a thread whose `Runspace.DefaultRunspace` is set, and that property is thread-static. Each test class takes a `RunspaceFixture` through `IClassFixture<RunspaceFixture>`, and each test that runs a script block starts with `using RunspaceScope scope = _runspace.Enter();`.
- Name each test `Subject_Behavior`, usually the member it calls followed by the behavior it checks, such as `Compare_SortsNullFirstWithoutRunningTheScript`. Use `[Theory]` with `[InlineData]` for cases that differ only in their data.
- Pass a `null` argument by name, as in `additionalVariables: null`, so the call shows which parameter it leaves empty.
- `Assert.IsType<T>` returns its argument as a `T`, and `Assert.Throws<T>` returns the exception, so a single call both checks the value and hands it on to the next assertion.
- Every test also builds and runs for `net48`, against the Windows PowerShell 5 reference assemblies, so an API that only .NET 10 or PowerShell 7 has breaks the build.
- The code style in `CLAUDE.md` applies, and the test project already follows it. Test classes and test methods have no XML documentation comments. Fixtures and helper types, such as `RunspaceFixture`, do.

## Repros and smoke tests

The version rule under PowerShell versions applies to repros and smoke tests, and so does the attribute check under Writing a Pester test. In PowerShell 7, import `ListFunctions.Next.dll` from `src/engine/ListFunctions-Next/bin/<configuration>/net10.0/`. In Windows PowerShell 5.1, import `ListFunctions.NETFramework.dll` from `src/engine/ListFunctions-NETFramework/bin/<configuration>/net48/`. Import it in a new process, as `Invoke-Tests.ps1` does. A process that has loaded the DLL keeps the file locked until it exits, so the next build can't replace it.
