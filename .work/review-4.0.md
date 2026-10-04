# ListFunctions 4.0 review list

Found in a code review on 2026-10-04, after every item in `bugs.md` except the release item, 15, was fixed. At that point the build had no warnings and every test passed: 195 Pester tests in each edition and 364 Engine tests. None of these items shows up as a failing test.

Each item is code or behavior that's wrong, inconsistent, or expensive to change after 4.0.0 ships. Most fixes change behavior that users can see, so they'd be breaking changes after the release. The items under **Can wait** are the exception.

Item numbers continue from `bugs.md`, so each number names one item in either file. Packaging, meaning the manifest and the DLLs shipped under `ListFunctions/`, stays in `bugs.md` item 15. When you fix an item, check it off and add a **Fixed:** note under it that says what changed.

## Checklist

**Wrong results**

- [ ] 23 — Collections with script comparers break in other runspaces
- [ ] 24 — An `[object]` sorted set has no consistent order when its elements' types differ
- [ ] 25 — `-CaseSensitive` switches to a culture-sensitive comparison
- [ ] 26 — ConvertTo-Dictionary throws a NullReferenceException when no key is given
- [ ] 27 — ConvertTo-Dictionary's `-ValueType` doesn't work without a value selector
- [ ] 28 — ConvertTo-Dictionary silently ignores some arguments
- [ ] 29 — Open generic `[OutputType]` types break member completion

**Decisions**

- [ ] 30 — String comparison rules differ between cmdlets and element types
- [ ] 31 — ConvertTo-Dictionary converts every key and value to the first object's types
- [ ] 32 — New-Dictionary drops entries whose value is `$null`
- [ ] 33 — A failing comparison script has a different effect in each collection cmdlet
- [ ] 34 — Script-block parameters reject bad input in different ways
- [ ] 35 — The output type depends on the input
- [ ] 36 — New-HashSet can't combine `-GenericType` with script equality
- [ ] 37 — Parameter names, aliases, and positions differ between cmdlets
- [ ] 38 — Each cmdlet handles `$null` input differently
- [ ] 39 — `-InputObject` gives wrong answers in two cases
- [ ] 40 — Condition script blocks hide their errors by default
- [ ] 41 — Command, alias, and class names

**Robustness**

- [ ] 42 — Every cmdlet reads private members of PSObject, with no fallback
- [ ] 43 — The Windows PowerShell 5.1 assembly resolver answers for every module
- [ ] 44 — Compatibility shims are public types in other projects' namespaces

**Public surface and dead code**

- [ ] 45 — Engine and Next have more public types than they need
- [ ] 46 — About 1,600 lines of code are dead or used only by tests
- [ ] 47 — Leftover members, unused extension points, and TODOs in the XML docs

**Can wait**

- [ ] 48 — `[ValidateScriptVariable]` rejects script blocks that have a `param()` block
- [ ] 49 — New-Dictionary's `-InputObject` takes only a hashtable
- [ ] 50 — There's no completion for type names and no help content
- [ ] 51 — The legacy script implementation is still in the repo

## Running the repros

Every repro was checked on 2026-10-04 against the Debug build of commit `e07546b`. The editions were PowerShell 7.6.6 and Windows PowerShell 5.1.26100, with the en-US culture. The two editions behave the same unless an item says otherwise.

Use stable PowerShell only: Windows PowerShell 5.1, and PowerShell 7.6 or a later stable release from `$env:PROGRAMFILES\PowerShell\7`. The `pwsh` on PATH can be a preview. Run each repro in a new process, and import the build like this:

```powershell
$PSModuleAutoLoadingPreference = 'None'
Import-Module Microsoft.PowerShell.Utility, Microsoft.PowerShell.Management
# PowerShell 7:
Import-Module <repo>\src\engine\ListFunctions-Next\bin\Debug\net10.0\ListFunctions.Next.dll -ErrorAction Stop
# Windows PowerShell 5.1:
Import-Module <repo>\src\engine\ListFunctions-NETFramework\bin\Debug\net48\ListFunctions.NETFramework.dll -ErrorAction Stop
(Get-Module ListFunctions*).Path
```

- **An installed copy can stand in for the build.** If ListFunctions is installed, for example 3.1.0 in the system module folder, it loads silently whenever the import fails, and every result then comes from that copy.
- **Relative paths fail this way.** A relative path without a leading `.\` makes the import fail, because PowerShell treats it as a module name.
- **Turning off autoloading prevents the fallback.** It also means you have to import the modules that hold `Get-Item`, `Select-Object`, and other common commands yourself, which the second line does.

Most repros use this variable:

```powershell
$people = [pscustomobject]@{ Id = 1; Name = 'Ann' }, [pscustomobject]@{ Id = 2; Name = 'Bob' }
```

## Tests

Don't follow the test-first approach of `bugs.md`. Fix an item without writing a failing test first, and don't give every item a test.

Write a test only when it has merit:

- **Has merit:** it pins down behavior that users rely on and that a later change could plausibly break, such as the comparison rule chosen for 30 or the error that 23 adds. A test also has merit when it records a decision that the code doesn't make obvious.
- **Has no merit:** it only restates the fix, or it covers something that can't regress in a way users would notice.
- **Usually needs no new test:** removing dead code, making types internal, renaming, and fixing documentation. Run the existing suites instead.

When a test has merit, write it whenever it's most useful, before or after the fix. Don't tag it `BugNN` or with an item number.

The `lf-testing` skill has the same rule, and covers how to run and write tests.

## Wrong results

### 23 — Collections with script comparers break in other runspaces

**Where:** the comparers that New-HashSet, New-Dictionary, and New-SortedSet build from script blocks. `EqualityBlock.Equals` reuses one variable list and the same `PSVariable` objects for every call (`src/engine/ListFunctions.Engine/Modern/EqualityBlock.cs:144`). `HashBlock`, `ComparingBlock<T>`, and `PSThisVariable` hold shared state the same way.

```powershell
# PowerShell 7 only.
$set = 'apple', 'pear' | New-HashSet -EqualityScript { $x -eq $y } -HashCodeScript { $_.ToUpperInvariant().GetHashCode() }
$set.Contains('APPLE')    # True
1..4 | ForEach-Object -ThrottleLimit 4 -TimeoutSeconds 60 -Parallel {
	$s = $using:set
	foreach ($i in 1..10) {
		try { $s.Contains('APPLE') } catch { 'ERR: ' + $_.Exception.GetBaseException().Message }
	}
}
# Expected: 40 × True.
# In one run: 30 × True, 2 × False, and 8 errors in 5.2 s. The errors included "Index was outside the bounds of the
# array." and "Source array was not long enough."
# An earlier run without -TimeoutSeconds didn't finish in 3 minutes.
```

What else the review measured:

- **Other ways in fail the same way.** `Start-ThreadJob`, a RunspacePool in either edition, and PLINQ worker threads also gave wrong answers and errors. The errors included:
  - "Collection was modified; enumeration operation may not execute."
  - "Object reference not set to an instance of an object."
  - "An item with the same key has already been added. Key: psitem"
- **Each call is slow.** A call from another runspace took 0.25–0.5 s, even when its answer was right.
- **The owning runspace isn't safe either.** While another runspace used the set, calls in the runspace that created it failed too.
- **A control without script blocks was fine.** A plain `HashSet[string]` in the same tests had no errors.
- **PowerShell's own guard doesn't catch it.** PowerShell refuses a bare script block in `$using:`, but a set gets its script blocks past that check.

The README doesn't mention threads or runspaces.

**Cause:** Concurrent calls overwrite each other's variables in the shared list, and PowerShell runs each call's script block in the runspace that created it.

**Fix idea:**

- Record the runspace in each comparer when it's created.
- When a call arrives from another runspace, or from a thread with no runspace, throw an error that says so instead of running the script block.
- Remove the shared mutable state, so concurrent calls can't corrupt each other.
- Document in the README that these collections work only in the runspace that created them.

**Decision needed:** whether to support use from other runspaces at all. Supporting it means paying the cost of running each call's script block back in the runspace that created it, or running it in the caller's runspace.

### 24 — An `[object]` sorted set has no consistent order when its elements' types differ

**Where:** `ObjectComparer.Compare` in `src/engine/ListFunctions.Engine/Modern/Constructors/SortingCollectorCtor.cs:189`, which calls `LanguagePrimitives.Compare`.

```powershell
foreach ($order in @(1, '10', 9, '2'), @('10', 9, '2', 1), @(9, '2', 1, '10')) {
	$s = New-SortedSet -InputObject $order
	'{0} -> {1}  Contains(''10'')={2}' -f ($order -join ','), ($s -join ','), $s.Contains('10')
}
# 1,10,9,2 -> 1,2,9,10  Contains('10')=False
# 10,9,2,1 -> 1,9,10,2  Contains('10')=True
# 9,2,1,10 -> 1,10,2,9  Contains('10')=True
# Expected: one order for the same four elements, and Contains('10') = True every time.
```

**Cause:** `LanguagePrimitives.Compare` converts the second operand to the first operand's type. With mixed types, both `9 -lt '10'` and `'10' -lt 9` are `$true`, so the comparer isn't a consistent order, and the set's tree breaks. The README says an `[object]` set compares "the way PowerShell's `-lt` and `-gt` operators do", which is exactly what happens.

**Fix idea:** Make the comparer a total order. For example, compare numbers as numbers and strings as strings, and order the different kinds of value by a fixed rank. The alternative is to reject mixed element types.

**Decision needed:** which order mixed types get. The `[object]` HashSet has the same asymmetry in `LanguagePrimitives.Equals`, but it hashes each element's string form, which hides most of it (see 30).

### 25 — `-CaseSensitive` switches to a culture-sensitive comparison

**Where:** `GetCustomEqualityComparer` in `src/engine/ListFunctions-Next/Cmdlets/Constructs/EqualityConstructingCmdlet.cs:349`, and `GetObjectKeyComparer` in `src/engine/ListFunctions.Engine/Modern/Constructors/DictionaryCtor.cs:104`. Without the switch, strings compare with `StringComparer.OrdinalIgnoreCase`. With it, they compare with `StringComparer.CurrentCulture`.

```powershell
$decomposed = 'e' + [char]0x301      # e followed by a combining acute accent
$precomposed = [string][char]0xE9    # é
(New-HashSet [string] -InputObject $decomposed, $precomposed).Count                  # 2
(New-HashSet [string] -InputObject $decomposed, $precomposed -CaseSensitive).Count   # Expected: 2. Actual: 1
$hyphenated = 'a' + [char]0xAD + 'b'   # a soft hyphen between a and b
(New-HashSet [string] -InputObject 'ab', $hyphenated -CaseSensitive).Count           # Expected: 2. Actual: 1
$d = New-Dictionary [string] [int] -CaseSensitive
$d[$decomposed] = 1
$d[$precomposed] = 2
$d.Count                                                                              # Expected: 2. Actual: 1
# Windows PowerShell 5.1 only:
(New-HashSet [string] -InputObject 'strasse', ('stra' + [char]0xDF + 'e') -CaseSensitive).Count   # Expected: 2. Actual: 1
```

The README describes `[string]` comparison as "Ordinal, without regard to case", and says that `-CaseSensitive` "makes string comparisons case-sensitive." A culture-sensitive comparison treats canonically equivalent strings as equal and ignores some characters. Its result also depends on the user's culture and on the edition: Windows PowerShell 5.1 compares with NLS, and PowerShell 7 with ICU.

**Fix idea:** Use `StringComparer.Ordinal` for `-CaseSensitive`, the case-sensitive counterpart of `OrdinalIgnoreCase`. The `[object]` sets and keys are covered by 30.

### 26 — ConvertTo-Dictionary throws a NullReferenceException when no key is given

**Where:** `src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:46`. The default parameter set, `None`, contains neither `-KeyPropertyName` nor `-KeySelector`, so `InferTypes` runs a `$null` key selector.

```powershell
$people | ConvertTo-Dictionary
# Expected: a binding error that names -KeyPropertyName or -KeySelector.
# Actual: Object reference not set to an instance of an object.
$people | ConvertTo-Dictionary -ValuePropertyName Name    # The same error
```

**Fix idea:** Remove the `None` set, or make a key parameter set the default, so that the binder asks for the key.

### 27 — ConvertTo-Dictionary's `-ValueType` doesn't work without a value selector

**Where:** `AddToDictionary` in `src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:398`. It converts a value only when there's a value selector, so otherwise the input object goes into the dictionary as it is.

```powershell
$d = 1, 2 | ConvertTo-Dictionary -KeySelector { $_ } -ValueType string
# Actual: two errors, such as 'The value "1" is not of type "System.String" and cannot be used in this generic
# collection.', and $d is an empty Dictionary[int, string].
# Expected: 1 = '1' and 2 = '2', which is what $d.Add(1, 1) stores.
```

The same missing conversion breaks input of mixed types without `-ValueType`, measured on 2026-10-04:

```powershell
1, '2' | ConvertTo-Dictionary -KeySelector { [int]$_ }
# A Dictionary[int, int] that holds only 1 = 1. '2' gets the dictionary's own error, 'The value "2" is not of type
# "System.Int32" and cannot be used in this generic collection.', instead of the conversion error that New-List writes.
1, '2' | ConvertTo-Dictionary -KeySelector { [int]$_ } -DuplicateKeyBehavior Skip
# The same failure is a terminating error, because AddSkip doesn't catch the dictionary's ArgumentException.
```

- **PowerShell doesn't convert the value for the cmdlet.** In a script, `$d.Add(1, 1)` works because PowerShell's method binder converts each argument with `LanguagePrimitives`. The cmdlet calls `IDictionary.Add` from C#, which only casts, so the cmdlet has to convert the value itself.
- **Custom objects survive the conversion.** `LanguagePrimitives.ConvertTo` to `[object]` keeps a `[pscustomobject]` and its properties, so `$people | ConvertTo-Dictionary Id` still stores the whole objects.
- **The conversion alone doesn't help objects of different types.** With only this fix, a `FileInfo` that follows a `DirectoryInfo` would still fail, with a conversion error instead. 31's decision, `[object]` values unless `-ValueType` is given, removes that case.

**Fix idea:** When there's no value selector, convert the input object to the value type, the same way a selected value is converted.

**Decided on 2026-10-04:** as the fix idea says. Every value goes through `LanguagePrimitives`, whether it comes from a selector or is the input object itself.

### 28 — ConvertTo-Dictionary silently ignores some arguments

**Where:**

- **`-ValuePropertyName`:** `BeginCore` checks `ValuePropertyName is string` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:186`). The parameter is an `[object]`, though, and a string can arrive wrapped in a `PSObject`.
- **`-KeyPropertyName`:** it's a `[string]`, so a script block passed to it, or to its aliases `-Key` and `-KeyName`, becomes a property name made of the script's text.

```powershell
$name = [psobject]'Name'      # The way Get-Content delivers each line
($people | ConvertTo-Dictionary Id $name)[1]            # Expected: Ann. Actual: the whole object, @{Id=1; Name=Ann}
($people | ConvertTo-Dictionary -Key { $_.Id }).Count   # Expected: 2, or a binding error. Actual: 0, with no error
($people | ConvertTo-Dictionary Nope).Count             # 0, with no warning that no object has a property Nope
($people | ConvertTo-Dictionary Id -ValuePropertyName Name -ValueSelector { $_.Id * 10 })[1]   # Ann: -ValueSelector is ignored
```

**Fix idea:**

- Unwrap a `PSObject` before the type test, and reject a value that's neither a string nor a script block.
- Reject a script block passed to `-KeyPropertyName`, or treat it as `-KeySelector`.
- Don't let `-ValuePropertyName` and `-ValueSelector` be used together.
- Write a warning when every key is `$null`.

### 29 — Open generic `[OutputType]` types break member completion

**Where:** `[OutputType(typeof(List<>))]` in `src/engine/ListFunctions-Next/Cmdlets/Constructs/NewListCmdlet.cs:24`, and the same pattern in `NewHashSetCmdlet.cs:32`, `NewSortedSetCmdlet.cs:27`, and `NewDictionaryCmdlet.cs:34`. ConvertTo-Dictionary has no `[OutputType]`.

```powershell
TabExpansion2 -inputScript '$l = New-List; $l.Ad' -cursorColumn 20
# Exception calling "CompleteInput" with "3" argument(s): "startIndex cannot be larger than length of string."
```

- **The other sets fail the same way:** `(New-List).Ad`, `(New-HashSet).Ad`, and `(New-SortedSet).Ad`.
- **New-Dictionary completes,** because it also lists `Hashtable`.
- **A closed type completes:** a function with `[OutputType([System.Collections.Generic.List[object]])]` completes `Add(` and `AddRange(`.

**Fix idea:** Name a closed type, such as `List<object>`, which is the output when no type is given. Give ConvertTo-Dictionary an `[OutputType]` too. The crash itself is PowerShell's, and could be reported to the PowerShell team.

## Decisions

Each of these needs a choice first. After the change, update the README to match.

### 30 — String comparison rules differ between cmdlets and element types

**Where:**

- **`StringComparer.OrdinalIgnoreCase`:** New-HashSet `[string]`, the `[string]` and `[object]` keys of New-Dictionary (including its Hashtable), and the `[string]` keys of ConvertTo-Dictionary (`src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:306`).
- **`LanguagePrimitives.Equals` and `Compare`, which use the invariant culture:** New-HashSet `[object]` (`src/engine/ListFunctions.Engine/Modern/Constructors/HashSetCtor.cs:130`), which hashes each element by its string form, and New-SortedSet `[object]`.
- **`StringComparer.InvariantCultureIgnoreCase`:** New-SortedSet `[string]` (`src/engine/ListFunctions.Engine/Modern/Constructors/SortingCollectorCtor.cs:95`).
- **`StringComparer.CurrentCulture`:** New-HashSet and New-Dictionary with `-CaseSensitive` (25).

```powershell
$hyphenated = 'a' + [char]0xAD + 'b'
(New-HashSet [string] -InputObject 'ab', $hyphenated).Count    # 2
(New-HashSet -InputObject 'ab', $hyphenated).Count             # 1
(New-SortedSet [string] -InputObject 'ab', $hyphenated).Count  # 1
$h = New-Dictionary; $h['ab'] = 1; $h[$hyphenated] = 2; $h.Count    # 2
(New-HashSet -InputObject 'strasse', ('stra' + [char]0xDF + 'e')).Count   # 1 in Windows PowerShell 5.1, 2 in PowerShell 7.6
(New-HashSet -InputObject 1, '01').Count                       # 2, although 1 -eq '01' is True
```

The README calls `[object]` sets "Like PowerShell's `-eq` operator". They behave that way only when both values have the same string form.

**Decision needed:** one rule for every comparison the module chooses:

- **Ordinal:** it matches the `[string]` sets, the dictionaries, and `@{}`, and it doesn't depend on the culture or the edition.
- **Culture-based:** it matches `-eq` and `Sort-Object`.

For sorted sets, the rule also decides the sort order that users see. Whatever the choice, 25 should follow it.

### 31 — ConvertTo-Dictionary converts every key and value to the first object's types

**Where:** `AddToDictionary` and `InferTypes` in `src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:384` and `:457`.

```powershell
$items = [pscustomobject]@{ K = 'a'; V = 1 }, [pscustomobject]@{ K = 'b'; V = 2.5 }
($items | ConvertTo-Dictionary K V)['b']       # Expected: 2.5. Actual: 2, with no error

$items = [pscustomobject]@{ K = 1; V = 'x' }, [pscustomobject]@{ K = 1.6; V = 'y' }, [pscustomobject]@{ K = '2'; V = 'z' }
$d = $items | ConvertTo-Dictionary K V         # An error: "An item with the same key has already been added."
$d.Keys -join ','                              # 1,2: 1.6 became 2, and 'z' lost to it as a duplicate

$a = [pscustomobject]@{ Id = $null }, [pscustomobject]@{ Id = 'A' }, [pscustomobject]@{ Id = 'a' }
($a | ConvertTo-Dictionary Id).Count           # 2: the $null key made the keys [object], compared with case
$b = [pscustomobject]@{ Id = 'A' }, [pscustomobject]@{ Id = $null }, [pscustomobject]@{ Id = 'a' }
($b | ConvertTo-Dictionary Id).Count           # 1, plus a duplicate-key error for 'a'
```

The README documents the conversion. It doesn't say that the conversion rounds numbers, or that the order of the input decides the key type and how keys compare.

More cases, measured on 2026-10-04:

```powershell
# An object that's skipped for its $null key still decides the value type.
$items = [pscustomobject]@{ K = $null; V = 'x' }, [pscustomobject]@{ K = 'a'; V = 1 }
($items | ConvertTo-Dictionary K V)['a']      # '1', in a Dictionary[object, string]

# A $null first key makes a string comparer unusable.
$items = [pscustomobject]@{ K = $null; V = 1 }, [pscustomobject]@{ K = 'a'; V = 2 }
$items | ConvertTo-Dictionary K V -KeyComparer ([System.StringComparer]::Ordinal)
# A terminating error: "Failed to instantiate dictionary with the arguments supplied - Constructor on type
# 'System.Collections.Generic.Dictionary`2[[System.Object, ...],[System.Int32, ...]]' not found."

# A $null first value makes the values [object], so the input order decides the value type.
$items = [pscustomobject]@{ K = 'a'; V = $null }, [pscustomobject]@{ K = 'b'; V = 2 }
$items | ConvertTo-Dictionary K V                  # Dictionary[string, object]: a = $null, b = 2
$items[1], $items[0] | ConvertTo-Dictionary K V    # Dictionary[string, int]: b = 2, a = 0

# When each object is its own value, an object of another type is rejected.
Get-Item "$env:windir", "$env:windir\notepad.exe" | ConvertTo-Dictionary Name
# A Dictionary[string, DirectoryInfo] that holds only the Windows folder, and an error for notepad.exe: 'The value
# "C:\WINDOWS\notepad.exe" is not of type "System.IO.DirectoryInfo" and cannot be used in this generic collection.'
```

- **When every key is `$null`,** the result is an empty `Dictionary[object, …]`, while no input gives an empty `Hashtable` (see 35).
- **The `-KeyComparer` failure** has the same cause as the one in 37: a comparer that doesn't fit the inferred key type.
- **27's fix doesn't help objects of different types.** `notepad.exe` then fails with a conversion error from `FileInfo` to `DirectoryInfo`. `Get-ChildItem` lists folders before files, so a folder that holds both loses all its files.

**Decided on 2026-10-04:** the cmdlet stops inferring types.

- **Keys:** the key type is `[object]` unless the new `-KeyType` parameter is given (see 37).
- **Values:** the value type is `[object]` unless `-ValueType` is given.
- **Neither parameter is mandatory,** and each can be given without the other, such as `[object]` keys with `[int]` values.
- **The result is always a `Dictionary[TKey, TValue]`:** a `Dictionary[object, object]` when neither type is given, and never a `Hashtable`, even when there's no input. New-Dictionary changes the same way (see 35).
- **What that removes:** the order dependence, a `$null` first key that changes how keys compare, and the rejected objects of other types. A value such as `2.5` is rounded only when the caller asks for a type such as `[int]`, and then it converts the way `Add` converts it, without an error. Because the types no longer depend on the input, the dictionary can also be created before the first input object arrives (see 35).
- **What it costs:** keys and values are no longer typed by default.
  - **The README's examples change,** such as the `Dictionary[int, string]` it shows for `$people | ConvertTo-Dictionary Id Name`.
  - **Dot notation stops reading keys.** `($people | ConvertTo-Dictionary Name).Ann` gives Ann's object today. With `[object]` keys, it gives `$null` without an error, unless `-KeyType ([string])` is given (see 35).
- **A `$null` input object** is skipped, as it is now.
- **A `$null` key** is a non-terminating error instead of a silent skip, and its object is skipped. The errors also make the fix idea in 28, a warning when every key is `$null`, unnecessary. The error isn't terminating because:
  - **It matches the cmdlet's other key errors.** A duplicate key under `-DuplicateKeyBehavior Error`, and a key that can't be converted, already write non-terminating errors, although `Dictionary.Add` throws for both.
  - **A terminating error would lose the whole dictionary,** because ConvertTo-Dictionary writes it only at the end. `-ErrorAction Stop` can still stop at the first `$null` key.
  - **It matches a native loop.** Measured in both editions, `$h.Add($null, $_)` in a `ForEach-Object` loop writes one error for that item and adds the others. For comparison, a hashtable literal or an indexer with a `$null` key ends its statement, and `Group-Object -AsHashTable` writes one non-terminating error that calls the `$null` key a key duplication, and outputs nothing.
- **A `$null` value** is always stored, because it may be intentional. It goes through `LanguagePrimitives` like every other value (see 27), so it only has to be stored, not kept as `$null`. With `[object]` values, it stays `$null`. With a `-ValueType`, it becomes what that type stores for `$null`, such as `''` for `[string]` and `0` for `[int]`. When `$null` can't be converted to that type, as for `[datetime]` (see 38), it gets the conversion error that any value that can't be converted gets, and `[Nullable[datetime]]` stores it instead.

**Decision needed:**

- **How `[object]` keys compare when `-KeyComparer` isn't given.** ConvertTo-Dictionary compares only `[string]` keys without regard to case, so with `[object]` keys, `$people | ConvertTo-Dictionary Name` would become case-sensitive. New-Dictionary's `[object]` keys follow the `Hashtable`'s rule instead: strings compare with `OrdinalIgnoreCase`, and other keys with their own `Equals` (`bugs.md` item 20, and see 30).
- **How `-KeyComparer` fits `[object]` keys.** A `StringComparer` isn't an `IEqualityComparer[object]`, so it can't be passed to a `Dictionary[object, TValue]` as it is. New-Dictionary wraps such a comparer in an `EqualityComparerAdapter[T]`. ConvertTo-Dictionary calls `Activator.CreateInstance` itself (`src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:317`), so it gets neither the adapter nor the `Hashtable` rule. Building the dictionary with `DictionaryCtor`, as New-Dictionary does, would give it both.

### 32 — New-Dictionary drops entries whose value is `$null`

**Where:** `NewDictionaryCmdlet.Process` calls `AddToCollection` with `addIfNull: false` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewDictionaryCmdlet.cs:212`). `AddMethodInvoker.TryInvoke` then skips any call that has a `$null` argument (`src/engine/ListFunctions.Engine/Modern/AddMethodInvoker.cs:83`).

```powershell
(@{ a = $null; b = 1 } | New-Dictionary).Count       # Expected: 2. Actual: 1
$h = @{}; $h.Add('x', $null)
($h | New-Dictionary [string] [int]).Count           # Expected: 1, holding x = 0. Actual: 0
```

- **The skip is deliberate:** the XML docs say that `$null` values are skipped, and `bugs.md` item 08 kept that behavior.
- **It breaks a later rule:** the rule decided in items 04 and 19 says a typed collection stores what `Add` stores, which is `0` here.
- **The README doesn't mention it:** its `-InputObject` row says nothing about the skip.

**Decided on 2026-10-04:** keep these entries, and convert their `$null` values the way `Add` does. The rule decided for ConvertTo-Dictionary under 31 applies here too: a `$null` value may be intentional, so it's never skipped, and it's converted through `LanguagePrimitives` like every other value. New-Dictionary's value type is always `-ValueType` or `[object]`, so a `$null` for a value type that can't hold one, such as `[datetime]`, would get a conversion error, like an explicit `-ValueType` in ConvertTo-Dictionary (see 38).

### 33 — A failing comparison script has a different effect in each collection cmdlet

**Where:**

- **New-HashSet:** `NewHashSetCmdlet.Process` returns `$false` when an element can't be added (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewHashSetCmdlet.cs:151`). The base class then stops the commands upstream and writes nothing.
- **New-SortedSet:** `NewSortedSetCmdlet.ProcessCore` writes an error and moves on.
- **New-Dictionary:** `AddToCollection` writes an error and moves on.

```powershell
$eq = { if ($x -eq 2 -or $y -eq 2) { throw 'boom' }; $x -eq $y }
$set = 1, 2, 3 | New-HashSet -EqualityScript $eq -HashCodeScript { $_ * 0 }
$null -eq $set      # True: one error, no set at all, and 3 never reached the cmdlet
$sorted = 5, 3, 1 | New-SortedSet [int] -ComparingScript { if ($x -eq 3 -or $y -eq 3) { throw 'boom' }; $x.CompareTo($y) }
$sorted -join ','   # 1,5: one error, and the set without 3
'still running'     # Runs: neither error ends the script
```

- **New-Dictionary behaves like New-SortedSet:** one error, and a dictionary without the failed entry.
- **New-HashSet's behavior is deliberate:** its XML docs describe it.
- **The README disagrees with all three:** it says that with the default `-ScriptBlockErrorAction Stop`, errors in these script blocks "are terminating errors" (`README.md:436`).
- **The condition cmdlets differ:** for them, `-ScriptBlockErrorAction Stop` does end the script.

**Decision needed:** what `Stop` means for the collection cmdlets:

- **End the script,** as it does for the condition cmdlets.
- **Write a non-terminating error** for each element that fails, and still write the rest of the collection.

Either way, New-HashSet has to match the other two, and the README has to match the code.

### 34 — Script-block parameters reject bad input in different ways

```powershell
New-HashSet -EqualityScript { begin {} process { $x -eq $y } } -HashCodeScript { $_ }
# A binding error: "block is not a proper script block."
New-Dictionary -EqualityScript { begin {} process { $x -eq $y } } -HashCodeScript { $_ }
# A terminating error: "scriptBlock is not a script block. (Parameter 'scriptBlock')"
5, 3 | New-SortedSet -ComparingScript { begin {} process { $x - $y } }
# A non-terminating error for each comparison ("...contains more than one clause..."), and a set that holds 5
1 | Assert-AnyObject { begin {} process { $_ } }
# A terminating error: "...contains more than one clause..."

1 | Assert-AllObject -Condition $null    # A run-time error: "Asserting an all-true condition requires a condition to be specified."
1 | Find-IndexOf -Condition $null        # A binding error: "Cannot bind argument to parameter 'Condition' because it is null."
1 | Assert-AnyObject -Condition $null    # True, the same as no condition
```

**Fix idea:** Check the shape of every script-block parameter at binding time with one attribute, as `[IsScriptBlock]` does for New-HashSet. Decide once what `-Condition $null` means, and apply it to all four condition cmdlets.

### 35 — The output type depends on the input

```powershell
(New-Dictionary).GetType().Name                         # Hashtable
(New-Dictionary -ValueType ([int])).GetType().Name      # Dictionary`2
(@() | ConvertTo-Dictionary Id).GetType().Name          # Hashtable
$staff = [pscustomobject]@{ Name = 'Ann'; Dept = 'IT' }, [pscustomobject]@{ Name = 'Jim'; Dept = 'IT' }, [pscustomobject]@{ Name = 'Jane'; Dept = 'HR' }
$d = $staff | ConvertTo-Dictionary Dept Name -DuplicateKeyBehavior Concatenate
$d['HR'].GetType().FullName                             # System.String
$d['IT'].GetType().FullName                             # ListFunctions.Modern.ObjectList
```

The README documents New-Dictionary's Hashtable. It doesn't document ConvertTo-Dictionary's empty Hashtable: its command table lists only `Dictionary[TKey, TValue]`.

**Decision needed:**

- Should ConvertTo-Dictionary always return a Dictionary?
- Should `Concatenate` store a list for every key, so that code reading the values doesn't have to check their type?
- Should `ObjectList` stay a public type that users test for?

**Decided on 2026-10-04:** ConvertTo-Dictionary and New-Dictionary always write a `Dictionary[TKey, TValue]`, whatever their input. Neither cmdlet writes a `Hashtable`.

- **New-Dictionary:** its default `[object]` keys and values give a `Dictionary[object, object]`. The keys keep the `Hashtable`'s comparison, which `DictionaryCtor` already gives `[object]` keys for any other value type: strings compare without regard to case unless `-CaseSensitive` is given (see 25 and 30), and other keys with their own `Equals`. The `Hashtable` comes from `DictionaryCtor.ShouldConstructDefault` and `ConstructTDefault` (`src/engine/ListFunctions.Engine/Modern/Constructors/DictionaryCtor.cs:139` and `:64`).
- **ConvertTo-Dictionary:** with no input, it writes the empty dictionary that it created before the first input object, typed by `-KeyType` and `-ValueType` (see 31), instead of the `Hashtable` at `src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:429`. Once `DictionaryCtor` stops creating a `Hashtable`, ConvertTo-Dictionary can build its dictionary with it without getting one back, as 31's second open question suggests.
- **`[OutputType]`:** `Hashtable` leaves New-Dictionary's `[OutputType]`. It's the reason New-Dictionary's members complete today, so 29's fix has to land with this one.
- **README:** its command table (`README.md:30`) and its New-Dictionary section (`README.md:314`) describe the `Hashtable`.

What changes for users, measured on 2026-10-04 in both editions. New-Dictionary already writes a dictionary with `[object]` keys when it's given only `-ValueType`:

```powershell
$h = New-Dictionary                      # A Hashtable
$d = New-Dictionary -ValueType ([int])   # A Dictionary[object, int]
$h['Ann'] = 1; $d['Ann'] = 1
$h.Ann        # 1
$d.Ann        # $null, with no error, although the key exists. $d['Ann'] is 1.
$h.Bob = 2    # Adds the key Bob
$d.Bob = 2    # An error: "The property 'Bob' cannot be found on this object. Verify that the property exists and can be set."
```

- **Dot notation:** PowerShell reads keys with dot notation on a `Hashtable`, a `Dictionary[string, …]`, and a `Dictionary[int, …]`, and sets them on the first two. On a dictionary with `[object]` keys, it does neither. Users who want dot notation can pass `-KeyType ([string])`, and the README's examples should use the indexer.
- **Type checks:** `$d -is [hashtable]` is `$false`, and a `Dictionary[object, object]` has no `Clone()` method.
- **Conversions:**
  - `[pscustomobject]$d` returns the dictionary unchanged, while `[pscustomobject]$h` makes a custom object from the entries.
  - A `[hashtable]` parameter or cast still accepts the dictionary, as a new `Hashtable`, so a function that changes its `[hashtable]` parameter no longer changes the caller's dictionary.
- **Addition:** `$d + @{ x = 1 }` gives a `Hashtable`, so `$d += @{ x = 1 }` replaces the dictionary with one.
- **Display:** the first column is headed `Key` instead of `Name`.
- **What doesn't change:** `$d['missing']` gives `$null` without an error, even under `Set-StrictMode -Version Latest`. Splatting with `@d` works, and `ConvertTo-Json` writes the same properties.
- **What improves:** the keys enumerate in the order they were added, as long as none is removed. A `Hashtable`'s order comes from the keys' hash codes: in Windows PowerShell 5.1, adding `z`, `y`, `x`, `w`, `v` enumerates them as `v,w,z,x,y`, and in PowerShell 7 the order changes from one process to the next (see 49).

The questions about `Concatenate` and `ObjectList` are still open.

### 36 — New-HashSet can't combine `-GenericType` with script equality

**Where:** `GenericType` belongs to the `SpecifiedType` and `StringSet` parameter sets only (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewHashSetCmdlet.cs:66`).

```powershell
New-HashSet [int] -EqualityScript { $x -eq $y } -HashCodeScript { $_ }
# A positional parameter cannot be found that accepts argument '[int]'.
New-Dictionary [int] -EqualityScript { $x -eq $y } -HashCodeScript { $_ }
# Works: a Dictionary[int, object] whose comparer wraps the script blocks
```

The README says that the element type is always `[object]` in this mode. New-Dictionary and New-SortedSet accept a type with their script blocks, though, and 3.1.0's syntax listed `-GenericType` with them too.

**Fix idea:** Add `GenericType` to the `WithCustomEquality` set. `EqualityComparerAdapter<T>`, which the fix for `bugs.md` item 09 added, already handles value types.

### 37 — Parameter names, aliases, and positions differ between cmdlets

- **`-Capacity`:**
  - It's positional only on New-List. So `New-List [int] 5` gives an empty list with a capacity of 5, and `New-List [int] 1, 2, 3` fails with "Cannot convert 'System.Object[]' to the type 'System.Int32' required by parameter 'Capacity'."
  - The `Size` alias exists on New-List and New-Dictionary but not on New-HashSet.
  - The default is 4 on New-List and 0 elsewhere.
- **`-Condition`:** the `FilterScript` alias exists only on Assert-AnyObject and Assert-AllObject.
- **`-InputObject`:** the `List` alias exists only on Find-IndexOf and Find-LastIndexOf, and `CopyFrom` only on New-Dictionary.
- **`-ScriptBlockErrorAction`:** the `ScriptErrorAction` alias exists only on the four condition cmdlets, and ConvertTo-Dictionary doesn't have the parameter at all.
- **String comparison:**
  - New-HashSet and New-Dictionary take `-CaseSensitive`.
  - New-SortedSet has no such switch, although `SortingCollectorCtor.IsCaseSensitive` exists for it.
  - Only ConvertTo-Dictionary takes a comparer (`-KeyComparer`).
- **ConvertTo-Dictionary has no `-KeyType`.** A `-KeyComparer` that doesn't fit the inferred key type fails. `$people | ConvertTo-Dictionary Id -KeyComparer ([System.StringComparer]::Ordinal)` gives "Failed to instantiate dictionary with the arguments supplied - Constructor on type 'System.Collections.Generic.Dictionary`2[[System.Int32, ...],[System.Object, ...]]' not found."
- **New-SortedSet's parameter set name:** the set for `-ComparingScript` is named `WithCustomEquality`.

**Decision needed:** which of these to align. Adding an alias or a parameter later isn't a breaking change. Removing one, or changing a position or a default, is.

**Decided on 2026-10-04:** ConvertTo-Dictionary gets `-KeyType`, and stops inferring its key and value types (see 31). The rest of this item is still open.

### 38 — Each cmdlet handles `$null` input differently

```powershell
($null | New-List).Count                         # 0: skipped unless -IncludeNullElements is given
($null | New-List -IncludeNullElements).Count    # 1
($null | New-HashSet).Count                      # 1
($null | New-HashSet [int]).Count                # 0, with no error
($null | New-SortedSet).Count                    # 0
$null | New-Dictionary                           # "Cannot bind argument to parameter 'InputObject' because it is null.", and an empty Hashtable
$null | Find-IndexOf { $null -eq $_ }            # 0: counted as an element
```

ConvertTo-Dictionary skips `$null` objects and `$null` keys, but keeps `$null` values (`bugs.md` item 19). Only New-List has `-IncludeNullElements`.

**Correction, found on 2026-10-04:** ConvertTo-Dictionary doesn't keep every `$null` value. When `$null` can't be converted to the value type, the value writes a conversion error, and its entry is dropped:

```powershell
$items = [pscustomobject]@{ K = 'a'; V = [datetime]'2026-01-01' }, [pscustomobject]@{ K = 'b'; V = $null }
$d = $items | ConvertTo-Dictionary K V
# A Dictionary[string, datetime] without b, and the error 'Cannot convert value "" of type "null" to type
# "System.DateTime": Error: Cannot convert null to type "System.DateTime".'
($items | ConvertTo-Dictionary K V -ValueType ([Nullable[datetime]])).Count    # 2, with b = $null
```

- **Which types:** the cmdlet does the same for `[guid]` and for enums such as `[ConsoleColor]`. `LanguagePrimitives.ConvertTo($null, ...)` also fails for `[timespan]` and other structs, such as `KeyValuePair[string, int]`.
- **Which types are fine:** `$null` converts to `0` for `[int]`, `''` for `[string]`, and `$null` for classes such as `[version]`.
- **The error is hard to act on:** it shows the `$null` as `""`, and it doesn't name the key.

After 31's decision, this happens only when `-ValueType` names such a type. `[object]` values store `$null` as it is.

**Decided on 2026-10-04:** keep the differences, and document the reason for each one, instead of making one rule. A `$null` key isn't the same thing as a `$null` element in a list or a set.

- **ConvertTo-Dictionary:** a `$null` input object is skipped, a `$null` key writes a non-terminating error and its object is skipped, and a `$null` value is always stored (see 31).
- **New-Dictionary:** a `$null` value is kept too (see 32).

### 39 — `-InputObject` gives wrong answers in two cases

```powershell
'a', 'b' | Find-IndexOf { $_ -eq 1 } -InputObject 1, 2
# Two InputObjectNotBound errors, then -1. The -InputObject value is never searched.

$s = 1, 2, 3 | New-HashSet [int]
Find-IndexOf -InputObject $s { $_ -eq 2 }    # 0. A set isn't an IList, so it's one element, and $s -eq 2 is truthy.
$s | Find-IndexOf { $_ -eq 2 }               # 1
```

Only an `IList` passed to `-InputObject` supplies its elements (`ListFunctionCmdletBase.GetInputElements`, `src/engine/ListFunctions-Next/Cmdlets/ListFunctionCmdletBase.cs:483`). So the sets that this module builds don't.

More about the first case, measured on 2026-10-04 in both editions:

- **Why it happens:** `-InputObject` is the cmdlet's only pipeline parameter, and the command line binds it before the begin block. Each piped object then has no parameter left to bind to, so PowerShell writes an `InputObjectNotBound` error for it and skips the process block. The cmdlet processes neither input, and then writes its result for no input.
- **Every cmdlet does it:** Assert-AnyObject writes `$false`, Assert-AllObject writes `$true`, and New-List writes an empty list. Native `ForEach-Object` gets the same two errors but writes nothing.
- **The cmdlet can tell:** in the begin block, `MyInvocation.ExpectingInput` is true and `-InputObject` is already bound, even when the pipeline turns out to be empty.

**Decided on 2026-10-04:**

- **Expansion:** `-InputObject` supplies the same elements that piping the same value would, so `-InputObject $s` searches the set's elements. `LanguagePrimitives.GetEnumerable` matched the pipeline for every value tried, in both editions.
- **Strings and dictionaries:** a `[string]` or a dictionary passed to `-InputObject` stays one element, as it does in the pipeline, and the cmdlet writes a warning, not an error. A string isn't truly enumerable, and a dictionary such as a hashtable enumerates in a pseudo-random order.
- **Both inputs:** this is a bug, not a decision. The cmdlet throws a terminating error and writes no result, so a `-1` never reaches the output. An error thrown from the begin block ends the statement before PowerShell binds any piped object, so it's the only error, without the `InputObjectNotBound` errors. That was measured with an advanced function in both editions.

### 40 — Condition script blocks hide their errors by default

`-ScriptBlockErrorAction` defaults to `SilentlyContinue` on Assert-AnyObject, Assert-AllObject, Find-IndexOf, and Find-LastIndexOf. The README documents this.

```powershell
'missing-1.txt' | Assert-AnyObject { (Get-Item -LiteralPath $_).Length -gt 0 }    # False, with no error
1 | Assert-AnyObject { if ($_) { throw 'boom' } }                                # False, with no error
```

**Decision made:** - when the `ErrorActionPreference` value would suppress any exceptions (non & terminating), the exception's message should be written as a warning with `Write-Warning`/`this.WriteWarning()`.

### 41 — Command, alias, and class names

- **Assert vs. Test:** Assert-AnyObject and Assert-AllObject return a `[bool]` and don't throw when the answer is `$false`. `Test-*` is PowerShell's usual verb for that. `Get-Verb` describes Assert as "Affirms the state of a resource" and Test as "Verifies the operation or consistency of a resource".
- **Aliases:** `All-Object`, `All-Objects`, and `Any-Object` read like commands with unapproved verbs, and only All has plural forms.
- **Class names:** the C# class names show up in error IDs, such as `...,ListFunctions.Cmdlets.Finds.FindIndexCmdlet`. Three of them don't match their cmdlets' names:
  - `AssertAllObjectsCmdlet` is Assert-AllObject.
  - `FindIndexCmdlet` is Find-IndexOf.
  - `FindLastIndexCmdlet` is Find-LastIndexOf.

  Renaming a class after 4.0.0 changes those IDs.

**Decision made** - The cmdlets and PowerShell module cmdlet/functions will be renamed to `Test-*` while keeping the `Assert-*` variants as aliases. The `.cs` files will be renamed as well to their appropriate substituted cmdlet name (e.g. - `Assert-AnyObject` will become `Test-AnyObject` and its compiled `.cs` file will be renamed `TestAnyObjectCmdlet.cs`).

## Robustness

### 42 — Every cmdlet reads private members of PSObject, with no fallback

**Where:** `PSObjectExtensions.GetBaseObject` (`src/engine/ListFunctions.Engine/Extensions/PSObjectExtensions.cs:119`) reads private members of `PSObject`:

- **PowerShell 7:** through `UnsafeAccessor`, the field `_immediateBaseObject` and the property getter `get_ImmediateBaseObjectIsEmpty`.
- **Windows PowerShell 5.1:** through reflection, the fields `immediateBaseObject` and `immediateBaseObjectIsEmpty`.

Every cmdlet calls it for every input object (`src/engine/ListFunctions-Next/Cmdlets/ListFunctionCmdletBase.cs:479`), and ConvertTo-Dictionary also calls it for every selector result. If a PowerShell release renames or removes those members, every cmdlet fails on its first input object: PowerShell 7 throws `MissingFieldException` or `MissingMethodException`, and Windows PowerShell 5.1 throws `TypeInitializationException`. This finding comes from reading the code; there's no repro.

**Fix idea:** Use the public `PSObject.ImmediateBaseObject`, and test whether it's a `PSCustomObject` instead of reading the private flag.

- **Already measured:** on 2026-10-03, that test gave the same answer as the private flag in both editions, for every case tried: `[pscustomobject]`, `New-Object PSObject`, a PSObject that wraps a PSCustomObject, AutomationNull, and ordinary values.
- **Not checked:** deserialized objects.
- **A model to follow:** the stop-upstream code in `ListFunctionCmdletBase` already falls back when PowerShell lacks what it needs.

### 43 — The Windows PowerShell 5.1 assembly resolver answers for every module

**Where:** `src/engine/ListFunctions-NETFramework/ModuleInitializer.cs:51`. The handler:

- answers every failed assembly load in the session, from any module.
- matches by simple name only and ignores the requested version.
- loads the file with `Assembly.LoadFile`.
- stays registered after `Remove-Module`, as its own XML docs say.

Besides Engine, the `net48` output carries seven dependencies:

- `Microsoft.Bcl.Memory`
- `System.Buffers`
- `System.Collections.Immutable`
- `System.Memory`
- `System.Numerics.Vectors`
- `System.Runtime.CompilerServices.Unsafe`
- `ZLinq`

Other modules often ship the `System.*` ones. When one of those modules fails to load its own copy, this handler can hand it ListFunctions' copy, whatever version it asked for. This finding comes from reading the code; it wasn't reproduced.

Two of the dependencies barely earn their place:

- **`System.Collections.Immutable`:** used in one file, `src/engine/ListFunctions.Engine/Modern/Variables/PSComparingVariable.cs`.
- **`Microsoft.Bcl.Memory`:** serves one `span[^1]`, in `src/engine/ListFunctions.Engine/Validation/ValidateScriptVariableAttribute.cs:342`.

**Fix idea:**

- Answer only when `ResolveEventArgs.RequestingAssembly` is one of the module's own assemblies, and handle the case where it's `null`.
- Drop the two little-used dependencies from the `netstandard2.0` build.

### 44 — Compatibility shims are public types in other projects' namespaces

**Where:**

- **`System.Management.Automation.ValidateNotNullOrWhiteSpaceAttribute`:** `src/engine/ListFunctions.Engine/Validation/ValidateNotNullOrWhitespaceAttribute.cs:13`.
- **`System.Collections.Generic.IReadOnlySet<T>` and `System.Collections.ObjectModel.ReadOnlySet<T>`:** `src/engine/ListFunctions.Engine/Internal/ReadOnlySet.cs:19` and `:109`.
- **`ZLinq.SetExtensions`:** `src/engine/ListFunctions.Engine/Extensions/SetExtensions.cs:10`, a dead public type that sits in ZLinq's namespace in the `net10.0` build.

The first three are compiled only into the `netstandard2.0` build. The `net10.0` build forwards them to the real types.

```powershell
# Windows PowerShell 5.1
[System.Collections.Generic.IReadOnlySet[int]]             # Unable to find type
$null = New-List
[System.Collections.Generic.IReadOnlySet[int]].Assembly.GetName().Name                        # ListFunctions.Engine
[System.Management.Automation.ValidateNotNullOrWhiteSpaceAttribute].Assembly.GetName().Name   # ListFunctions.Engine
```

How the shims affect users:

- **Their own scripts:** after a ListFunctions cmdlet has run, a user's own `[System.Management.Automation.ValidateNotNullOrWhiteSpace()]` works. It gets the shim's behavior, which differs from PowerShell 7's for collections.
- **Other modules:** when another module ships the same shim, whichever loads first wins, with no error.

**Fix idea:**

- Make the shims internal. Attributes don't need to be public to work on cmdlet parameters; `ArgumentToTypeTransformAttribute` is already internal.
- Delete `ZLinq.SetExtensions`.

## Public surface and dead code

PowerShell users can name any public type in a loaded assembly, for example `[ListFunctions.Guard]`, so making a type internal after 4.0.0 is a breaking change.

### 45 — Engine and Next have more public types than they need

`GetExportedTypes()` counts:

- **Engine:** 50 public types in the `net10.0` build and 52 in the `netstandard2.0` build.
- **Next:** 15, and 16 in the NETFramework build, which adds `ModuleInitializer`.

About 7 to 17 of them need to be public, depending on the judgment calls below.

- **Must stay public:**
  - `DuplicateKeyBehavior`
  - the cmdlet classes and their base classes, `ListFunctionCmdletBase`, `AssertObjectCmdlet`, and `EqualityConstructingCmdlet<T>`
  - `ModuleInitializer`
- **Public only because a protected member uses them:**
  - `CmdletRunState` and `CmdletRunFlags`, through `EndCore`.
  - `ScriptBlockFilter`, through `AssertObjectCmdlet.Process`.
  - `EqualityCollectionCtor`, through `EqualityConstructingCmdlet<T>.GetConstructor`. That keeps `GenericCollectionCtor` and `CreateConstructingType` public too.

  They can become internal if those members become `private protected`.
- **Can be internal with no caveat:**
  - the extension classes `ExceptionExtensions`, `ObjectCloningExtensions`, `PSObjectExtensions`, `PSVariableCollectionExtensions`, `TypeExtensions`, and `DictionaryExtensions`, which is empty in the `net10.0` build
  - `Guard`, `ArraySlice`, `Empty`, `ListWrapper`, and `AddMethodInvoker`
  - the static `ComparingBlock` class
  - `ReflectionResolver`. Keep its methods `public`, because its static constructor finds them by name.
  - `IEqualityBlock` and the `*Ctor` classes
  - `PSComparingVariable`, `PSThisVariable`, `IsScriptBlockAttribute`, and `ValidateScriptVariableAttribute`
- **Judgment calls, because users meet these types at run time:**
  - the exceptions, which need to be public for `catch [T]` to work. The Pester tests use three of them as type literals.
  - `ObjectList` (see 35)
  - the comparers that users reach through a collection's `.Comparer` property: `EqualityBlock`, `HashBlock`, `ComparingBlock<T>`, and `EqualityComparerAdapter<T>`

### 46 — About 1,600 lines of code are dead or used only by tests

**Dead, with no reference outside their own files:**

- `EqualityExtensions`, which is public.
- `ZLinq.SetExtensions` (see 44).
- `IHashCodeBlock`. `IHashBlock`, in the same file, is used.
- `EqualityScriptException`, which nothing throws.
- `System.Collections.ObjectModel.ReadOnlySet<T>`. The `IReadOnlySet<T>` shim serves only dead code: `Empty.Set<T>()` and `SingleValueReadOnlySet<T>`.
- `PSVariableNameEquality`.
- `SingleValueReadOnlySet`, along with `DoubleBool` and `EnumerableExtensions`, which only it uses.
- `ArraySlice<T>.CtorArgs` and the constructor that takes it.
- `InternalFinder`, which is marked `[Obsolete]`, and the `AssemblyMetadata` lines in its file, which nothing reads.
- `src/engine/ListFunctions.Engine/Internal/VarList.cs`. It's excluded from compilation, it refers to types that don't exist, and Engine's csproj has two items only for it.

**Used only by tests:**

- `IComparingBlock`.
- `ListTransformAttribute`, which no parameter uses.
- `PipelineItem`, which only `ListTransformAttribute` creates.
- The public `ComparingBlock<T>` constructor.
- The `EqualityBlock(ScriptBlock, IHashBlock)` constructor.
- `EqualityComparerAdapter<T>.InnerComparer`.

Removing a type that only tests use means removing its tests too. Keep `ScriptBlockInvocationException.Offender` and `.Variables`: in code, only tests read them, but users see them in error records.

### 47 — Leftover members, unused extension points, and TODOs in the XML docs

**Members whose XML docs have TODOs:**

- `ComparingBlock<T>.CurrentLeft` and `CurrentRight` always return `default` (`src/engine/ListFunctions.Engine/Modern/ComparingBlock.cs:117`).
- Nothing assigns `PSComparingVariable.InstanceValue` or `EqualityBlock.ObjVariable.Value`.
- Nothing calls `HashBlock(ScriptBlock, List<PSVariable>?)`, and every call clears the list it's given (`src/engine/ListFunctions.Engine/Modern/HashBlock.cs:46`).
- The three `EqualityBlock` constructors accept a `$null` `IHashBlock`, which fails later with a NullReferenceException.
- The `addIfNull` parameter of `AddToCollection` does nothing (`src/engine/ListFunctions-Next/Cmdlets/Constructs/EqualityConstructingCmdlet.cs:311`).
- `ArraySlice<T>(T[], int, int)` doesn't validate its offset (`src/engine/ListFunctions.Engine/Internal/ArraySlice.cs:228`).

**Extension points nothing uses:**

- `CreateConstructingType` and the constructor parameters that take it. Every caller passes `null`.
- `EqualityConstructingCmdlet<T>.Begin` and `TryGetDynamicParameters`. No class overrides either one.
- `ListFunctionCmdletBase.GetErrorPreference()`. Nothing calls it.
- `CmdletRunState.Flags`, `IsStopping`, `HadError`, `BeginFailed`, and `ProcessFailed`. Nothing reads them.
- `SortingCollectorCtor.IsCaseSensitive`. Nothing sets it (see 37).

**Members nothing calls:**

- `Guard.NotNull(void*)`, `NotNullOrEmpty`, and `ThrowIfGreaterThanOrEqual`.
- `Empty.Set<T>()`.
- `PSVariableCollectionExtensions.GetLastValue`.
- `ComparingScriptException.FromBlockException<T>(Exception, ...)`.
- `PSThisVariable.Clone()`.
- `ListWrapper.Count`.
- `PipelineItem.AddToList`.
- `GenericCollectionCtor.GenericDefinitionType` and `HasGenerics`.
- `DictionaryCtor.ValueType`.
- `AddMethodInvoker.ImplementingType`.
- Most of `ArraySlice<T>`'s members.
- `ScriptBlockExtensions.TryInvokeWithContext<T>`.
- `NewDictionaryCmdlet.GetAddMethod` and `GetHashtableAddMethod`. The second throws "What the hell? That's not a method call..." (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewDictionaryCmdlet.cs:348`).

**Also:**

- **`ReadOnlySet<T>`:** its `ISet<T>.Add` throws `NotImplementedException`. The type is dead (46).
- **`HashCodeScriptException`:**
  - It's `[Serializable]` in every build, because the `#if` around the attribute is commented out (`src/engine/ListFunctions.Engine/Modern/Exceptions/HashCodeScriptException.cs:10`). Its sibling exceptions apply the attribute only before .NET 8.
  - In the `net10.0` build, it has no deserialization constructor.
- **`AssertObjectCmdlet.BeginCore`:** it has a commented-out block.
- **`[MemberNotNullWhen]`:** it's on a property that isn't a `bool` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/EqualityConstructingCmdlet.cs:55`).

## Can wait

Fixing these after 4.0.0 doesn't break anyone.

### 48 — `[ValidateScriptVariable]` rejects script blocks that have a `param()` block

**Where:** `src/engine/ListFunctions.Engine/Validation/ValidateScriptVariableAttribute.cs:95`.

```powershell
1, 2, 3 | Assert-AnyObject { param($n) $n -gt 2 }
# Cannot validate argument on parameter 'Condition'. At least one of the following variables must be included in
# the script block: $_, $this, $psitem
5, 3, 1 | New-SortedSet [int] -ComparingScript { param($a, $b) $a.CompareTo($b) }    # Rejected the same way
```

- **Both commands would work:** since the fix for `bugs.md` item 05, a script block with a `param()` block receives the elements as its parameters.
- **Function calls are rejected too:** the check also rejects a script block that calls a function that reads `$_`.
- **The message is incomplete:** it leaves out `$args[0]` and `$args[1]`, which the README lists.

**Fix idea:** Accept a script block whose `param()` block declares a parameter for each element, and list `$args[...]` in the message.

### 49 — New-Dictionary's `-InputObject` takes only a hashtable

**Where:** `src/engine/ListFunctions-Next/Cmdlets/Constructs/NewDictionaryCmdlet.cs:124`. PowerShell converts any other dictionary to a `Hashtable` before the cmdlet sees it, so the entries arrive in hash order.

```powershell
(New-Dictionary [string] [int] -InputObject ([ordered]@{ z = 1; y = 2; x = 3; w = 4; v = 5 })).Keys -join ','
# Expected: z,y,x,w,v. Actual: x,y,v,w,z in Windows PowerShell 5.1, and a different order in each PowerShell 7 process.
```

**Fix idea:** Type the parameter as `IDictionary`, and copy the entries in the order they're enumerated.

### 50 — There's no completion for type names and no help content

- **No completer for type parameters:** `-GenericType`, `-KeyType`, and `-ValueType` have no argument completer. `TabExpansion2 -inputScript 'New-List -GenericType Sys' -cursorColumn 25` finds 0 matches, and with nothing typed, completion offers file names.
- **No help files:** the repo has no `*-help.xml`, so `Get-Help` shows only the syntax. The manifest's `HelpInfoURI` points at the GitHub issues page.

**Fix idea:** Add an `ArgumentCompleter` that completes type names. Generate MAML help from the cmdlets' XML docs or from the README.

### 51 — The legacy script implementation is still in the repo

**Where:**

- `src/public/` (10 files) and `src/private/` (4 files).
- `.build/build.ps1` and `.debug/debug.ps1`, which refer to paths that no longer exist (see `CLAUDE.md`).

`Remove-All` and `Remove-At` exist only in the legacy scripts. The manifest's `Tags` still include `Remove` and `Modify`, which `bugs.md` item 15 covers.

**Decision needed:** whether to port `Remove-All` and `Remove-At` to cmdlets or drop them. Then delete the legacy files.

## Outside this list

Found in the same review, but not about the module's code:

- **Edition name:** `CompatiblePSEditions` in `ListFunctions/ListFunctions.psd1:18` is `@('Desk', 'Core')`. The edition is named `Desktop`, and `Test-ModuleManifest` doesn't catch the mistake. `bugs.md` item 15 doesn't list it.
- **A stale paragraph in `CLAUDE.md`:** its Code style section says most C# doesn't follow `.editorconfig` yet. On 2026-10-04, no `.cs` file in Engine, Next, or NETFramework had CRLF line endings or 4-space indentation. The only file with block-scoped namespaces was `src/engine/ListFunctions.Engine/Internal/ReadOnlySet.cs`, which needs them because it declares types in two namespaces.
