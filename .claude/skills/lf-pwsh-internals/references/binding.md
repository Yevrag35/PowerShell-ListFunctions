# Binding input and parameters

## Pipeline input and `-InputObject`

Measured 2026-10-03 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100 with a throwaway `Add-Type` cmdlet, while fixing bug 06.

- **Pipeline to an `[object]` parameter:** every non-null object arrives wrapped in a `PSObject`, even an `[int]`. A piped array arrives as one `PSObject` around the `object[]`. `$null` arrives as `$null`.
- **Pipeline to an `[object[]]` parameter:** a scalar arrives in a one-element array, unwrapped from its `PSObject` unless it's a `[pscustomobject]`, and its instance notes are dropped. An array binds as itself, so it's flattened.
- **`-InputObject` to an `[object[]]` parameter:** only an `IList`, after unwrapping, supplies its elements. A hashtable, a `HashSet[T]`, a `Queue`, a string, or a LINQ iterator is one element, and a single `PSObject`-wrapped argument keeps its wrapper.
- PowerShell never enumerates a string, whether in the pipeline, in `foreach`, or in its operators.

## Enumerating a value the way the pipeline does

Measured 2026-10-06 in both editions, for review item 39.

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

## In ListFunctions

- `ListFunctionCmdletBase.GetInputElements` follows the pipeline and `-InputObject` rules above. It unwraps each pipeline object with Engine's `PSObjectExtensions.GetBaseObject`, which, like the pipeline, keeps a custom object wrapped.
- Since review item 39, Find-Index and Find-LastIndex expand `-InputObject` the way piping it would, through `ListFunctionCmdletBase.GetSearchElements`, which calls `LanguagePrimitives.GetEnumerator`. It warns when the argument is one element, except for a string. The other cmdlets keep the `IList` rule.
- `GetDynamicParameters` alone can't validate a dynamic parameter against a positional argument. Check it again in `BeginCore`, against the final values, as `NewSortedSetCmdlet.BeginCore` does. New-HashSet and New-Dictionary still ignore a `-CaseSensitive` that comes before a positional type, and review item 37 records that gap under "Not changed".
