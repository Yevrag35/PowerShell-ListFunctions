# Binding input and parameters

## Pipeline input and `-InputObject`

Measured 2026-10-03 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100 with a throwaway `Add-Type` cmdlet.

- **Pipeline to an `[object]` parameter:** every non-null object arrives wrapped in a `PSObject`, even an `[int]`. A piped array arrives as one `PSObject` around the `object[]`. `$null` arrives as `$null`.
- **Pipeline to an `[object[]]` parameter:** a scalar arrives in a one-element array, unwrapped from its `PSObject` unless it's a `[pscustomobject]`, and its instance notes are dropped. An array binds as itself, so it's flattened.
- **`-InputObject` to an `[object[]]` parameter:** only an `IList`, after unwrapping, supplies its elements. A hashtable, a `HashSet[T]`, a `Queue`, a string, or a LINQ iterator is one element, and a single `PSObject`-wrapped argument keeps its wrapper.
- PowerShell never enumerates a string, whether in the pipeline, in `foreach`, or in its operators.

## Enumerating a value the way the pipeline does

Measured 2026-10-06 in both editions.

- `LanguagePrimitives.GetEnumerator` gives exactly the pipeline's elements for every value tried: arrays, lists, sets, queues, stacks, LINQ iterators, a 2-D array (all its cells), a `BitArray`, a `NameValueCollection` (its keys), a `DataTable` (its rows), a `PSObject` around an array, and enumerators such as `$hashtable.GetEnumerator()`.
- `LanguagePrimitives.GetEnumerable` matches too, except that it treats an `IEnumerator` as one element, while the pipeline enumerates it.
- Both return `null` for strings, dictionaries, an `XmlNode`, custom objects, and scalars, which the pipeline sends as one object, and for `$null`, which the pipeline sends as one `$null`.
- Neither follows the `-InputObject` rule above. Both enumerate sets, queues, and LINQ iterators.
- From script, Windows PowerShell 5.1 can't call `GetEnumerator()` on LINQ's private iterator types ("Cannot find an overload"). That looks like a difference between the editions, but isn't one: C# can call it in both.

## Dynamic parameters

Measured 2026-10-06 in both editions, with the dynamic `-CaseSensitive` switch of New-SortedSet, New-HashSet, and New-Dictionary.

- `New-HashSet [int] -CaseSensitive` fails with `NamedParameterNotFound`. `GetDynamicParameters` sees `GenericType` = `[int]` and offers no switch.
- `New-HashSet -CaseSensitive [int]` succeeds and silently ignores the switch. While `-CaseSensitive` is still unknown, positional binding would take the argument after it as its value, so PowerShell calls `GetDynamicParameters` with `GenericType` unbound, gets the switch, and binds `[int]` by position only afterward.
- A named `-GenericType ([int])` binds before PowerShell calls `GetDynamicParameters`, in either order.
- A dynamic parameter whose only parameter set excludes a bound static parameter fails with `ParameterNotInParameterSet` ("Parameter 'CaseSensitive' cannot be specified in parameter set 'WithComparingScript'."). That's a clearer error than the `NamedParameterNotFound` that withholding the parameter gives.

## Arguments to a script block's parameters

Measured 2026-10-07 in both editions, by calling `ScriptBlock.InvokeWithContext` from script.

- **Binding:** `InvokeWithContext` doesn't use the command parameter binder. It binds its `args` array to the declared parameters in the order they're declared, whatever their attributes: `[Parameter(Position = n)]`, `[CmdletBinding(PositionalBinding = $false)]`, and `ValueFromRemainingArguments` change nothing, and a `[switch]` parameter takes an argument too, so a `1` fails to convert to it. The arguments left over go to `$args`, even in a `[CmdletBinding()]` script block. So with `param($a)` and two arguments, `$a` holds the first, and `$args[0]` the second.
- **Variables:** the variables that `InvokeWithContext` defines stay defined in a script block with a `param()` block, but a parameter with the same name replaces one. With `$x` and `$y` defined, `{ param($y, $x) }` gets the first argument in `$y`.
- **A function's script block:** `${function:Test-It}` and `(Get-Command Test-It).ScriptBlock` are the same object, and its `Ast` is a `FunctionDefinitionAst`. `FindAll` from that node with `searchNestedScriptBlocks` set to `$false` finds nothing, because the body counts as nested, so search `.Body` instead. The parameters are in `.Parameters` for `function Test-It($a)`, and in `.Body.ParamBlock` for `function Test-It { param($a) }`, and the other one is `$null`. `InvokeWithContext` runs the function's body like any other script block, and a filter's statements are in its `process` block.
- **Called functions:** a function that the script block calls sees the `$_` that `InvokeWithContext` defines, whether or not the call passes `$_`. With `$_` set to 3, `{ Test-Under }` and `{ Test-Under $_ }` both give `True` when `Test-Under` is `function Test-Under { $_ -gt 2 }`.
- **Searching a script block's syntax tree:** `FindAll` from a `ScriptBlockAst` also finds the variables in its `param()` block, such as the declared names and a default value like `$_` in `param($n = $_)`. A search from the `NamedBlockAst` that runs, such as `.EndBlock`, leaves out the `param()` block and, with `searchNestedScriptBlocks` set to `$false`, the nested script blocks. `ParameterAst.Name.Extent.Text` is the name as it's written, such as `$a` or `${b c}`, without the parameter's type or attributes.

## In ListFunctions

- `ListFunctionCmdletBase.GetInputElements` follows the pipeline and `-InputObject` rules above. It unwraps each pipeline object with Engine's `PSObjectExtensions.GetBaseObject`, which, like the pipeline, keeps a custom object wrapped.
- Find-Index and Find-LastIndex expand `-InputObject` the way piping it would, through `ListFunctionCmdletBase.GetSearchElements`, which calls `LanguagePrimitives.GetEnumerator`. It warns when the argument is one element, except for a string. The other cmdlets keep the `IList` rule.
- `GetDynamicParameters` alone can't validate a dynamic parameter against a positional argument. Check it again in `BeginCore`, against the final values, as `NewSortedSetCmdlet.BeginCore` does. New-HashSet and New-Dictionary don't check yet, so they still ignore a `-CaseSensitive` that comes before a positional type, as in `New-HashSet -CaseSensitive [int]`. The same check in `EqualityConstructingCmdlet<T>.BeginCore` would close the gap.
- Every script block parameter runs through `InvokeWithContext` with the elements as its arguments, so a `param()` block receives them. `[ValidateScriptVariable]` follows the rules above: it searches only the block that runs, in a function's body too, and accepts the parameter that receives an element, or `$args[i]` for the element at index `i` plus the number of parameters. It doesn't look inside the functions that a script block calls, so it rejects `{ Test-Under }` although that works. `ScriptBlockExtensions.TryGetBody` gives the body and the parameters for both kinds of syntax tree.
