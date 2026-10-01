# ListFunctions bug list

Found while rewriting `README.md` on 2026-09-28. The README describes how the module is meant to work, so each item under **README accuracy** makes a README statement false until it's fixed.

## Checklist

**README accuracy**

- [x] 01 — `-HashCodeScript`'s return value is ignored
- [x] 02 — `-Capacity` does nothing on New-HashSet and New-Dictionary
- [x] 03 — New-HashSet can't combine `-GenericType` with `-CaseSensitive`
- [x] 04 — New-List ignores `-IncludeNullElements` unless `-GenericType` is given

**Other bugs**

- [x] 05 — `$args[0]` and `$args[1]` pass validation but are always `$null`
- [ ] 06 — Piped `$null` and array elements are miscounted
- [ ] 07 — New-Dictionary ignores the equality scripts when it copies from `-InputObject`
- [ ] 08 — New-Dictionary doesn't convert copied values to `-ValueType`
- [ ] 09 — New-Dictionary can't use script block equality with value-type keys
- [ ] 10 — A `-ComparingScript` result that isn't an `[int]` silently means "equal"
- [ ] 11 — A generic type split at a comma silently becomes `[object]`
- [ ] 12 — ConvertTo-Dictionary misses `$_` when an operator follows it
- [ ] 13 — Assert-AllObject gives different answers for empty input
- [x] 14 — `Debug.Fail` ends the PowerShell process in Debug builds

**Release**

- [ ] 15 — Update the manifest and the shipped DLLs for 4.0.0

**Minor**

- [ ] 16 — `-ScriptErrorAction` exists on only two of the four condition cmdlets
- [ ] 17 — New-SortedSet silently skips elements it can't convert
- [ ] 18 — New-Dictionary's `-CaseSensitive` can't be combined with `-InputObject`
- [ ] 19 — ConvertTo-Dictionary stores the whole input object when the value is `$null`
- [ ] 20 — New-Dictionary's `[object]` keys turn case-sensitive when `-ValueType` isn't `[object]`
- [ ] 21 — Find-LastIndexOf handles condition errors differently from the other condition cmdlets

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

**Tests:** Both. Pester is written: `Bug01` in `tests/New-HashSet.Tests.ps1` and `tests/New-Dictionary.Tests.ps1`. On the Engine side, `Modern/HashBlockTests.cs` covers the fixed `HashBlock` behavior, but without a `Bug01` trait.

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

**Fixed:** `EqualityCollectionCtor` has a `Capacity` property, which `EqualityConstructingCmdlet.BeginCore` sets from `-Capacity`. `GetConstructorArguments` yields the capacity before the comparer, so reflection now calls the `(int, IEqualityComparer<T>)` constructors. The default paths call `Hashtable(int, IEqualityComparer)` and `HashSet<object>(int, IEqualityComparer<object>)`, which the `netstandard2.0` build reaches through `Activator.CreateInstance`. A capacity of 0 creates the same collections as before. The `Bug02` tests measure the bucket array with `tests/Get-BucketCount.ps1`, because .NET Framework has no `EnsureCapacity`.

**Tests:** Both. Pester is written: `Bug02` in `tests/New-HashSet.Tests.ps1` and `tests/New-Dictionary.Tests.ps1`. The Engine tests aren't written yet. They go in `Modern/Constructors/HashSetCtorTests.cs` and a new `Modern/Constructors/DictionaryCtorTests.cs`, and they set `Capacity` before calling `Construct`.

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

**Fixed:** `InvokeWithContext<T>` and the two `TryInvokeWithContext` overloads in `src/engine/ListFunctions.Engine/Internal/ScriptBlockExtensions.cs` take an `args` array and pass it to `ScriptBlock.InvokeWithContext` in place of the shared empty array. `ScriptBlockFilter.IsTrue` and `HashBlock.GetHashCode` pass the element as `$args[0]`, and `EqualityBlock.Equals` and `ComparingBlock<T>.Compare` pass their operands as `$args[0]` and `$args[1]`, the same values that `$x` and `$y` hold. Each call builds a new array, because PowerShell doesn't copy it: a script block without a `param()` block gets that exact array as `$args`, so a shared array would change under a script block that keeps `$args`. A script block with a `param()` block now gets the elements as its parameters, the way `ScriptBlock.Invoke` does, and `$args` holds only the elements left over. The validation attributes and the cmdlets are unchanged. All four repros give their expected results in both editions, with no errors. The `Bug05` tests cover `$args` in every parameter that **Where:** lists, the order of `$args[0]` and `$args[1]`, and a `$null` or array element, which arrives as a single argument. Each of them fails when `ScriptBlockExtensions.cs` passes an empty array again.

**Tests:** Both, and written. Pester: `Bug05` in `tests/Assert-AnyObject.Tests.ps1`, `tests/Assert-AllObject.Tests.ps1`, `tests/Find-IndexOf.Tests.ps1`, and `tests/Find-LastIndexOf.Tests.ps1` (all new), and in `tests/New-HashSet.Tests.ps1` and `tests/New-SortedSet.Tests.ps1`. Engine: `Category=Bug05` in `Modern/ScriptBlockFilterTests.cs`, `Modern/EqualityBlockTests.cs`, `Modern/HashBlockTests.cs`, and `Modern/ComparingBlockTests.cs`.

**Open question:** New-Dictionary's `-EqualityScript` and `-HashCodeScript` run through the same `EqualityBlock` and `HashBlock`, so `$args[0]` and `$args[1]` hold the keys there now too. Its `ValidateScriptVariable` attributes don't list `FirstArg` and `SecondArg`, though, so New-Dictionary still rejects script blocks that New-HashSet accepts, such as `-EqualityScript { $args[0] -eq $args[1] }`. Should it accept them? And should the README, which doesn't mention `$args` for any parameter, list it?

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

**Tests:** Pester only, because the cause is in how the cmdlets bind pipeline input. The repros go in `tests/Find-IndexOf.Tests.ps1` and `tests/Find-LastIndexOf.Tests.ps1` (both new) and `tests/New-List.Tests.ps1`. The New-List tests add the piped `$null` case that 04's tests leave out.

### 07 — New-Dictionary ignores the equality scripts when it copies from `-InputObject`

**Where:** `NewDictionaryCmdlet.GetCustomEqualityComparer` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewDictionaryCmdlet.cs`) only recognizes the `WithCustomEquality` parameter set. When you also pass `-InputObject`, the set is `WithCustomEqualityAndCopy`, so the method falls back to the default comparer and the command returns a plain `Hashtable`. `-ScriptBlockErrorAction` isn't in `WithCustomEqualityAndCopy` either.

```powershell
$d = @{ a = 1 } | New-Dictionary -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() }
$d.GetType().Name    # Expected: Dictionary`2, with an EqualityBlock comparer. Actual: Hashtable

New-Dictionary -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() } -InputObject @{ a = 1 } -ScriptBlockErrorAction Stop
# Error: Parameter set cannot be resolved using the specified named parameters.
```

**Fix idea:** Recognize both set names, and add `ScriptBlockErrorAction` to `WithCustomEqualityAndCopy`.

**Tests:** Pester only, in `tests/New-Dictionary.Tests.ps1`. The cause is in the cmdlet's parameter sets.

### 08 — New-Dictionary doesn't convert copied values to `-ValueType`

**Where:** `NewDictionaryCmdlet.Process` converts each key with `LanguagePrimitives.ConvertTo(de.Key, this.KeyType)`, but only clones each value, so the `Add` call rejects a value of the wrong type.

```powershell
$d = @{ a = '1' } | New-Dictionary [string] [int]
# Error: Object of type 'System.String' cannot be converted to type 'System.Int32'.
$d.Count             # Expected: 1. Actual: 0
```

**Fix idea:** Convert each value to `ValueType` after cloning it, and report conversion failures the way New-List does.

**Tests:** Pester only, in `tests/New-Dictionary.Tests.ps1`. The cause is in `NewDictionaryCmdlet.Process`.

### 09 — New-Dictionary can't use script block equality with value-type keys

**Where:** `EqualityBlock` implements `IEqualityComparer<object>`. `Dictionary<string, TValue>` accepts it through contravariance, but `Dictionary<int, TValue>` needs an `IEqualityComparer<int>`, so `GenericCollectionCtor.CallActivator` finds no matching constructor.

```powershell
New-Dictionary [int] -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() }
# Error: An exception occurred attempting to construct an object of type "System.Collections.Generic.Dictionary`2[[System.Int32, ...],[System.Object, ...]]".
```

**Fix idea:** Wrap the `EqualityBlock` in a generic `IEqualityComparer<TKey>` adapter, the way `ComparingBlock<T>` works for comparers. Or reject value-type keys during parameter binding, with a clear message.

**Tests:** Both with the adapter fix. Rejecting value-type keys changes only the cmdlet, so that fix needs Pester only.

- Pester: `tests/New-Dictionary.Tests.ps1`. The `Bug14` test there runs this repro but checks only that the process survives. The `Bug09` test checks the dictionary that comes back, or the error message.
- Engine, with the adapter fix: a new `Modern/Constructors/DictionaryCtorTests.cs`, where a `DictionaryCtor` with an `EqualityBlock` and an `[int]` key type creates a working dictionary. If the adapter is its own type, it gets its own test class too.

### 10 — A `-ComparingScript` result that isn't an `[int]` silently means "equal"

**Where:** `ComparingBlock<T>.Compare` (`src/engine/ListFunctions.Engine/Modern/ComparingBlock.cs`) converts the script's first output with `LanguagePrimitives.ConvertTo<int>` through `PSVariableCollectionExtensions.GetFirstValue`, which catches a failed conversion and returns `0`. No output also gives `0`. Every pair then compares as equal, so the set keeps only its first element, and no error appears.

```powershell
(5, 3, 1 | New-SortedSet [int] -ComparingScript { 'x' + $x + $y }) -join ','     # Expected: an error. Actual: 5
(5, 3, 1 | New-SortedSet [int] -ComparingScript { $null = $x, $y }) -join ','    # Expected: an error. Actual: 5
```

**Fix idea:** Treat no output, or output that can't be converted to `[int]`, as an error in the comparing script, the way `HashBlock` treats a `$null` hash code.

**Tests:** Both.

- Engine: `Modern/ComparingBlockTests.cs`, where `Compare` throws for both repros. `GetFirstValue` is shared: `ScriptBlockFilter` and `EqualityBlock` reach it through `InvokeWithContext`. If the fix changes `GetFirstValue` rather than `ComparingBlock<T>`, it also gets a new `Extensions/PSVariableCollectionExtensionsTests.cs`, and the existing `ScriptBlockFilterTests.cs` and `EqualityBlockTests.cs` show whether those types changed too.
- Pester: `tests/New-SortedSet.Tests.ps1`, where both repros produce an error. The `Bug14` test there runs the first repro but checks only that the process survives.

### 11 — A generic type split at a comma silently becomes `[object]`

**Where:** `ArgumentToTypeTransformAttribute.Transform` (`src/engine/ListFunctions-Next/Validation/ArgumentToTypeNameTransformAttribute.cs`). PowerShell splits an unparenthesized type literal at the comma into an `object[]`, and the `default` case returns `typeof(object)` instead of failing.

```powershell
(New-List [System.Collections.Generic.KeyValuePair[string,int]]).GetType().FullName
# Expected: an error, or List[KeyValuePair[string, int]]. Actual: List[object]
```

**Fix idea:** Throw for unsupported input. Or join an `object[]` of strings back together with `,` and parse that, which recovers the type the user meant.

**Related:** A type name without brackets is rejected with a misleading message. `New-List System.String` fails with "'System.String' is not a valid .NET or custom-defined type", even though it is one; the transform only accepts bracketed type literals.

**Tests:** Pester only, in `tests/New-List.Tests.ps1`, which is the cmdlet the repro uses. That file also gets the related case. The transform is in `ListFunctions-Next`, and every cmdlet that takes a type shares it.

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

**Tests:** Both with the AST fix. The `InvokeWithContext` fix changes only the cmdlet, which then stops calling `ReplaceWithArgsZero`, so it needs Pester only.

- Pester: a new `tests/ConvertTo-Dictionary.Tests.ps1`, with all three repros.
- Engine, with the AST fix: a new `Extensions/ScriptBlockVariableExtensionsTests.cs`. It covers `ReplaceWithArgsZero` directly: an operator right after `$_`, `$_` inside a double-quoted string, and `$_` in a nested script block.

### 13 — Assert-AllObject gives different answers for empty input

**Where:** `ScriptBlockFilter.All` returns `$false` for an empty collection, so `-InputObject @()` counts as a failure. With an empty pipeline, `ProcessRecord` never runs, so the result is `$true`.

```powershell
@() | Assert-AllObject { $_ -is [int] }                          # True
Assert-AllObject -InputObject @() -Condition { $_ -is [int] }    # False
```

**Fix idea:** Pick one answer and return it on both paths. LINQ's `All` returns `true` for an empty sequence.

**Tests:** Both if the answer is `$true`, because that changes `ScriptBlockFilter.All`. If the answer is `$false`, only the cmdlet's pipeline path changes, so Pester alone covers it.

- Pester: a new `tests/Assert-AllObject.Tests.ps1`, where an empty pipeline and `-InputObject @()` give the same answer.
- Engine, if the answer is `$true`: `Modern/ScriptBlockFilterTests.cs`, where `All` returns `true` for an empty collection.

### 14 — `Debug.Fail` ends the PowerShell process in Debug builds

**Where:** In .NET, a failed `Debug.Fail` with no debugger attached ends the process, and the PowerShell session with it. Two of the calls are on paths that ordinary user input reaches:

- `GenericCollectionCtor.CallActivator`: 09's repro ends pwsh in a Debug build (seen while testing).
- `PSVariableCollectionExtensions.GetFirstValue`: reached when a script block's output can't be converted, as in 10.

The others guard cleanup and reflection fallbacks: `ListFunctionCmdletBase.CleanupCore`, and two in `ScriptBlockInvocationException`.

**Fix idea:** Don't call `Debug.Fail` on paths that user input can reach. Use `Debug.WriteLine`, or nothing.

**Fixed:** All five calls now use `Debug.WriteLine`, including the three that guard cleanup and reflection fallbacks. A `Debug.Fail` on any path that a cmdlet runs would end a whole test run instead of failing one test. The `Bug14` tests in `tests/New-Dictionary.Tests.ps1` and `tests/New-SortedSet.Tests.ps1` cover the two calls that user input reaches.

**Tests:** Pester only, and written (see **Fixed:**). Both calls that user input reaches are in Engine, but the fix only swapped `Debug.Fail` for `Debug.WriteLine`, and the Pester tests already reach both calls in both builds.

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

**Tests:** Pester only, because the alias is a cmdlet parameter attribute. Each condition cmdlet gets one test, in `tests/Assert-AnyObject.Tests.ps1`, `tests/Assert-AllObject.Tests.ps1`, `tests/Find-IndexOf.Tests.ps1`, and `tests/Find-LastIndexOf.Tests.ps1`, so that all four agree on `-ScriptErrorAction`.

### 17 — New-SortedSet silently skips elements it can't convert

**Where:** `NewSortedSetCmdlet.ProcessCore` skips an element when `LanguagePrimitives.TryConvertTo` fails. New-List and New-HashSet write a non-terminating error in the same situation.

```powershell
(1, 'abc', 2 | New-SortedSet [int]) -join ','    # 1,2, with no error
(1, 'abc', 2 | New-HashSet [int]).Count          # 2, with a conversion error for 'abc'
```

**Fix idea:** Write the same conversion error that New-List writes.

**Tests:** Pester only, in `tests/New-SortedSet.Tests.ps1`. The cause is in `NewSortedSetCmdlet.ProcessCore`.

### 18 — New-Dictionary's `-CaseSensitive` can't be combined with `-InputObject`

**Where:** The dynamic `-CaseSensitive` belongs only to the `StringDict` parameter set, and `InputObject` belongs only to `JustCopy` and `WithCustomEqualityAndCopy`.

```powershell
@{ a = 1 } | New-Dictionary [string] -CaseSensitive
# Error: The input object cannot be bound to any parameters for the command...
# Output: an empty Dictionary[string, object]
```

**Fix idea:** Also add `-CaseSensitive` to the `JustCopy` set.

**Tests:** Pester only, in `tests/New-Dictionary.Tests.ps1`. The cause is in the cmdlet's parameter sets.

### 19 — ConvertTo-Dictionary stores the whole input object when the value is `$null`

**Where:** `ConvertToDictionaryCmdlet.AddToDictionary` uses the input object itself as the value whenever the value selector returns `$null`. With a typed value, adding it then fails.

```powershell
$items = [pscustomobject]@{ K = 'a'; V = 'x' }, [pscustomobject]@{ K = 'b'; V = $null }
$d = $items | ConvertTo-Dictionary K V
# Error: The value "@{K=b; V=}" is not of type "System.String" and cannot be used in this generic collection.
$d.Count     # Expected: 2, with $d['b'] -eq $null. Actual: 1
```

**Fix idea:** Store `$null` when a value selector returns `$null`, and fall back to the input object only when there's no value selector.

**Tests:** Pester only, in a new `tests/ConvertTo-Dictionary.Tests.ps1`. The cause is in `ConvertToDictionaryCmdlet.AddToDictionary`.

### 20 — New-Dictionary's `[object]` keys turn case-sensitive when `-ValueType` isn't `[object]`

**Where:** New-Dictionary builds its case-insensitive `Hashtable` only when both types are `[object]` (`DictionaryCtor.ShouldConstructDefault`). With any other value type, `[object]` keys get `EqualityComparer<object>.Default`, which compares strings case-sensitively, and `-CaseSensitive` is offered but changes nothing.

```powershell
$d = New-Dictionary -ValueType ([int])     # Dictionary[object, int]
$d['a'] = 1
$d['A'] = 2
$d.Count                                   # 2; the default Hashtable gives 1
```

**Fix idea:** Decide how `[object]` keys should compare, and use that rule on both paths. New-HashSet's `ObjectEqualityComparer` is one option.

**Tests:** Both.

- Engine: a new `Modern/Constructors/DictionaryCtorTests.cs`. `[object]` keys follow the chosen rule whatever the value type is, and `IsCaseSensitive` changes them.
- Pester: `tests/New-Dictionary.Tests.ps1`, with the repro, plus `-CaseSensitive` with a non-`[object]` `-ValueType`.

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

**Tests:** Pester only, because the cause is in `ListFunctionCmdletBase`. The repros go in `tests/Find-LastIndexOf.Tests.ps1` and `tests/Find-IndexOf.Tests.ps1`. Matching tests in `tests/Assert-AnyObject.Tests.ps1` and `tests/Assert-AllObject.Tests.ps1` keep all four cmdlets handling errors the same way.
