# PSObject and AutomationNull

Measured 2026-10-03 in PowerShell 7.6.6 and Windows PowerShell 5.1.26100, from `Add-Type` C# that uses reflection, unless a fact gives another date.

## Building a PSObject in C#

PowerShell 7 has a public `PSObject(int)` constructor, which sets the capacity for instance members. So in C# compiled against PowerShell 7, `new PSObject(42)` builds an empty custom object, not a wrapped 42. The PowerShell 5 reference assemblies have no such overload, so the same code wraps 42 on `net48` and doesn't on `net10.0`. Use `PSObject.AsPSObject(42)` or `new PSObject((object)42)`.

## Members

- The private members differ. Windows PowerShell 5.1 has the fields `immediateBaseObject` and `immediateBaseObjectIsEmpty`. PowerShell 7.6.6 has the field `_immediateBaseObject` and the non-public property `ImmediateBaseObjectIsEmpty`, which reads a bit in `_flags`.
- `ImmediateBaseObject` is public in both editions, and `PSCustomObject` is a public type, in PowerShellStandard.Library 5.1.1 too.
- **`ImmediateBaseObject is PSCustomObject` always equals the private empty flag.** Checked on 2026-10-03 for `[pscustomobject]`, `New-Object PSObject`, `new PSObject(<a PSCustomObject>)`, `AutomationNull.Value`, and ordinary values, and on 2026-10-06 for deserialized objects from PSSerializer, `Export-Clixml` and `Import-Clixml`, and `Start-Job` output: 166 PSObjects per edition, no mismatch. The PowerShell 7.6.6 source agrees. Only `MshObject.cs` writes the flag, in the constructors through `CommonInitialization`, in `Copy`, and in `SetCoreOnDeserialization`, and every write keeps it equal to the test. The deserializer creates each `<Obj>` as `new PSObject()`.
- The public `PSObject.BaseObject` unwraps through a nested custom object to its `PSCustomObject` placeholder, in both editions (measured 2026-10-06). Only the internal `PSObject.Base` stops at the custom object.
- `PSObject` overrides `Equals` (PowerShell 7.6.6 source): reference equality first, then `false` whenever `this.BaseObject` is the `PSCustomObject` placeholder, and otherwise `LanguagePrimitives.Equals(BaseObject, obj)`.

## AutomationNull

- `AutomationNull.Value` is an empty `new PSObject()`. Its empty flag is true, and its immediate base object is a `PSCustomObject`.
- The parameter binder turns `(& {})` and `$x = & {}` into `null` before transformation attributes and cmdlet parameters see them, and `$null | cmd` gives `null`. `InvokeWithContext` drops it from its output. `ScriptBlock.InvokeReturnAsIs()` on an empty script does return it.
- Piping a variable that holds it sends no pipeline input at all (measured 2026-10-06). After `$x = & {}`, or a `$x = Get-ChildItem` that matches nothing, `$x | ForEach-Object { 'ran' }` outputs nothing, and `$x | New-HashSet` is empty. After `$x = $null`, `$x | New-HashSet` holds one `$null`.

## In ListFunctions

- Engine's `PSObjectExtensions.GetBaseObject`, in `src/engine/ListFunctions.Engine/Extensions/PSObjectExtensions.cs`, unwraps one layer at a time through `ImmediateBaseObject` and stops at a `PSCustomObject`, so a custom object stays wrapped, the way the pipeline leaves it. Until 2026-10-06, it read the private members above. Don't reintroduce private reads, and don't replace its loop with `PSObject.BaseObject`. `src/engine/ListFunctions.Engine.Tests/Extensions/PSObjectExtensionsTests.cs` guards both.
- Engine tests and repros never build a wrapped value with `new PSObject(<int>)`.
