# Errors from script blocks

How an error in a script block that a cmdlet runs ends a statement or the whole script. Measured 2026-10-03 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100, unless a fact gives another date.

## How ForEach-Object behaves

ListFunctions takes `ForEach-Object` as the reference, with a script block that sets `$ErrorActionPreference` in a child scope: `{ & { $ErrorActionPreference = 'Stop'; ... } }`.

- `Write-Error` under `Stop` raises an `ActionPreferenceStopException`, which ends the whole script with the original error record.
- `throw` ends the whole script with its own record: `throw 'boom'` gives the fully qualified error ID `boom`, with no cmdlet name. Under `SilentlyContinue` it's suppressed completely.
- A failed method call, such as `$null.Foo()`, under `Stop` ends only the statement. PowerShell wraps it in a record that keeps its ID and category and adds the cmdlet: `InvokeMethodOnNull,<cmdlet type>`.
- `break` leaves the enclosing loop.
- A statement-ending error from a cmdlet ignores the cmdlet's own `-ErrorAction`. `-ErrorAction SilentlyContinue` doesn't hide it.

## What a cmdlet's handling changes

- A cmdlet that catches these and calls `ThrowTerminatingError` makes every one of them end only the statement, and turns `break` into a `BreakException` error.
- Wrapping the exception changes the outcome too (measured 2026-10-04 with an `Add-Type` test cmdlet that rethrows from `ProcessRecord`). A `RuntimeException` subclass that wraps an `ActionPreferenceStopException`, such as one from `HashCodeScriptException.FromBlockException`, ends only the statement. One that copies `WasThrownFromThrowStatement` from a `throw` still ends the script. A wrapped `BreakException` can't leave the loop.

## Exception types

Seen from C# around `InvokeWithContext` (measured 2026-10-05):

- `throw 'x'` is exactly a `RuntimeException`.
- `Write-Error` under `Stop` is an `ActionPreferenceStopException`. Its `ErrorRecord` is the record of the error that caused the stop, so its `ToString()` is that error's message, not "The running command stopped because..." (measured 2026-10-06).
- `break` is a `BreakException`, which is a `FlowControlException`, not a `RuntimeException`.
- A script block with a `begin` block gives a `PSInvalidOperationException`, which derives from `InvalidOperationException`, not `RuntimeException`. Likewise, `PSInvalidCastException` derives from `InvalidCastException` (checked 2026-10-07). So C# overload resolution sends either one to an `Exception` overload, not a `RuntimeException` one. Don't call an `Exception` overload unused just because a `RuntimeException` overload sits beside it, as in `ComparingScriptException.FromBlockException<T>`: these exceptions reach it.

## Preferences inside the script block

Measured 2026-10-06.

- Under `SilentlyContinue`, a written error, a `throw`, and a failed method call are all hidden, and the script block goes on to its next statement. But when any `try` block encloses the command, even the caller's or Pester's, the `throw` and the failed method call go to that `try` block instead.
- `-ErrorAction Stop` on a command inside the script block still ends the whole script.
- `$Error` also records errors that are caught in a `try` block, hidden with `-ErrorAction SilentlyContinue`, or redirected with `2>$null`, but not ones hidden with `-ErrorAction Ignore`. So `$Error` can't tell which errors a preference hid.
- `Ignore` passed to `InvokeWithContext` as the `$ErrorActionPreference` variable differs between the editions. Windows PowerShell 5.1 writes "The value Ignore is not supported for an ActionPreference variable..." for each error and falls back to `Continue`. PowerShell 7.6 hides the errors and doesn't record written ones in `$Error`. `Suspend` gives a `NotSupportedException` in both. Oddly, `$ErrorActionPreference = 'Ignore'` assigned in a 5.1 script hid a `Write-Error` without the "not supported" error (not investigated).
- `-WarningVariable` collects a cmdlet's warnings even under `-WarningAction SilentlyContinue`.

## Measuring traps

- `ForEach-Object` runs its script block in the caller's scope, so setting `$ErrorActionPreference` directly inside it changes the caller's preference and confounds the comparison. Use the child-scope form above.
- Inside any `try` block, an error that ends a statement and one that ends the script both jump to `catch`, and Pester runs every test inside one. Use `tests/Invoke-InNewRunspace.ps1`, which runs a script in a new runspace and returns its `Output`, `Errors`, and `StoppedBy`. The `lf-testing` skill explains how to tell its results apart.

## In ListFunctions

- `ListFunctionCmdletBase.PassesThrough` lets a `RuntimeException` or a `FlowControlException` reach PowerShell unchanged, so PowerShell handles it the way it does from `ForEach-Object`. The base class applies it to exceptions from `BeginCore` and `ProcessCore`. That makes the four condition cmdlets and ConvertTo-Dictionary's selectors match `ForEach-Object`.
- The comparer script blocks of New-HashSet, New-SortedSet, and New-Dictionary match `ForEach-Object` too. The collection's `Add` method runs through reflection, so the cmdlets rethrow the unwrapped exception with `RethrowIfPassesThrough`.
- A condition cmdlet whose `-ScriptBlockErrorAction` is `SilentlyContinue` or `Ignore` runs the condition under `Stop` and writes each error that the condition doesn't handle as a warning, through `ListFunctionCmdletBase.CreateConditionFilter`. Don't switch to comparing `$Error` before and after: `$Error` can't tell which errors the preference hid.
