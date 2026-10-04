# ListFunctions bug list

> **Historical record only.** This document is kept only as evidence of the bugs it lists and how they were fixed. It doesn't provide any guidance about the future direction of the codebase. Its fix ideas, decisions, test rules, and `BugNN` tags describe the code as it was when each item was written, and they don't apply to new work.

Found while rewriting `README.md` on 2026-09-28. The README describes how the module is meant to work, so each item under **README accuracy** makes a README statement false until it's fixed.

## Checklist

**README accuracy**

- [x] 01 — `-HashCodeScript`'s return value is ignored
- [x] 02 — `-Capacity` does nothing on New-HashSet and New-Dictionary
- [x] 03 — New-HashSet can't combine `-GenericType` with `-CaseSensitive`
- [x] 04 — New-List ignores `-IncludeNullElements` unless `-GenericType` is given

**Other bugs**

- [x] 05 — `$args[0]` and `$args[1]` pass validation but are always `$null`
- [x] 06 — Piped `$null` and array elements are miscounted
- [x] 07 — New-Dictionary ignores the equality scripts when it copies from `-InputObject`
- [x] 08 — New-Dictionary doesn't convert copied values to `-ValueType`
- [x] 09 — New-Dictionary can't use script block equality with value-type keys
- [x] 10 — A `-ComparingScript` result that isn't an `[int]` silently means "equal"
- [x] 11 — A generic type split at a comma silently becomes `[object]`
- [x] 12 — ConvertTo-Dictionary misses `$_` when an operator follows it
- [x] 13 — Assert-AllObject gives different answers for empty input
- [x] 14 — `Debug.Fail` ends the PowerShell process in Debug builds

**Release**

- [ ] 15 — Update the manifest and the shipped DLLs for 4.0.0

**Minor**

- [x] 16 — `-ScriptErrorAction` exists on only two of the four condition cmdlets
- [x] 17 — New-SortedSet silently skips elements it can't convert
- [x] 18 — New-Dictionary's `-CaseSensitive` can't be combined with `-InputObject`
- [x] 19 — ConvertTo-Dictionary stores the whole input object when the value is `$null`
- [x] 20 — New-Dictionary's `[object]` keys turn case-sensitive when `-ValueType` isn't `[object]`
- [x] 21 — Find-LastIndexOf handles condition errors differently from the other condition cmdlets
- [x] 22 — ConvertTo-Dictionary fails on a property name that contains a single quote

## Running the repros

- **PowerShell 7:** run `src/engine/ListFunctions-Next/bin/Debug/net10.0/Debug.ps1`, which is what the `ListFunctions-Next` launch profile does, or import `ListFunctions.Next.dll` from `src/engine/ListFunctions-Next/bin/<configuration>/net10.0/`.
- **Windows PowerShell 5.1:** import `ListFunctions.NETFramework.dll` from `src/engine/ListFunctions-NETFramework/bin/<configuration>/net48/`.
- **Tests:** `tests/Invoke-Tests.ps1` runs the Pester tests in both editions. An item's tests are tagged with its number, so `-Tag Bug06` runs only item 06's tests.
- **Engine tests:** `dotnet test src/engine/ListFunctions.Engine.Tests/ListFunctions.Engine.Tests.csproj -c Debug` runs the xUnit.net tests in both editions. An item's tests have the trait `Category=BugNN`, so adding `--filter-trait "Category=Bug10"` runs only item 10's tests. Don't add `--nologo` or `--no-incremental`, or the run stops with "Zero tests ran".

Every repro was checked against Release builds on Windows PowerShell 5.1.26100 and PowerShell 7.6.6. The two editions behave the same unless an item says otherwise.

## Where an item's tests go

Every item has a **Tests:** line that names the files its tests go in. The rule behind those lines:

- **Pester**, in `tests/<Cmdlet>.Tests.ps1` and tagged `BugNN`: every bug that can surface during normal use of the module. Test it through the cmdlet, the way a user runs into it. A cmdlet that has no test file yet gets a new one.
- **Engine**, in `src/engine/ListFunctions.Engine.Tests/` with `[Trait("Category", "BugNN")]`: every bug whose cause is in `src/engine/ListFunctions.Engine/`. Test the Engine type directly, in the test class that mirrors its source file, such as `Modern/ComparingBlockTests.cs` for `Modern/ComparingBlock.cs`. A type that has no test class yet gets a new one.
- **Both**: an Engine bug that also surfaces through a cmdlet. The Engine test pins down the type's behavior, and the Pester test shows that the cmdlet passes the fix on to the user. Every open item with a cause in Engine is like this.

Both suites run every test in PowerShell 7 and Windows PowerShell 5.1. When an item's fix idea offers a choice, its **Tests:** line says what each choice needs.

## README accuracy

### 01 — `-HashCodeScript`'s return value is ignored

**README:** New-HashSet › Script block equality, and New-Dictionary › Script block key equality ("returns its `[int]` hash code").
**Where:** `src/engine/ListFunctions.Engine/Modern/HashBlock.cs`: `HashBlock.GetHashCode(object, IEnumerable<PSVariable>?)` and `HashBlock.GetHashObjectAsIs`.

```powershell
$set = New-HashSet -EqualityScript { [string]::Equals($x, $y, 'OrdinalIgnoreCase') } -HashCodeScript { $_.ToUpperInvariant().GetHashCode() }
$set.Add('abc')     # True
$set.Add('ABC')     # Expected: False. Actual: True
```

**Cause:** `GetHashCode` stores the script's result in `hashObj`, then returns `obj?.GetHashCode()`. The script runs, and its errors surface, but its value is never used. The README examples work only because every `[pscustomobject]` has the same hash code, which sends every comparison to `-EqualityScript`.

The no-variables path, `GetHashObjectAsIs`, has two more problems. It runs the script with `InvokeReturnAsIs(obj)`, which sets `$args[0]` instead of `$_`, and it returns `obj` (or its first non-null element) instead of the script's result. The cmdlets always pass `$ErrorActionPreference` as an extra variable, so they never take that path.

**Fix idea:** Convert the script's first output to `[int]` and return it. Throw `HashCodeScriptException` if there's no output or it can't be converted.

**Fixed:** `HashBlock.GetHashCode` converts the script's first output to `[int]` with `LanguagePrimitives.ConvertTo` and returns it. Output that can't be converted throws `HashCodeScriptException`, as no output and a `$null` output already did. `GetHashObjectAsIs` is gone, so every call sets `$_`, `$this`, and `$PSItem`, with or without additional variables. The `Bug01` tests in `tests/New-HashSet.Tests.ps1` and `tests/New-Dictionary.Tests.ps1` cover the returned hash code, the conversion, both error cases, and duplicates found through `Add`, `ContainsKey`, and pipeline input.

**Tests:** Both, and written.

- Pester: `Bug01` in `tests/New-HashSet.Tests.ps1` and `tests/New-Dictionary.Tests.ps1`.
- Engine: `Category=Bug01` in `Modern/HashBlockTests.cs`, for the hash code that the script block computes from `$_`, `$this`, or `$PSItem`, with and without additional variables, its conversion to `int`, and the `HashCodeScriptException` for no output, a `$null` output, output that can't be converted, or a `throw`. The tests that check a hash code, and the one for output that can't be converted, fail when `GetHashCode` returns `obj.GetHashCode()` again.

### 02 — `-Capacity` does nothing on New-HashSet and New-Dictionary

**README:** the `-Capacity` rows in the New-HashSet and New-Dictionary parameter tables.
**Where:** `EqualityConstructingCmdlet<T>.Capacity` is never read (`src/engine/ListFunctions-Next/Cmdlets/Constructs/EqualityConstructingCmdlet.cs`). In `src/engine/ListFunctions.Engine/Modern/Constructors/`, `EqualityCollectionCtor.GetConstructorArguments` yields only the comparer, and `HashSetCtor.ConstructTDefault` and `DictionaryCtor.ConstructTDefault` don't take a capacity.

```powershell
# PowerShell 7 only: .NET Framework has no EnsureCapacity.
(New-HashSet [int] -Capacity 1000).EnsureCapacity(0)                # Expected: 1000 or more. Actual: 0
(New-Dictionary [string] [int] -Capacity 1000).EnsureCapacity(0)    # Expected: 1000 or more. Actual: 0
(New-List [int] -Capacity 1000).Capacity                            # 1000, so New-List is fine
```

**Fix idea:** Pass the capacity through to the constructor classes and use the `(int capacity, IEqualityComparer)` overloads, including in the default paths (`Hashtable` and `HashSet<object>`). The `HashSet<T>(int, IEqualityComparer<T>)` constructor isn't in `netstandard2.0`, so the Engine's `netstandard2.0` build can't call it directly; it does exist at run time on .NET Framework 4.7.2 and later.

**Fixed:** `EqualityCollectionCtor` has a `Capacity` property, which `EqualityConstructingCmdlet.BeginCore` sets from `-Capacity`. `GetConstructorArguments` yields the capacity before the comparer, so reflection now calls the `(int, IEqualityComparer<T>)` constructors. The default paths call `Hashtable(int, IEqualityComparer)` and `HashSet<object>(int, IEqualityComparer<object>)`, which the `netstandard2.0` build reaches through `Activator.CreateInstance`. A capacity of 0 creates the same collections as before. The Pester `Bug02` tests measure the bucket array with `tests/Get-BucketCount.ps1`, because .NET Framework has no `EnsureCapacity`.

**Tests:** Both, and written.

- Pester: `Bug02` in `tests/New-HashSet.Tests.ps1` and `tests/New-Dictionary.Tests.ps1`.
- Engine: `Category=Bug02` in `Modern/Constructors/HashSetCtorTests.cs` and `Modern/Constructors/DictionaryCtorTests.cs`. They set `Capacity` before calling `Construct`, for sets of `object`, `int`, and `string`, a `Hashtable`, a `Dictionary<string, int>`, and a set and a dictionary with an `EqualityBlock`. They measure the bucket array with the new `BucketCount` helper in `src/engine/ListFunctions.Engine.Tests/BucketCount.cs`, which reads the same private fields as `tests/Get-BucketCount.ps1`. Every one of them fails when the `Capacity` getter returns 0.

### 03 — New-HashSet can't combine `-GenericType` with `-CaseSensitive`

**README:** New-HashSet's `-CaseSensitive` row ("Available when the element type is `[object]` or `[string]`").
**Where:** `src/engine/ListFunctions-Next/Cmdlets/Constructs/NewHashSetCmdlet.cs`. `GenericType` belongs only to the `SpecifiedType` parameter set, and the dynamic `-CaseSensitive` belongs only to `StringSet`.

```powershell
New-HashSet [string] -CaseSensitive     # Error: Parameter 'CaseSensitive' cannot be specified in parameter set 'SpecifiedType'.
New-HashSet ([object]) -CaseSensitive   # Same error
New-HashSet -CaseSensitive              # Works, and creates an [object] set
```

**Fix idea:** Also put `GenericType` in the `StringSet` parameter set, with a second `[Parameter(ParameterSetName = ..., Position = 0)]`. It has to stay out of `WithCustomEquality`.

**Fixed:** `GenericType` has a second `[Parameter]` for `StringSet` at position 0 and stays out of `WithCustomEquality`. Without `-CaseSensitive`, `SpecifiedType` is still chosen, so `[string]` sets stay case-insensitive by default. The `Bug03` tests in `tests/New-HashSet.Tests.ps1` cover `[string]` and `[object]` sets, pipeline input, the case-insensitive default, and the error for `[int]`, which still isn't offered `-CaseSensitive`.

**Tests:** Pester only, and written: `Bug03` in `tests/New-HashSet.Tests.ps1`. The cause was the cmdlet's parameter sets.

### 04 — New-List ignores `-IncludeNullElements` unless `-GenericType` is given

**README:** New-List's description ("`$null` elements are skipped unless you pass `-IncludeNullElements`") and its `-IncludeNullElements` row.
**Where:** `src/engine/ListFunctions-Next/Cmdlets/Constructs/NewListCmdlet.cs`. `_isObjectType` is set only when the `GenericType` getter or setter runs. Without `-GenericType`, the first `ProcessCore` call takes the typed path, `AddTypedItemsToList`, where `TryConvertItem($null, [object])` returns `$false` and drops the `$null`. That path reads `GenericType`, which sets `_isObjectType`, so any later pipeline input takes the object path.

```powershell
(New-List -InputObject 1, $null, 2 -IncludeNullElements).Count               # Expected: 3. Actual: 2
(New-List ([object]) -InputObject 1, $null, 2 -IncludeNullElements).Count    # 3
(1, $null, 2 | New-List ([object]) -IncludeNullElements).Count               # Expected: 3. Actual: 2 (a different cause; see 06)
```

**Fix idea:** Set `_isObjectType` in `BeginCore` from the resolved element type.

**Fixed:** `BeginCore` sets `_isObjectType` from the resolved element type. The `GenericType` getter and setter no longer change it, so the two `SetToObjectType` methods are gone. The `Bug04` tests in `tests/New-List.Tests.ps1` cover `-InputObject`. A piped `$null` is still dropped until 06 is fixed.

**Tests:** Pester only, and written: `Bug04` in `tests/New-List.Tests.ps1`. The cause was in the cmdlet.

**Open question, answered on 2026-09-30:** A typed list converts `$null` instead of adding it. `New-List [int] -InputObject 1, $null -IncludeNullElements` holds `1, 0`, and a `[string]` list gets `''`. Is that intended?

- **Answer:** Yes. A typed list follows PowerShell's conversion rules exactly, so it holds what `List[T].Add($null)` stores when PowerShell calls it: `0` for `[int]` and `''` for `[string]`.
- **Bug it turned up:** `Add($null)` stores a literal `$null` for `[Nullable[T]]` and for most classes, such as `[version]`. New-List dropped that `$null` even with `-IncludeNullElements`, because `ListFunctionCmdletBase.TryConvertItem` reported a `$null` result as a failed conversion.
- **Fixed:** `TryConvertItem` returns `true` whenever `LanguagePrimitives.ConvertTo` doesn't throw, even when the result is `$null`. `AddTypedItemsToList` adds a `$null` result only when `-IncludeNullElements` is set. The switch now decides whether any `$null` reaches a typed list, whether the input was `$null` or converted to `$null`, as `[NullString]::Value` does for `[string]`. Without the switch, every typed list behaves as before. The new `Bug04` tests cover `[int]`, `[string]`, `[Nullable[int]]`, and `[version]` lists with and without the switch, plus `[NullString]::Value` in a `[string]` list.

## Other bugs

### 05 — `$args[0]` and `$args[1]` pass validation but are always `$null`

**Where:** The `ValidateScriptVariable` attributes accept `PSThisVariable.FirstArg` and `SecondArg` for `-Condition` (Assert-AnyObject, Assert-AllObject, Find-IndexOf, Find-LastIndexOf), for `-EqualityScript` and `-HashCodeScript` (New-HashSet), and for `-ComparingScript` (New-SortedSet). At run time, though, `ScriptBlockFilter.IsTrue`, `EqualityBlock.Equals`, `HashBlock.GetHashCode`, and `ComparingBlock<T>.Compare` all call `InvokeWithContext` with an empty `args` array.

```powershell
1, 2, 3 | Any { $args[0] -gt 2 }             # Expected: True. Actual: False
1, 2, 3 | Find-IndexOf { $args[0] -eq 2 }    # Expected: 1. Actual: -1

5, 3, 1 | New-SortedSet [int] -ComparingScript { $args[0].CompareTo($args[1]) }
# Actual: two "Exception has been thrown by the target of an invocation." errors, and the set holds only 5

$set = New-HashSet -EqualityScript { $args[0].Name -eq $args[1].Name } -HashCodeScript { $_.Id.GetHashCode() }
$set.Add([pscustomobject]@{ Id = 1; Name = 'a' })    # True
$set.Add([pscustomobject]@{ Id = 1; Name = 'b' })    # Expected: True. Actual: False, because $null -eq $null
```

**Fix idea:** Pass the elements as `args` to `InvokeWithContext`, or remove `FirstArg` and `SecondArg` from the attributes. ConvertTo-Dictionary already supports `$args[0]`, because it calls its selectors with `ScriptBlock.Invoke(item)`.

**Fixed:** `InvokeWithContext<T>` and the two `TryInvokeWithContext` overloads in `src/engine/ListFunctions.Engine/Extensions/ScriptBlockExtensions.cs` take an `args` array and pass it to `ScriptBlock.InvokeWithContext` in place of the shared empty array. `ScriptBlockFilter.IsTrue` and `HashBlock.GetHashCode` pass the element as `$args[0]`, and `EqualityBlock.Equals` and `ComparingBlock<T>.Compare` pass their operands as `$args[0]` and `$args[1]`, the same values that `$x` and `$y` hold. Each call builds a new array, because PowerShell doesn't copy it: a script block without a `param()` block gets that exact array as `$args`, so a shared array would change under a script block that keeps `$args`. A script block with a `param()` block now gets the elements as its parameters, the way `ScriptBlock.Invoke` does, and `$args` holds only the elements left over. The validation attributes and the cmdlets are unchanged. All four repros give their expected results in both editions, with no errors. The `Bug05` tests cover `$args` in every parameter that **Where:** lists, the order of `$args[0]` and `$args[1]`, and a `$null` or array element, which arrives as a single argument. Each of them fails when `ScriptBlockExtensions.cs` passes an empty array again.

**Tests:** Both, and written. Pester: `Bug05` in `tests/Assert-AnyObject.Tests.ps1`, `tests/Assert-AllObject.Tests.ps1`, `tests/Find-IndexOf.Tests.ps1`, and `tests/Find-LastIndexOf.Tests.ps1` (all new), and in `tests/New-HashSet.Tests.ps1` and `tests/New-SortedSet.Tests.ps1`. Engine: `Category=Bug05` in `Modern/ScriptBlockFilterTests.cs`, `Modern/EqualityBlockTests.cs`, `Modern/HashBlockTests.cs`, and `Modern/ComparingBlockTests.cs`.

**Open question, answered on 2026-10-03:** New-Dictionary's `-EqualityScript` and `-HashCodeScript` run through the same `EqualityBlock` and `HashBlock`, so `$args[0]` and `$args[1]` hold the keys there now too. Its `ValidateScriptVariable` attributes didn't list `FirstArg` and `SecondArg`, though, so New-Dictionary rejected script blocks that New-HashSet accepts, such as `-EqualityScript { $args[0] -eq $args[1] }`. Should it accept them? And should the README, which didn't mention `$args` for any parameter, list it?

- **Answer:** Yes to both.
- **Fixed:** New-Dictionary's `-EqualityScript` accepts `$args[0]` and `$args[1]`, and its `-HashCodeScript` accepts `$args[0]`, the same as New-HashSet's. The README's Script blocks table lists `$args[0]` for script blocks that receive one element, and `$args[0]` and `$args[1]` for those that compare two. The `Bug05` tests in `tests/New-Dictionary.Tests.ps1` cover both parameters.

### 06 — Piped `$null` and array elements are miscounted

**Where:** `InputObject` is an `object[]` that takes pipeline input, so PowerShell binds each pipeline object as an array: a scalar is wrapped in a one-element array, an array binds as itself, and `$null` binds as `$null`. `ProcessCore` then counts the elements of the bound array, so a piped `$null` or empty array counts as zero elements and a piped array counts as its length.

- `FindIndexCmdlet.ProcessCore` doesn't advance `_currentIndex` for `$null` or an empty array, and advances it by the array's length otherwise.
- `FindLastIndexCmdlet.ProcessCore` adds nothing to `_list` for `$null`, and flattens arrays.
- `NewListCmdlet.ProcessCore` returns early for `$null`, so a piped `$null` never reaches `-IncludeNullElements` (04).

```powershell
1, $null, 3 | Find-IndexOf { $_ -eq 3 }                  # Expected: 2. Actual: 1
1, $null, 3 | Find-LastIndexOf { $_ -eq 3 }              # Expected: 2. Actual: 1
@(1, @(), 3) | Find-IndexOf { $_ -eq 3 }                 # Expected: 2. Actual: 1
@(1, @(2, 2), 3) | Find-IndexOf { $_ -eq 3 }             # Expected: 2. Actual: 3
@(1, @(2, 2), 3) | Find-LastIndexOf { $_ -eq 3 }         # Expected: 2. Actual: 3
Find-IndexOf -InputObject @(1, $null, 3) { $_ -eq 3 }    # 2, which is correct
```

**Fix idea:** Count each pipeline object as one element. For example, make `InputObject` a single `object`, treat it as one element when `MyInvocation.ExpectingInput` is true, and enumerate it otherwise.

**Scope, decided on 2026-10-03:** Assert-AnyObject, Assert-AllObject, New-HashSet, New-SortedSet, and ConvertTo-Dictionary bound pipeline input the same way, so they also dropped a piped `$null` and flattened a piped array. For example, `1, $null | Assert-AllObject { $true -or $_ }` returned `$false`, and `@(1, @(2, 3)) | New-HashSet [int]` held 1, 2, and 3. The fix covers all eight cmdlets.

**Fixed:** `InputObject` is a single `object` in all eight cmdlets, and each one gets its elements from the new `ListFunctionCmdletBase.GetInputElements`:

- When `MyInvocation.ExpectingInput` is true, the pipeline object is one element, even when it's `$null` or an array. The method removes the `PSObject` that PowerShell wraps around it unless the object is a custom object. The cmdlets store and test the same values that the `object[]` parameter gave them, so `1, 'abc' | New-List` still holds an `[int]` and a `[string]`.
- Otherwise, the value is the argument of `-InputObject`, and it supplies the same elements as before. A list, such as an array or a `List[T]`, supplies its elements, and `$null` supplies none. Anything else is one element, including a string, a hashtable, and a `HashSet[T]`, which isn't a list.
- A piped array is no longer flattened, so `@(1, @(2, 3)) | New-HashSet [int]` writes a conversion error for `@(2, 3)`. A piped `$null` reaches every cmdlet: New-List adds it with `-IncludeNullElements`, and New-HashSet adds it to an `[object]` set, the way `-InputObject 1, $null` already did.
- ConvertTo-Dictionary infers its key and value types from the first input object that isn't `$null`. It used to infer them from the first element of `-InputObject` even when that element was `$null`, which made the keys `[object]`. A piped `$null` was skipped, and still is.
- The README has a new Input section that describes both kinds of input.

**Tests:** Pester only, and written: `Bug06` in `tests/Find-IndexOf.Tests.ps1`, `tests/Find-LastIndexOf.Tests.ps1`, `tests/New-List.Tests.ps1`, `tests/Assert-AnyObject.Tests.ps1`, `tests/Assert-AllObject.Tests.ps1`, `tests/New-HashSet.Tests.ps1`, `tests/New-SortedSet.Tests.ps1`, and a new `tests/ConvertTo-Dictionary.Tests.ps1`. The cause was in how the cmdlets bind pipeline input. The New-List tests add the piped `$null` case that 04's tests leave out. Tests in the Find-IndexOf, Find-LastIndexOf, New-List, Assert-AnyObject, Assert-AllObject, and ConvertTo-Dictionary files also check that an array passed to `-InputObject` still supplies its elements.

### 07 — New-Dictionary ignores the equality scripts when it copies from `-InputObject`

**Where:** `NewDictionaryCmdlet.GetCustomEqualityComparer` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewDictionaryCmdlet.cs`) only recognizes the `WithCustomEquality` parameter set. When you also pass `-InputObject`, the set is `WithCustomEqualityAndCopy`, so the method falls back to the default comparer and the command returns a plain `Hashtable`. `-ScriptBlockErrorAction` isn't in `WithCustomEqualityAndCopy` either.

```powershell
$d = @{ a = 1 } | New-Dictionary -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() }
$d.GetType().Name    # Expected: Dictionary`2, with an EqualityBlock comparer. Actual: Hashtable

New-Dictionary -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() } -InputObject @{ a = 1 } -ScriptBlockErrorAction Stop
# Error: Parameter set cannot be resolved using the specified named parameters.
```

**Fix idea:** Recognize both set names, and add `ScriptBlockErrorAction` to `WithCustomEqualityAndCopy`.

**Fixed:** `GetCustomEqualityComparer` builds the script block comparer in both `WithCustomEquality` and `WithCustomEqualityAndCopy`, and `ScriptBlockErrorAction` belongs to both sets. Both repros return a `Dictionary[object, object]` whose comparer is an `EqualityBlock`. The `TODO` about this in `EqualityScript`'s XML docs is gone.

**Tests:** Pester only, and written: `Bug07` in `tests/New-Dictionary.Tests.ps1`, for script block equality and for `-ScriptBlockErrorAction`, each with `-InputObject`. The cause was in the cmdlet's parameter sets.

### 08 — New-Dictionary doesn't convert copied values to `-ValueType`

**Where:** `NewDictionaryCmdlet.Process` converts each key with `LanguagePrimitives.ConvertTo(de.Key, this.KeyType)`, but only clones each value, so the `Add` call rejects a value of the wrong type.

```powershell
$d = @{ a = '1' } | New-Dictionary [string] [int]
# Error: Object of type 'System.String' cannot be converted to type 'System.Int32'.
$d.Count             # Expected: 1. Actual: 0
```

**Fix idea:** Convert each value to `ValueType` after cloning it, and report conversion failures the way New-List does.

**Keys, decided on 2026-10-03:** A key that can't be converted to `-KeyType` ended the command with a terminating error, and no dictionary was written. Keys now fail the same way values do.

**Fixed:** `NewDictionaryCmdlet.Process` converts each key to `KeyType`, and each value to `ValueType` after it clones the value, with `ListFunctionCmdletBase.TryConvertItem`. A key or value that can't be converted writes the same `LFInvalidCastException` error that New-List writes, and its entry is skipped. The other entries are still copied. Values aren't converted when `ValueType` is `[object]`, and a `$null` value is still skipped without an error. The repro returns a `Dictionary[string, int]` that holds `a = 1`.

**Tests:** Pester only, and written: `Bug08` in `tests/New-Dictionary.Tests.ps1`, for a converted value, a value and a key that can't be converted, and a value that `-CloneValues` clones before the conversion. The cause was in `NewDictionaryCmdlet.Process`.

### 09 — New-Dictionary can't use script block equality with value-type keys

**Where:** `EqualityBlock` implements `IEqualityComparer<object>`. `Dictionary<string, TValue>` accepts it through contravariance, but `Dictionary<int, TValue>` needs an `IEqualityComparer<int>`, so `GenericCollectionCtor.CallActivator` finds no matching constructor.

```powershell
New-Dictionary [int] -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() }
# Error: An exception occurred attempting to construct an object of type "System.Collections.Generic.Dictionary`2[[System.Int32, ...],[System.Object, ...]]".
```

**Fix idea:** Wrap the `EqualityBlock` in a generic `IEqualityComparer<TKey>` adapter, the way `ComparingBlock<T>` works for comparers. Or reject value-type keys during parameter binding, with a clear message.

**Fixed:** With the adapter, chosen on 2026-10-03. The new `EqualityComparerAdapter<T>` (`src/engine/ListFunctions.Engine/Modern/EqualityComparerAdapter.cs`) implements `IEqualityComparer<T>` and passes every call to a non-generic `IEqualityComparer`, which its `InnerComparer` property returns. `EqualityCollectionCtor.GetComparerOrDefault` wraps the comparer in one when the comparer isn't an `IEqualityComparer<T>` of the key or element type. `DictionaryCtor` and `HashSetCtor` therefore accept an `EqualityBlock` for any value type, including `[Nullable[T]]`. For `[object]` and other reference types, such as `[string]`, the collection gets the `EqualityBlock` itself, through contravariance, as before. The repro returns a `Dictionary[int, object]` whose comparer is an `EqualityComparerAdapter[int]` around the `EqualityBlock`.

**Tests:** Both, and written.

- Pester: `Bug09` in `tests/New-Dictionary.Tests.ps1`, for `[int]` and `[Nullable[int]]` keys, and for copying `-InputObject` into an `[int]`-keyed dictionary. The repro succeeds now, so the `Bug14` test there passes a `-Capacity` of `[int]::MaxValue` instead, which still fails in `GenericCollectionCtor.CallActivator`.
- Engine: `Category=Bug09` in a new `Modern/Constructors/DictionaryCtorTests.cs`, for `int` and `int?` keys, the wrapping, and `[string]` keys that get the `EqualityBlock` itself, and in a new `Modern/EqualityComparerAdapterTests.cs`.

### 10 — A `-ComparingScript` result that isn't an `[int]` silently means "equal"

**Where:** `ComparingBlock<T>.Compare` (`src/engine/ListFunctions.Engine/Modern/ComparingBlock.cs`) converts the script's first output with `LanguagePrimitives.ConvertTo<int>` through `PSVariableCollectionExtensions.GetFirstValue`, which catches a failed conversion and returns `0`. No output also gives `0`. Every pair then compares as equal, so the set keeps only its first element, and no error appears.

```powershell
(5, 3, 1 | New-SortedSet [int] -ComparingScript { 'x' + $x + $y }) -join ','     # Expected: an error. Actual: 5
(5, 3, 1 | New-SortedSet [int] -ComparingScript { $null = $x, $y }) -join ','    # Expected: an error. Actual: 5
```

**Fix idea:** Treat no output, or output that can't be converted to `[int]`, as an error in the comparing script, the way `HashBlock` treats a `$null` hash code.

**Fixed:** `ComparingBlock<T>.Compare` reads the script block's first output itself instead of going through `GetFirstValue`, which is unchanged. No output, a `$null` first output, or one that `LanguagePrimitives.ConvertTo<int>` can't convert throws the new `ComparingScriptException` (`src/engine/ListFunctions.Engine/Modern/Exceptions/ComparingScriptException.cs`). It's built like `EqualityScriptException`: its `Offender` is the first operand, and its `Variables` hold both. A script block that throws still throws its own exception.

- In New-SortedSet, `SortedSet.Add` throws the exception, so the element isn't added, and the command writes a non-terminating error. That error used to be the `TargetInvocationException` from calling `Add` through reflection, whose message was "Exception has been thrown by the target of an invocation." New-SortedSet now writes the exception inside it, so the message explains the failure. That applies to a script block that throws, too.
- Both repros return a set that holds only `5`, and write two errors.
- `ScriptBlockFilter` and `EqualityBlock` still use `GetFirstValue`, but they convert with `LanguagePrimitives.IsTrue`, which doesn't throw, so no user input reaches its `catch` anymore.

**Tests:** Both, and written. The fix changed `ComparingBlock<T>` rather than `GetFirstValue`, so there's no `Extensions/PSVariableCollectionExtensionsTests.cs`.

- Engine: `Category=Bug10` in `Modern/ComparingBlockTests.cs`, where `Compare` throws for both repros and for a `$null` output, and still converts output such as `'-1'`.
- Pester: `Bug10` in `tests/New-SortedSet.Tests.ps1`, where the same three outputs write `ComparingScriptException` errors. The `Bug14` test there still runs the first repro and checks only that the process survives.

### 11 — A generic type split at a comma silently becomes `[object]`

**Where:** `ArgumentToTypeTransformAttribute.Transform` (`src/engine/ListFunctions-Next/Validation/ArgumentToTypeTransformAttribute.cs`). PowerShell splits an unparenthesized type literal at the comma into an `object[]`, and the `default` case returns `typeof(object)` instead of failing.

```powershell
(New-List [System.Collections.Generic.KeyValuePair[string,int]]).GetType().FullName
# Expected: an error, or List[KeyValuePair[string, int]]. Actual: List[object]
```

**Fix idea:** Throw for unsupported input. Or join an `object[]` of strings back together with `,` and parse that, which recovers the type the user meant.

**Related, fixed:** A type name without brackets was rejected with a misleading message. `New-List System.String` failed with "'System.String' is not a valid .NET or custom-defined type", even though it is one. Parsed as a script, a bare type name is a command name, or a parse error when it contains a comma, so it holds no type literal. Where that parse would fail, the transform now puts the name in brackets and resolves the result only if it's a single type literal, so `string` resolves the same way as `[string]` in both editions. Input that worked before still takes the old path. PowerShell's `[type]` conversion isn't used, because in 5.1 it ignores the text after a type name and turns `'System.String bad text'` into `[string]`.

**Fixed:** By joining the parts, chosen on 2026-10-03. When every element of an `object[]` argument is a string, `ArgumentToTypeTransformAttribute.Transform` joins the elements with commas and resolves the result with the new `ResolveFromJoinedName`.

- `ResolveFromJoinedName` accepts only a single type: a type literal that spans the whole text, or a type name without brackets. Unlike a string argument, it doesn't settle for the first of several type literals. PowerShell passes `New-Dictionary [string],[int]` to `-KeyType` as the array `'[string]', '[int]'`, so that command fails now, instead of quietly making `[string]` the key type. A single string such as `'[string],[int]'` still resolves to `[string]`, as before.
- Any other argument that isn't a `Type`, a `ScriptBlock`, a string, or `$null` throws "Cannot convert a value of type '...' to a type", where it used to become `[object]`. That includes an array that holds anything but strings, and a number such as `5`. `$null` still gives `[object]`.
- PSReadLine still gets `[object]` instead of an error. The new `Reject` method holds that rule for every path, including the two that had it before.
- The repro returns a `List[KeyValuePair[string, int]]` in both editions. The README's Generic types section says that a type literal with a comma works as it is, and that `New-Dictionary [string],[int]` fails.

**Tests:** Pester only, and written: `Bug11` in the `GenericType` context of `tests/New-List.Tests.ps1`, and one in `tests/New-Dictionary.Tests.ps1`. The transform is in `ListFunctions-Next`, and every cmdlet that takes a type shares it.

- The related case's tests cover bare type names, the forms that already worked, and names that are still rejected.
- The fix's tests cover the repro, the same type literal with a space after the comma and without its outer brackets, and a literal with two commas. They also cover an array of strings that doesn't make up a single type, an array that holds a `Type`, the number `5`, and `$null`.
- The `New-Dictionary` test checks that `New-Dictionary [string],[int]` fails.

### 12 — ConvertTo-Dictionary misses `$_` when an operator follows it

**Where:** `ScriptBlockVariableExtensions.ReplaceWithArgsZero` (`src/engine/ListFunctions.Engine/Extensions/ScriptBlockVariableExtensions.cs`). Its regex rewrites `$_`, `$this`, and `$PSItem` to `$args[0]` only when whitespace, `)`, `"`, `;`, `.`, `,`, `'`, `#`, or the end of the text follows. After any other character, such as `*`, `+`, `-`, or `]`, the variable stays as it is and is unset when the selector runs. Every key comes back `$null`, so every object is skipped.

```powershell
(1..3 | ConvertTo-Dictionary -KeySelector { $_*10 }).Count     # Expected: 3. Actual: 0
(1..3 | ConvertTo-Dictionary -KeySelector { $_ * 10 }).Count   # 3
```

The regex also rewrites `$_` in places where `$args[0]` means something else. These selectors run without an error but return the wrong result:

```powershell
('x', 'y' | ConvertTo-Dictionary -KeySelector { "$_" }).Keys -join ','    # Expected: x,y. Actual: x[0],y[0]

$people = [pscustomobject]@{ Name = 'Ann'; Tags = 'a', 'b' }, [pscustomobject]@{ Name = 'Bob'; Tags = 'c' }
($people | ConvertTo-Dictionary Name -ValueSelector { @($_.Tags | Where-Object { $_ -ne 'b' }).Count })['Ann']    # Expected: 1. Actual: 2
```

- Inside a double-quoted string, `"$_"` becomes `"$args[0]"`. PowerShell expands `$args` but not the index, so `[0]` stays in the key as text.
- The rewrite also reaches nested script blocks. `Where-Object` passes no arguments to its filter, so `$args[0]` is `$null` there, and `$null -ne 'b'` keeps every tag.

**Fix idea:** Rewrite from the AST, using the `VariableExpressionAst` extents, instead of a regex. Or run the selectors with `InvokeWithContext` and set `$_`, the way the other commands do.

**Fixed:** With `InvokeWithContext`, chosen on 2026-10-03. ConvertTo-Dictionary no longer rewrites its selectors. The new `ConvertToDictionaryCmdlet.Select` runs a selector with `$_`, `$this`, and `$PSItem` set through a `PSThisVariable`, and with the input object as `$args[0]`, the way the other cmdlets run their script blocks. `AddToDictionary` and `InferTypes`, which infers the key and value types from the first input object, both call it. `ScriptBlockVariableExtensions`, which held `ReplaceWithArgsZero`, had no other callers and is gone. All three repros give their expected results in both editions.

- A `$_` that the regex missed wasn't always unset. Inside a script block that sets its own `$_`, such as a `ForEach-Object` script block or a Pester test, the selector read that outer `$_`. That's how most of the `Bug12` tests failed before the fix.
- Errors that a selector writes now reach the error stream of the pipeline that runs ConvertTo-Dictionary, as errors from the other cmdlets' script blocks do: `2>$null` hides them, and `2>&1` captures them. `ScriptBlock.Invoke` wrote them straight to the host. Before and after the fix, the cmdlet's `-ErrorVariable` doesn't collect them, its `-ErrorAction Stop` doesn't stop on them, and `$ErrorActionPreference = 'Stop'` does.

**Tests:** Pester only, and written: `Bug12` in `tests/ConvertTo-Dictionary.Tests.ps1`. The fix changed only the cmdlet, so there's no `Extensions/ScriptBlockVariableExtensionsTests.cs`. The tests cover all three repros, and:

- `$_`, `$PSItem`, `$this`, and `$args[0]` followed by an operator in `-KeySelector`.
- `$_` followed by an operator in `-ValueSelector`, and in a script block passed to `-ValuePropertyName`.
- The key type inferred from `-KeySelector`, for piped input and for `-InputObject`.

### 13 — Assert-AllObject gives different answers for empty input

**Where:** `ScriptBlockFilter.All` returns `$false` for an empty collection, so `-InputObject @()` counts as a failure. With an empty pipeline, `ProcessRecord` never runs, so the result is `$true`.

```powershell
@() | Assert-AllObject { $_ -is [int] }                          # True
Assert-AllObject -InputObject @() -Condition { $_ -is [int] }    # False
```

**Fix idea:** Pick one answer and return it on both paths. LINQ's `All` returns `true` for an empty sequence.

**Fixed:** With `$true`, chosen on 2026-10-03. That's the answer of `List[T].TrueForAll`, which the README names as a model, and of LINQ's `All`. `ScriptBlockFilter.All` returns `true` for an empty collection and for `null`, without running the script block. `-InputObject @()` and `-InputObject $null` return `$true` now, as an empty pipeline already did. A piped `$null` is still one element, so `$null | Assert-AllObject { $_ -is [int] }` returns `$false`. The README's Assert-AllObject section says that no elements gives `$true`.

**Tests:** Both, and written.

- Engine: `Category=Bug13` in `Modern/ScriptBlockFilterTests.cs`, where `All` returns `true` for an empty collection and for `null` without running a script block that throws.
- Pester: `Bug13` in `tests/Assert-AllObject.Tests.ps1`, where an empty pipeline, `-InputObject @()`, and `-InputObject $null` all return `$true`, and a piped `$null` is still tested as an element.

### 14 — `Debug.Fail` ends the PowerShell process in Debug builds

**Where:** In .NET, a failed `Debug.Fail` with no debugger attached ends the process, and the PowerShell session with it. Two of the calls are on paths that ordinary user input reaches:

- `GenericCollectionCtor.CallActivator`: 09's repro ends pwsh in a Debug build (seen while testing).
- `PSVariableCollectionExtensions.GetFirstValue`: reached when a script block's output can't be converted, as in 10.

The others guard cleanup and reflection fallbacks: `ListFunctionCmdletBase.CleanupCore`, and two in `ScriptBlockInvocationException`.

**Fix idea:** Don't call `Debug.Fail` on paths that user input can reach. Use `Debug.WriteLine`, or nothing.

**Fixed:** All five calls now use `Debug.WriteLine`, including the three that guard cleanup and reflection fallbacks. A `Debug.Fail` on any path that a cmdlet runs would end a whole test run instead of failing one test. The `Bug14` tests in `tests/New-Dictionary.Tests.ps1` and `tests/New-SortedSet.Tests.ps1` cover the two calls that user input reaches.

**Tests:** Pester only, and written (see **Fixed:**). Both calls that user input reaches are in Engine, but the fix only swapped `Debug.Fail` for `Debug.WriteLine`, and the Pester tests already reach both calls in both builds.

**Since 09 and 10 were fixed:** 09's repro creates its dictionary now, so the `Bug14` test in `tests/New-Dictionary.Tests.ps1` passes a `-Capacity` of `[int]::MaxValue` instead. No array can hold that many buckets, so the constructor still fails in `CallActivator`. No user input reaches `GetFirstValue`'s `catch` anymore (see 10), so the `Bug14` test in `tests/New-SortedSet.Tests.ps1` checks only that its repro returns.

## Release

### 15 — Update the manifest and the shipped DLLs for 4.0.0

- [ ] `ListFunctions/Core/` and `ListFunctions/Desk/` still hold the v3.1.0 build (commit `b459e4c`), and its Core DLLs target .NET 9. Copy in the 4.0.0 Release outputs: `Core/` from `src/engine/ListFunctions-Next/bin/Release/net10.0/`, and `Desk/` from `src/engine/ListFunctions-NETFramework/bin/Release/net48/`.
- [ ] The 4.0.0 `net48` Release output also contains `ZLinq.dll`, `System.Memory.dll`, `System.Collections.Immutable.dll`, `Microsoft.Bcl.Memory.dll`, `System.Buffers.dll`, `System.Numerics.Vectors.dll`, and `System.Runtime.CompilerServices.Unsafe.dll`. `Desk/` doesn't ship them, and `FileList` doesn't list them.
- [ ] `DotNetFrameworkVersion = '4.7.1'` in `ListFunctions/ListFunctions.psd1` doesn't match the `net48` target.
- [ ] Nothing enforces PowerShell 7.6 or later, which the README states. `ListFunctions/ListFunctions.psm1` imports `Core\ListFunctions.Next.dll` on any 7.x, but a `net10.0` assembly can't load on PowerShell 7.5 (.NET 9) or earlier.
- [ ] `Tags` includes `Remove` and `Modify`, but `Remove-All` and `Remove-At` exist only in the legacy scripts under `src/public`.
- [ ] `ReleaseNotes` is still the 3.x text.

**Tests:** None. This is a release checklist, and the Pester tests import the build output, not the DLLs shipped under `ListFunctions/`. `tests/Module.Tests.ps1` already checks the build's exports against the manifest's `CmdletsToExport` and `AliasesToExport`.

## Minor

### 16 — `-ScriptErrorAction` exists on only two of the four condition cmdlets

Assert-AnyObject and Find-IndexOf give `-ScriptBlockErrorAction` the alias `ScriptErrorAction`, but Assert-AllObject and Find-LastIndexOf don't. The README doesn't mention the alias.

```powershell
1 | Find-IndexOf { $_ -eq 1 } -ScriptErrorAction Stop        # 0
1 | Find-LastIndexOf { $_ -eq 1 } -ScriptErrorAction Stop    # Error: A parameter cannot be found that matches parameter name 'ScriptErrorAction'.
1 | Assert-AllObject { $_ -eq 1 } -ScriptErrorAction Stop    # Same error
```

**Fix idea:** Add the alias to the other two, or remove it from both.

**Fixed:** By adding the alias to the other two, chosen on 2026-10-03. Assert-AllObject's and Find-LastIndexOf's `-ScriptBlockErrorAction` have the alias `ScriptErrorAction` too, so all four condition cmdlets accept it. Nothing that worked before breaks. The README's `-ScriptBlockErrorAction` rows for Assert-AnyObject, Assert-AllObject, and Find-IndexOf list the alias, and Find-LastIndexOf takes the same parameters as Find-IndexOf. New-HashSet, New-SortedSet, and New-Dictionary have a `-ScriptBlockErrorAction` without the alias, and keep it that way.

**Tests:** Pester only, and written: `Bug16` in `tests/Assert-AnyObject.Tests.ps1`, `tests/Assert-AllObject.Tests.ps1`, `tests/Find-IndexOf.Tests.ps1`, and `tests/Find-LastIndexOf.Tests.ps1`. Each test passes `-ScriptErrorAction Stop` with a condition that writes an error, and checks that the error becomes a terminating error, which shows that the alias reached `-ScriptBlockErrorAction`. The tests for Assert-AllObject and Find-LastIndexOf failed with the repro's binding error before the fix.

### 17 — New-SortedSet silently skips elements it can't convert

**Where:** `NewSortedSetCmdlet.ProcessCore` skips an element when `LanguagePrimitives.TryConvertTo` fails. New-List and New-HashSet write a non-terminating error in the same situation.

```powershell
(1, 'abc', 2 | New-SortedSet [int]) -join ','    # 1,2, with no error
(1, 'abc', 2 | New-HashSet [int]).Count          # 2, with a conversion error for 'abc'
```

**Fix idea:** Write the same conversion error that New-List writes.

**Fixed:** `NewSortedSetCmdlet.ProcessCore` converts each element with `ListFunctionCmdletBase.TryConvertItem`, as New-Dictionary does. An element that can't be converted writes the `LFInvalidCastException` error that New-List writes, through the same `WriteConversionError`, and is skipped. The other elements are still added. A `$null` element is still skipped without an error. The repro returns `1,2` and writes an error for `'abc'`. `TryConvertItem` reports a conversion to `$null` as a success, as `[NullString]::Value` gives for `[string]`, so the array that passes each element to `Add` is now an `object?[]`. The README's New-SortedSet section says that an element that can't be converted writes a non-terminating error.

**Tests:** Pester only, and written: `Bug17` in `tests/New-SortedSet.Tests.ps1`, for piped input and `-InputObject`, which check the error's exception type and target object, and for a piped `$null`, which still writes no error. The `Bug06` test there, whose piped array can't be converted to `[int]`, now passes `-ErrorAction SilentlyContinue`, because the array writes a conversion error.

### 18 — New-Dictionary's `-CaseSensitive` can't be combined with `-InputObject`

**Where:** The dynamic `-CaseSensitive` belongs only to the `StringDict` parameter set, and `InputObject` belongs only to `JustCopy` and `WithCustomEqualityAndCopy`.

```powershell
@{ a = 1 } | New-Dictionary [string] -CaseSensitive
# Error: The input object cannot be bound to any parameters for the command...
# Output: an empty Dictionary[string, object]
```

**Fix idea:** Also add `-CaseSensitive` to the `JustCopy` set.

**Fixed:** `-CaseSensitive` belongs to `JustCopy` too, as an optional parameter. It stays mandatory in `StringDict`, where it's the only thing that tells that set apart from the default set. In `JustCopy`, the mandatory `-InputObject` already does that, so the switch can be optional there. `EqualityConstructingCmdlet` has a new virtual `CaseSensitiveOptionalParameterSetName`, which New-Dictionary overrides with `JustCopy`, and the dynamic parameter gets a second `ParameterAttribute` for that set. Without input, `New-Dictionary [string] -CaseSensitive` still resolves to `StringDict` and creates an empty dictionary, the way `New-Dictionary -EqualityScript ... -HashCodeScript ...` resolves to `WithCustomEquality` instead of `WithCustomEqualityAndCopy`. The repro returns a `Dictionary[string, object]` with case-sensitive keys that holds `a = 1`.

**Tests:** Pester only, and written: `Bug18` in `tests/New-Dictionary.Tests.ps1`, for `[string]` and `[object]` keys, each with piped input and with `-InputObject`, for `-CaseSensitive` with `-CloneValues`, and for `-CaseSensitive` without input, which already worked.

### 19 — ConvertTo-Dictionary stores the whole input object when the value is `$null`

**Where:** `ConvertToDictionaryCmdlet.AddToDictionary` uses the input object itself as the value whenever the value selector returns `$null`. With a typed value, adding it then fails.

```powershell
$items = [pscustomobject]@{ K = 'a'; V = 'x' }, [pscustomobject]@{ K = 'b'; V = $null }
$d = $items | ConvertTo-Dictionary K V
# Error: The value "@{K=b; V=}" is not of type "System.String" and cannot be used in this generic collection.
$d.Count     # Expected: 2, with $d['b'] -eq $null. Actual: 1
```

**Fix idea:** Store `$null` when a value selector returns `$null`, and fall back to the input object only when there's no value selector.

**Fixed:** `ConvertToDictionaryCmdlet.AddToDictionary` uses the input object as the value only when there's no value selector. When `-ValuePropertyName` or `-ValueSelector` gives one, its output is converted to the value type with `LanguagePrimitives.ConvertTo`, even when it's `$null`. That follows the rule decided under 04: a `$null` value becomes what `Add($key, $null)` stores in a dictionary with the same value type. Measured on 2026-10-03 in both editions, that's `''` for `[string]`, `0` for `[int]`, and `$null` for `[object]` and `[version]`.

- The repro returns a dictionary with 2 entries. Its value type is `[string]`, so `$d['b']` is `''`, not the `$null` that the repro expects.
- When the first object's value is `$null`, the value type is `[object]`, so the dictionary stores `$null`.
- Without a value selector, each object is its own value and isn't converted, as before.
- The README's ConvertTo-Dictionary section and the XML docs of `-ValueSelector` and `-ValuePropertyName` describe the conversion.

**Tests:** Pester only, and written: `Bug19` in `tests/ConvertTo-Dictionary.Tests.ps1`, for a `$null` value property of a later object with `[string]`, `[int]`, and `[version]` values, a `$null` value property of the first object, and a `-ValueSelector` that outputs nothing. One more test, which passed before the fix, checks that each object is its own value when there's no value selector.

### 20 — New-Dictionary's `[object]` keys turn case-sensitive when `-ValueType` isn't `[object]`

**Where:** New-Dictionary builds its case-insensitive `Hashtable` only when both types are `[object]` (`DictionaryCtor.ShouldConstructDefault`). With any other value type, `[object]` keys get `EqualityComparer<object>.Default`, which compares strings case-sensitively, and `-CaseSensitive` is offered but changes nothing.

```powershell
$d = New-Dictionary -ValueType ([int])     # Dictionary[object, int]
$d['a'] = 1
$d['A'] = 2
$d.Count                                   # 2; the default Hashtable gives 1
```

**Fix idea:** Decide how `[object]` keys should compare, and use that rule on both paths. New-HashSet's `ObjectEqualityComparer` is one option.

**Fixed:** With the rule of the `Hashtable` that New-Dictionary already creates, chosen on 2026-10-03. Without a comparer, `[object]` keys compare the same way whatever the value type is: strings with `StringComparer.OrdinalIgnoreCase`, or with `StringComparer.CurrentCulture` under `-CaseSensitive`, and other keys with their own `Equals` method, so `1` and `'1'` stay different keys. The default `Hashtable` doesn't change.

- `EqualityCollectionCtor` has a new virtual `GetDefaultComparer`, which `GetComparerOrDefault` calls when no comparer was passed to the constructor. Its base implementation returns the invariant-culture comparers for `string` that `GetComparerOrDefault` used to choose itself, and `null` for other types, which still get `EqualityComparer<T>.Default`.
- `DictionaryCtor` overrides it for `object` keys with the comparer that its `Hashtable` uses. A `StringComparer` isn't an `IEqualityComparer<object>`, so the `Dictionary[object, TValue]` gets it inside an `EqualityComparerAdapter<object>`.
- The repro's dictionary holds one entry, `a = 2`. The README and New-Dictionary's XML docs describe the rule.

**Tests:** Both, and written.

- Engine: `Category=Bug20` in `Modern/Constructors/DictionaryCtorTests.cs`, for `object` and `int` values: string keys compare without regard to case, `IsCaseSensitive` makes them compare with case, and `1` and `"1"` stay different keys. Only the case-insensitive test with `int` values failed before the fix. The others pin down the rule.
- Pester: `Bug20` in `tests/New-Dictionary.Tests.ps1`, for `[object]` and `[int]` values, with the repro, `-CaseSensitive`, and `1` and `'1'` as keys.

### 21 — Find-LastIndexOf handles condition errors differently from the other condition cmdlets

**Where:** `ListFunctionCmdletBase` (`src/engine/ListFunctions-Next/Cmdlets/ListFunctionCmdletBase.cs`). `BeginProcessing` and `ProcessRecord` catch exceptions and rethrow them with `ThrowTerminatingError(e.ToRecord(...))`. `EndProcessing` runs `EndCore` in a `try`/`finally` with no `catch`, so exceptions from `EndCore` reach PowerShell unchanged. Find-LastIndexOf is the only cmdlet that runs a script block in `EndCore`. Its condition errors take the second path, and those of Assert-AnyObject, Assert-AllObject, and Find-IndexOf take the first. The two paths differ in two ways:

- **What the error stops.** With `-ScriptBlockErrorAction Stop`, an error that the condition writes stops the whole script when it comes from Find-LastIndexOf, the way `-ErrorAction Stop` does for an ordinary command. From the other three cmdlets, it stops only the current statement, because `ThrowTerminatingError` wraps the `ActionPreferenceStopException` in an error that ends just the statement. A `throw` or a failed method call stops only the statement from all four.
- **The error record.** From Find-LastIndexOf, a `throw` or a `Stop` error keeps its original record, whose error ID and category don't mention the cmdlet. PowerShell wraps other errors, such as a failed method call, in a record that keeps their error ID and category and adds the cmdlet. The other three cmdlets always name themselves, but `ProcessRecord` rebuilds the record with `ToRecord`, which drops the original error ID and category in favor of the exception's type name and `NotSpecified`.

```powershell
1 | Find-IndexOf { if ($_) { Write-Error 'oops' } } -ScriptBlockErrorAction Stop; 'still running'
# An error from Find-IndexOf, then: still running
1 | Find-LastIndexOf { if ($_) { Write-Error 'oops' } } -ScriptBlockErrorAction Stop; 'still running'
# An error from Write-Error, and nothing else

try { 1 | Find-IndexOf { if ($_) { throw 'boom' } } } catch { $_.FullyQualifiedErrorId; "$($_.CategoryInfo)" }
# System.Management.Automation.RuntimeException,ListFunctions.Cmdlets.Finds.FindIndexCmdlet
# NotSpecified: (:) [Find-IndexOf], RuntimeException
try { 1 | Find-LastIndexOf { if ($_) { throw 'boom' } } } catch { $_.FullyQualifiedErrorId; "$($_.CategoryInfo)" }
# boom
# OperationStopped: (boom:String) [], RuntimeException

try { 1 | Find-IndexOf { if ($_) { $null.Foo() } } } catch { $_.FullyQualifiedErrorId }
# System.Management.Automation.RuntimeException,ListFunctions.Cmdlets.Finds.FindIndexCmdlet
try { 1 | Find-LastIndexOf { if ($_) { $null.Foo() } } } catch { $_.FullyQualifiedErrorId }
# InvokeMethodOnNull,ListFunctions.Cmdlets.Finds.FindLastIndexCmdlet
```

**Fix idea:** Handle exceptions the same way in all three phases. `StopUpstreamCommands` runs `EndCore` too, outside `ProcessRecord`'s `try`, so it needs the same handling. First decide what `Stop` should do: letting the `ActionPreferenceStopException` through matches `-ErrorAction Stop`. Wrap other errors in a record that names the cmdlet and keeps the original error ID and category, the way PowerShell's own wrapping does for Find-LastIndexOf's `$null.Foo()`.

**Correction, found on 2026-10-03:** The description is wrong about `throw`, and leaves out `break`.

- Under the default `-ScriptBlockErrorAction`, `SilentlyContinue`, a `throw` in a condition is suppressed, and no error appears from any of the four cmdlets.
- With `Stop` or `Continue`, a `throw` from Find-LastIndexOf ended the whole script, as one from `ForEach-Object` does. The other three ended only the statement.
- `break` in a condition left the enclosing loop from Find-LastIndexOf and `ForEach-Object`. The other three turned it into a `BreakException` error, and the loop went on.

**Fixed:** Like `ForEach-Object`, chosen on 2026-10-03. Find-LastIndexOf already behaved exactly like a `ForEach-Object` script block that sets `$ErrorActionPreference` in a child scope, measured in both editions, so the other three now behave the same way.

- `ListFunctionCmdletBase.BeginProcessing` and `ProcessRecord` still record the failure and run `Cleanup`. Then they pass a `RuntimeException` or a `FlowControlException` to PowerShell unchanged, through the new `PassesThrough`. Any other exception, such as Assert-AllObject's `ArgumentException` for a `$null` condition, still becomes a terminating error through `ToRecord`. `EndProcessing` and `StopUpstreamCommands` already passed every exception on unchanged, and PowerShell gives any other exception the same error ID and category that `ToRecord` does.
- With `-ScriptBlockErrorAction Stop`, an error that the condition writes ends the whole script, as `-ErrorAction Stop` does, and keeps its original record.
- A `throw` ends the whole script, unless the preference is `SilentlyContinue`, and keeps its own record.
- A failed method call ends only the statement. PowerShell wraps its error in a record that keeps its error ID and category, such as `InvokeMethodOnNull` and `InvalidOperation`, and names the cmdlet.
- `break` leaves the loop around the cmdlet.
- `ThrowTerminatingError` inside `ProcessCore` throws a `PipelineStoppedException`, which is a `RuntimeException`, so the base class no longer reports that error a second time.
- The README's Errors in script blocks section describes the behavior.

ConvertTo-Dictionary's `-KeySelector` and `-ValueSelector` follow the same rule, decided on 2026-10-03 after this fix. `AddToDictionary` lets a selector's error reach PowerShell unchanged through `PassesThrough`, which is now `protected`. An error for the first input object, whose selectors run in `InferTypes`, reaches the base class the same way. Before, ConvertTo-Dictionary reported every selector error as its own terminating error, which ended only the statement.

**Tests:** Pester only, and written.

- `Bug21` in `tests/Find-LastIndexOf.Tests.ps1`, `tests/Find-IndexOf.Tests.ps1`, `tests/Assert-AnyObject.Tests.ps1`, and `tests/Assert-AllObject.Tests.ps1`: an error written under `Stop` and a `throw` under `Continue` end the script, a failed method call under `Stop` ends only the statement and keeps its error ID and category, and `break` leaves the enclosing loop. The Find-LastIndexOf tests passed before the fix and keep all four cmdlets in agreement.
- The tests run their scripts through the new `tests/Invoke-InNewRunspace.ps1`. Pester runs each test inside a `try` block, where both kinds of error jump to the `catch` block.
- Untagged tests in `tests/ConvertTo-Dictionary.Tests.ps1` cover the selectors: a `throw` in `-KeySelector` or `-ValueSelector`, for the first or the second input object, ends the script, a failed method call under `Stop` ends only the statement, and `break` leaves the enclosing loop.

### 22 — ConvertTo-Dictionary fails on a property name that contains a single quote

Found on 2026-10-03 while fixing 12.

**Where:** `ConvertToDictionaryCmdlet.BeginCore` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs`) turns `-KeyPropertyName`, and a string passed to `-ValuePropertyName`, into a selector by pasting the name into the text `$args[0].'<name>'` and passing that text to `ScriptBlock.Create`. A `'` in the name ends the single-quoted string early, so the text doesn't parse, and the command ends with a terminating error. PowerShell treats the typographic single quotes, such as U+2019 (right single quotation mark), as apostrophes in a single-quoted string, so they fail the same way.

```powershell
[pscustomobject]@{ "it's" = 'a' } | ConvertTo-Dictionary -KeyPropertyName "it's"
# Error: ... The string is missing the terminator: '.
```

**Fix idea:** Escape the name with `CodeGeneration.EscapeSingleQuotedStringContent` before pasting it in. It doubles every kind of single quote.

**Fixed:** The new `ConvertToDictionaryCmdlet.CreatePropertySelector` escapes the name with `CodeGeneration.EscapeSingleQuotedStringContent` before it pastes the name in, for `-KeyPropertyName` and for a string passed to `-ValuePropertyName`. The repro returns a dictionary with the key `a` in both editions.

**Tests:** Pester only, and written: `Bug22` in `tests/ConvertTo-Dictionary.Tests.ps1`, for a key property and a value property whose names contain an apostrophe or U+2019. The cause was in the cmdlet.
