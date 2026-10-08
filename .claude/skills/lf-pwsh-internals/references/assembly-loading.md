# Assembly loading

## Resolving dependencies in Windows PowerShell 5.1

Measured 2026-10-05 on Windows PowerShell 5.1.26100.9444 with the Debug `net48` build, for review item 43.

- `ResolveEventArgs.RequestingAssembly` is `null` for a static reference from a module's assembly, including one that `Import-Module` loaded for another module. The only non-null requester seen was `Anonymously Hosted DynamicMethods Assembly`, a dynamic assembly, for `[Reflection.Assembly]::Load()` called from script. Satellite `.resources` lookups have a `null` requester too. So a requester check can't tell one module's binds from another's.
- The runtime rejects a handler's result whose public key token is wrong (`FileLoadException`, 0x80131040), but accepts any version, including a downgrade.
- The runtime reuses each answer: once a handler returns an assembly for an identity, binding that identity again raises no new event.
- `Assembly.LoadFile` and `Assembly.LoadFrom` on the same path return the same `Assembly` object, in either order. No duplicate copies appeared in the session.
- The runtime calls `AssemblyResolve` handlers in registration order and stops at the first non-null result. PowerShell registers two handlers of its own, from `LanguagePrimitives` and `ExecutionContext`, ahead of any module's. To see whether a handler answered an event, register a logging handler before it and another after it.
- PowerShell calls `IModuleAssemblyInitializer.OnImport` on every import, including a re-import after `Remove-Module` and an `Import-Module -Force`, so `OnImport` has to guard against registering a handler twice.

## Type names and lazy loading

PowerShell resolves a type name only against assemblies that are already loaded, and the runtime loads an assembly the first time code needs it. Importing `ListFunctions.Next.dll` or `ListFunctions.NETFramework.dll` doesn't load `ListFunctions.Engine.dll`, so until a cmdlet runs, `'ListFunctions.Modern.Exceptions.HashCodeScriptException' -as [type]` is `$null` and a type literal fails with "Unable to find type", in both editions (seen 2026-10-07). The checklist under Repros and smoke tests in the `lf-testing` skill works around it.

## Loading a build's DLLs in a script

To inspect a build, such as counting the public types each build exports with `Assembly.GetExportedTypes()` (review item 45), load each DLL with `[Reflection.Assembly]::LoadFrom` in a new process, so the build output stays unlocked.

- `src/engine/ListFunctions-Next/bin/Debug/net10.0/` holds the `net10.0` Engine and `ListFunctions.Next.dll`.
- `src/engine/ListFunctions-NETFramework/bin/Debug/net48/` holds the `netstandard2.0` Engine and `ListFunctions.NETFramework.dll`.
- Load each Engine build in its own PowerShell 7 process. Both are `ListFunctions.Engine, Version=4.0.0.0`, so a second `LoadFrom` in the same process throws "Assembly with same name is already loaded". In a `foreach` loop that error doesn't stop the script, the variable keeps the first assembly, and the second path prints the first build's results under its own name (found 2026-10-07). DLLs whose assembly names differ, such as an Engine build and `ListFunctions.Next.dll`, can share a process.
- In Windows PowerShell 5.1, `GetExportedTypes()` on the `netstandard2.0` Engine throws `FileNotFoundException` for `System.Collections.Immutable, Version=10.0.0.0`, because nothing runs the module's resolver (found 2026-10-07). `ListFunctions.NETFramework.dll` itself works in 5.1, and PowerShell 7 loads the `netstandard2.0` Engine without help.

## In ListFunctions

Engine's `netstandard2.0` build and ZLinq, which ships only for `netstandard2.0`, reference the assembly versions of their dependencies' `netstandard2.0` builds. The `net48` output holds the `net462` builds of the same packages, which have higher assembly versions. For example, System.Memory 4.6.3 is 4.0.2.0 versus 4.0.5.0, and System.Collections.Immutable 10.0.12 is 10.0.0.0 versus 10.0.0.12. An application would add binding redirects, but a module can't. Running all nine cmdlets raises five resolve events that only ListFunctions answers: System.Memory 4.0.2.0, System.Collections.Immutable 10.0.0.0, Microsoft.Bcl.Memory 10.0.0.5, System.Runtime.CompilerServices.Unsafe 6.0.0.0, and System.Buffers 4.0.2.0. Four of them come from ZLinq's references.

- Since 2026-10-05, `ModuleInitializer` in `src/engine/ListFunctions-NETFramework/` answers only when an assembly loaded from the module's folder references the exact requested identity, and its XML docs explain why. On the same traffic, it answers all five of the module's own events and declines every foreign one.
- Its uncached scan takes about 157 microseconds when it finds the identity and 463 when it doesn't, and it runs about five times per session, because the runtime reuses each answer.
- Don't build the list of references in `OnImport`. Right after the import, only `ListFunctions.NETFramework` is loaded, and it references none of the five identities.
- `OnImport` registers the handler only on its first call, because PowerShell calls it on every import.
