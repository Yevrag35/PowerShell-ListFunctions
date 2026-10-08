---
name: lf-pwsh-internals
description: Measured PowerShell engine behavior that ListFunctions depends on, in Windows PowerShell 5.1 and PowerShell 7. Covers how pipeline input, `-InputObject`, and dynamic parameters bind, PSObject wrapping and AutomationNull, how errors and `break` in a script block that a cmdlet runs end a statement or the whole script, what converting `$null` to an element type gives, how Windows PowerShell 5.1 resolves the module's assemblies, and the traps in measuring any of it. Use it before you write or change code that reads cmdlet input, unwraps a PSObject, runs a script block or catches its exceptions, converts elements, adds a dynamic parameter, or touches assembly loading or ModuleInitializer, before you answer a question about how PowerShell behaves in these areas, and before you run an experiment to find out.
---

# PowerShell internals for ListFunctions

ListFunctions mirrors native PowerShell wherever it can, so its code depends on details of the PowerShell engine that the documentation leaves out. This skill records those details as measured in Windows PowerShell 5.1.26100 and PowerShell 7.6.6, with the date and, where there was one, the review item or bug that the measurement was for. A fact holds in both editions unless it says otherwise.

- Trust a measurement here over recollection. When a change depends on a fact that a newer PowerShell release could change, measure it again.
- When you measure something that later work could depend on, add it to the reference it belongs to, with the versions and the date, instead of saving it as a memory. Say what it means for ListFunctions beside it.

## References

Read the reference for the area you're working in. Each one also says what its facts mean for ListFunctions.

- `references/binding.md`: how pipeline input and `-InputObject` bind to `[object]` and `[object[]]` parameters, which values `LanguagePrimitives.GetEnumerator` enumerates, and when PowerShell calls `GetDynamicParameters`. Read it before you change how a cmdlet reads its input, or add or change a dynamic parameter.
- `references/psobject.md`: PowerShell 7's `PSObject(int)` constructor, each edition's private `PSObject` members, how to tell a custom object from a wrapped value, `PSObject.BaseObject` versus Engine's `GetBaseObject`, and where `AutomationNull.Value` reaches C# code. Read it before you unwrap a `PSObject`, build one in C#, or handle a command that outputs nothing.
- `references/errors.md`: how a written error, a `throw`, a failed method call, and `break` in a script block end the statement or the whole script, which exception types carry them, what wrapping them changes, what `SilentlyContinue`, `Ignore`, and `$Error` do, and the traps in measuring it. Read it before you catch, wrap, or rethrow an exception from a script block, or change how a cmdlet reports one.
- `references/conversions.md`: the rule that a typed collection stores what `$list.Add($x)` stores, and what converting `$null` gives for each kind of type. Read it before you change how elements or dictionary values convert.
- `references/assembly-loading.md`: how Windows PowerShell 5.1 resolves the module's dependencies and why `ModuleInitializer` exists, what PowerShell does on each import, why Engine type names don't resolve right after the import, and how to load the build's DLLs in a script. Read it before you change `ModuleInitializer` or the module's dependencies, or chase a `FileNotFoundException` or `FileLoadException` in Windows PowerShell 5.1.

## Measuring PowerShell behavior

- Measure in both editions, on stable releases (see PowerShell versions in the `lf-testing` skill), and record the versions and the date with each result.
- Run each experiment in a new process, with `pwsh -NoProfile -Command ...` or `powershell.exe -NoProfile -Command ...`. A process that has loaded the build keeps its DLLs locked until it exits, and PowerShell 7 can't load two builds of the same assembly in one process (see `references/assembly-loading.md`).
- When an experiment imports the module, follow the checklist under Repros and smoke tests in the `lf-testing` skill. Otherwise a failed import can let an installed ListFunctions 3.1.0 answer instead, without an error.
- To pass an array to a script, use `-Command "& '<script>' -Path a, b"`. Under `-File`, `-Path a,b` arrives as the single string `'a,b'`.
- `Add-Type` in Windows PowerShell 5.1 compiles C# 5, so scratch C# that runs in both editions can't use pattern matching such as `is PSObject p`, string interpolation, `?.`, or `nameof`. Read a non-public member through reflection, by the name it has in each edition.
- In script, `$psObject.BaseObject` reads a member of the wrapped object, which is usually `$null`. Use `$psObject.psobject.BaseObject`.
- Pester runs each test inside a `try` block, where an error that ends a statement and one that ends the script both jump to `catch`. To see how an error ends a script, run it with `tests/Invoke-InNewRunspace.ps1` or in a new process (see `references/errors.md`).
- When an experiment or a test should show that a change makes a difference, also run it against the build from before the change, and check that the difference shows.
