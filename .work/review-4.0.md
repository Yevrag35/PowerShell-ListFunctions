# ListFunctions 4.0 review list

Found in a code review on 2026-10-04, after every item in `bugs.md` except the release item, 15, was fixed. At that point the build had no warnings and every test passed: 195 Pester tests in each edition and 364 Engine tests. None of these items shows up as a failing test.

Each item is code or behavior that's wrong, inconsistent, or expensive to change after 4.0.0 ships. Most fixes change behavior that users can see, so they'd be breaking changes after the release. The items under **Can wait** are the exception.

Item numbers continue from `bugs.md`, so each number names one item in either file. Packaging, meaning the manifest and the DLLs shipped under `ListFunctions/`, stays in `bugs.md` item 15. When you fix an item, check it off and add a **Fixed:** note under it that says what changed.

## Checklist

**Wrong results**

- [x] 23 — Collections with script comparers break in other runspaces
- [x] 24 — An `[object]` sorted set has no consistent order when its elements' types differ
- [x] 25 — `-CaseSensitive` switches to a culture-sensitive comparison
- [x] 26 — ConvertTo-Dictionary throws a NullReferenceException when no key is given
- [x] 27 — ConvertTo-Dictionary's `-ValueType` doesn't work without a value selector
- [x] 28 — ConvertTo-Dictionary silently ignores some arguments
- [x] 29 — Open generic `[OutputType]` types break member completion

**Decisions**

- [x] 30 — String comparison rules differ between cmdlets and element types
- [x] 31 — ConvertTo-Dictionary converts every key and value to the first object's types
- [x] 32 — New-Dictionary drops entries whose value is `$null`
- [x] 33 — A failing comparison script has a different effect in each collection cmdlet
- [x] 34 — Script-block parameters reject bad input in different ways
- [x] 35 — The output type depends on the input
- [x] 36 — New-HashSet can't combine `-GenericType` with script equality
- [x] 37 — Parameter names, aliases, and positions differ between cmdlets
- [x] 38 — Each cmdlet handles `$null` input differently
- [ ] 39 — `-InputObject` gives wrong answers in two cases
- [ ] 40 — Condition script blocks hide their errors by default
- [ ] 41 — Command, alias, and class names

**Robustness**

- [ ] 42 — Every cmdlet reads private members of PSObject, with no fallback
- [x] 43 — The Windows PowerShell 5.1 assembly resolver answers for every module
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

- **Has merit:** it pins down behavior that users rely on and that a later change could plausibly break, such as the comparison rule chosen for 30. A test also has merit when it records a decision that the code doesn't make obvious.
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

**Decided on 2026-10-04:** this isn't a problem, and nothing changes. The collections themselves were never meant to be thread-safe, so the script comparers don't need to be either. Use from another runspace or thread isn't supported, and the fix idea is dropped: no runspace check, no change to the shared state, and no README note. The XML docs of `EqualityBlock`, `HashBlock`, and `PSThisVariable` already say that their instances aren't thread-safe.

The decision covers read-only use too. A plain `HashSet[string]` gives right answers when several runspaces only read it, as the control above showed, but a set with script comparers doesn't, because the comparer runs on every read.

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

The `[object]` HashSet has the same asymmetry in `LanguagePrimitives.Equals`, but it hashes each element's string form, which hides most of it (see 30).

**Decided on 2026-10-04:** without `-ComparingScript`, New-SortedSet sorts only element types that have a consistent default order, and `[string]` replaces `[object]` as the default element type. Mixed types don't need an order of their own anymore, because every element is converted to the one element type.

- **The check:** without `-ComparingScript`, the element type must implement `IComparable<T>` for itself, be an enum, or be `Nullable[U]` for a `U` that passes. `Comparer<T>.Default` orders each of these. Enums such as `[ConsoleColor]` and `[DayOfWeek]` implement only the non-generic `IComparable`, and `Nullable[int]` implements neither, but both sort correctly today. Any other type, `[object]` and `[psobject]` included, is a terminating error from the begin block, so no input is read.
- **The default:** without `-ComparingScript` and `-GenericType`, the element type is `[string]`.
- **With `-ComparingScript`:** neither applies. The script block does the comparing, so any element type is accepted, `[object]` and `[psobject]` included, and the default stays `[object]`. The README's `$people | New-SortedSet -ComparingScript { $x.Age.CompareTo($y.Age) }` (`README.md:301`) keeps working. With a `[string]` default, `$people | New-SortedSet -ComparingScript { $x.Id - $y.Id }` would turn both people into strings, whose `Id` is `$null`, and keep only one of them.
- **Where:** the check and the default depend on both parameters, so they belong in `NewSortedSetCmdlet.BeginCore`, not in a validation attribute. Today `GenericType` defaults through `field ??= typeof(object)` and `[PSDefaultValue(Value = typeof(object))]` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewSortedSetCmdlet.cs:45`).
- **What becomes dead:** `ObjectComparer`, `ConstructDefault`, and the `[object]` branch of `GetComparer` in `SortingCollectorCtor`. Only a set of `[object]` without a comparer uses them.
- **Related items:**
  - **30:** `[string]` elements sort with `OrdinalIgnoreCase`, so that's the default sort order. New-SortedSet still has no `-CaseSensitive` (37).
  - **29:** the closed `[OutputType]` for New-SortedSet is `SortedSet[string]`, or `SortedSet[object]` with `-ComparingScript`.
  - **README:** the description at `README.md:275` and the `-GenericType` row at `README.md:307`.

What changes for users, measured on 2026-10-04 in both editions:

```powershell
5, 3, 10 | New-SortedSet    # 3, 5, 10 today. As [string]: 10, 3, 5
$people | New-SortedSet     # As [string]: '@{Id=1; Name=Ann}' and '@{Id=2; Name=Bob}', with no error
# Today: a set that holds Ann, and the error 'Cannot compare "@{Id=2; Name=Bob}" to "@{Id=1; Name=Ann}" because the
# objects are not the same type or the object "@{Id=2; Name=Bob}" does not implement "IComparable".'
```

- **Numbers sort as strings** unless a type such as `[int]` or `[double]` is given.
- **Objects become their string forms** without an error.
- **Some types that sort today are rejected:**
  - `[Tuple[int, string]]`, which implements only the non-generic `IComparable`.
  - PowerShell classes. A class can implement `IComparable` but not `IComparable[T]` for itself: `class Bar : System.IComparable[Bar]` fails with "Unable to find type [System.IComparable[Bar]]." A class that implements `IComparable` sorts correctly in `New-SortedSet` today.

  Both can still use `-ComparingScript`.
- **`$null` elements are still skipped.** The cmdlet skips them before it converts anything (`NewSortedSetCmdlet.cs:130`), so the `[string]` default doesn't turn `$null` into `''`, and 38's `($null | New-SortedSet).Count` stays 0.

**Fixed:** as decided.

- **The check and the default:** `NewSortedSetCmdlet.BeginCore` resolves `GenericType`. With `-ComparingScript`, it defaults to `[object]`. Without it, it defaults to `[string]`, and the new `HasDefaultOrder` rejects a type that doesn't implement `IComparable[T]` of itself and isn't an enum or a `Nullable[U]` of such a type. The rejection is an `ArgumentException`, which the base class turns into a terminating error with the ID `System.ArgumentException,ListFunctions.Cmdlets.Constructs.NewSortedSetCmdlet` before any input is read. `GenericType` lost its `field ??= typeof(object)` getter, and its `[PSDefaultValue]` gives a `Help` text instead of a value.
- **Dead code:** `SortingCollectorCtor` lost `ObjectComparer` and the `[object]` branch of `GetComparer`. `ShouldConstructDefault` always returns `false`, and `ConstructDefault`, which `GenericCollectionCtor` requires, throws `NotSupportedException`.
- **README:** New-SortedSet's description, its script block section, and its `-GenericType` row.
- **Results, in both editions:** the repro gives `1,10,2,9` and `Contains('10') = True` for all three orders. The changes for users happen as measured above: `5, 3, 10 | New-SortedSet` gives `10, 3, 5`, and `$people | New-SortedSet` holds both people's string forms, without an error.
- **Left for other items:** `[string]` elements still sort with `InvariantCultureIgnoreCase` until 30, and New-SortedSet's open `[OutputType]` waits for 29.
- **Tests:** a new `Element type` context in `tests/New-SortedSet.Tests.ps1` covers the `[string]` default with the repro's three orders, the rejection of `[object]`, `[psobject]`, and `[Tuple[int, string]]` before any input is read, `[ConsoleColor]` and `[Nullable[int]]`, which pass the check without implementing `IComparable[T]` of themselves, and the `[object]` default with `-ComparingScript`.

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

**Decided on 2026-10-04:** as the fix idea says, following 30's ordinal rule.

**Fixed:** `EqualityConstructingCmdlet.GetCustomEqualityComparer` and `DictionaryCtor.GetObjectKeyComparer` return `StringComparer.Ordinal` for `-CaseSensitive`, so the switch now changes only whether case matters. The XML docs that named `CurrentCulture`, in those two files and in `NewHashSetCmdlet` and `NewDictionaryCmdlet`, name `Ordinal`. Every repro gives 2 in both editions, and so do a `Hashtable` and a `Dictionary[object, int]` from New-Dictionary with `-CaseSensitive`. The README already calls `[string]` comparison ordinal, so it doesn't change.

- **Left for 30:** an `[object]` set with `-CaseSensitive` still compares with `LanguagePrimitives.Equals` and hashes with `InvariantCulture`. `(New-HashSet -InputObject $decomposed, $precomposed -CaseSensitive).Count` is still 1 in both editions, and so is the same set of `'ab'` and `$hyphenated`.
- **Tests:** `tests/New-HashSet.Tests.ps1` checks that a `[string]` set with `-CaseSensitive` keeps both of the repro's pairs apart. `DictionaryCtorTests.Construct_ComparesObjectKeysOrdinallyWhenCaseSensitive` checks the decomposed and precomposed `é` as `[object]` keys, with `[object]` and `[int]` values. New-Dictionary's `[string]` keys get their comparer from the same base method as New-HashSet's `[string]` elements, so they have no test of their own.

### 26 — ConvertTo-Dictionary throws a NullReferenceException when no key is given

**Where:** `src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:46`. The default parameter set, `None`, contains neither `-KeyPropertyName` nor `-KeySelector`, so `InferTypes` runs a `$null` key selector.

```powershell
$people | ConvertTo-Dictionary
# Expected: a binding error that names -KeyPropertyName or -KeySelector.
# Actual: Object reference not set to an instance of an object.
$people | ConvertTo-Dictionary -ValuePropertyName Name    # The same error
```

**Fix idea:** Remove the `None` set, or make a key parameter set the default, so that the binder asks for the key.

**Fixed:** the `None` set is gone, and `KeyProperty` is the default parameter set. New `KEY_PROPERTY` and `KEY_SCRIPT` constants name the two sets in the `[Cmdlet]` and `[Parameter]` attributes and in `BeginCore`, and `Get-Command -Syntax` lists only those two. Measured in both editions:

- **Without a key:** both repros prompt for `-KeyPropertyName` in a console. In a runspace without a host, they end the statement with a `MissingMandatoryParameter` error whose message names `KeyPropertyName`.
- **Positional binding doesn't change:** `ConvertTo-Dictionary Id Name` still binds `-KeyPropertyName`, and `ConvertTo-Dictionary { $_.Id }` still binds `-KeySelector`.
- **Docs:** the class's XML docs and the README's `-KeyPropertyName` row say that a key parameter is required.
- **Tests:** a new `Key parameters` context in `tests/ConvertTo-Dictionary.Tests.ps1` runs both repros through `Invoke-InNewRunspace`, whose runspace can't prompt. They have merit because 28 may split the value parameters into more parameter sets, and a set without a key could come back.

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

**Fixed:** `AddToDictionary` passes every value to `TryConvertItem`, the input object as well as a selector's output. Measured in both editions:

- **The repros:** `1, 2 | ConvertTo-Dictionary -KeySelector { $_ } -ValueType string` gives `1 = '1'` and `2 = '2'`. `1, '2' | ConvertTo-Dictionary -KeySelector { [int]$_ }` gives `1 = 1` and `2 = 2` with no error, and so does the same command with `-DuplicateKeyBehavior Skip`. `AddSkip` still doesn't catch the dictionary's `ArgumentException`, but a converted value always fits the value type, so it no longer gets one.
- **A value that can't be converted** gets the error that New-List writes, as a selected value already did. `'a', 'x' | ConvertTo-Dictionary -KeySelector { $_ } -ValueType int` writes two `LFInvalidCastException` errors and gives an empty `Dictionary[string, int]`.
- **`-InputObject` now takes command output.** It passes the elements of an array as they are, and `Get-Item` and `Get-ChildItem` wrap each item in a `PSObject`, which the dictionary rejected. `ConvertTo-Dictionary -InputObject (Get-Item $env:windir) Name` gave an empty `Dictionary[string, DirectoryInfo]` and an error for every object. The conversion unwraps each one, so the dictionary holds the folder.
- **Unchanged:** `$people | ConvertTo-Dictionary Id` still stores the whole custom objects. `Get-Item "$env:windir", "$env:windir\notepad.exe" | ConvertTo-Dictionary Name` writes a conversion error from `FileInfo` to `DirectoryInfo` for notepad.exe, instead of the dictionary's own error, until 31.
- **Docs:** the XML docs of the class, `ValueType`, and `AddToDictionary`, and the README, say that every value is converted, including an input object that is its own value.
- **Tests:** `tests/ConvertTo-Dictionary.Tests.ps1` checks the first repro, and that two folders from `Get-Item` passed to `-InputObject` are both stored without an error. The second repro depends on the `[int]` value type inferred from the first object, which 31 removes, so it has no test.

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

**Fixed:** the cmdlet now uses or rejects each argument in the first, second, and fourth repros. The third is left for 31. Measured in both editions:

- **`-ValuePropertyName`:** a new transformation attribute, `[StringOrScriptBlockTransform]` in `src/engine/ListFunctions.Engine/Validation/`, unwraps the argument from its `PSObject` and passes on `$null`, a string, or a script block. `($people | ConvertTo-Dictionary Id $name)[1]` is `Ann`, and a line from `Get-Content` and a script block wrapped in a `PSObject` work too. Any other argument, such as `5` or `Name, Id`, fails to bind with a `ParameterArgumentTransformationError`: "The argument must be a string or a script block, not a value of type 'System.Int32'." `$null`, `''`, and white space still mean that each object is its own value.
- **`-KeyPropertyName`:** a new transformation attribute, `[RejectScriptBlock(nameof(KeySelector))]`, sees the argument before PowerShell converts it to a string, and rejects a script block. `$people | ConvertTo-Dictionary -Key { $_.Id }` fails to bind with "The parameter doesn't take a script block. Pass the script block to -KeySelector instead." The attribute can't treat the script block as `-KeySelector`, because it can only change the argument, and PowerShell still converts the result to a string. Other arguments still go to that conversion, so `ConvertTo-Dictionary 2024 Id` selects a property named `2024`. `-ValuePropertyName` rejects the number `2024`, as the fix idea says, and `'2024'` works for both.
- **Positional binding doesn't change:** `ConvertTo-Dictionary { $_.Id }` and `ConvertTo-Dictionary { $_.Id } Name` still bind `-KeySelector`. That depends on the attribute's exception, an `ArgumentTransformationMetadataException` that holds a `PSInvalidCastException`. PowerShell treats it as a failed conversion, which doesn't end positional binding, so the script block moves on to `-KeySelector`. In a test cmdlet with the same parameter sets, an `ArgumentTransformationMetadataException` without the inner exception, or an `ArgumentException`, ended the binding at `-KeyPropertyName`, and `ConvertTo-Dictionary { $_.Id }` failed. `[StringOrScriptBlockTransform]` throws the same way as `[RejectScriptBlock]`.
- **`-ValuePropertyName` with `-ValueSelector`:** `BeginCore` throws an `ArgumentException` when `-ValuePropertyName` gives a property name or a script block and `-ValueSelector` isn't `$null`. That's a terminating error in the `InvalidArgument` category, before any input is read. The message names both parameters, and says that a second positional argument binds to `-ValuePropertyName`. `$null` or `''` with `-ValueSelector` still works, so a function that passes its own parameters on can pass the one it wasn't given. The check is in `BeginCore`, not in parameter sets, so `Get-Command -Syntax` still lists two sets.
- **Left for 31:** `($people | ConvertTo-Dictionary Nope).Count` is still 0, with no warning. 31's decision replaces the warning idea with a non-terminating error for each `$null` key.
- **Docs:** the XML docs of the class, `KeyPropertyName`, `ValuePropertyName`, `ValueSelector`, and `BeginCore`, and the README's parameter rows.
- **Tests:** `tests/ConvertTo-Dictionary.Tests.ps1` checks:
  - that a script block passed by position binds to `-KeySelector`, which guards the inner `PSInvalidCastException`.
  - that `-KeyPropertyName` and `-Key` reject a script block.
  - that a property name and a script block wrapped in a `PSObject` select the values.
  - that `5` and an array of names are rejected.
  - that both shapes of the `-ValueSelector` conflict are rejected before any input is read.
  - that `$null` and `''` work with `-ValueSelector`.

  Against commit `984e386`, before 27 and 28, the 10 new tests for the two items' fixes fail, and the 3 that guard unchanged behavior pass. The two attributes have no Engine tests, because what they do matters only inside PowerShell's binder, which the Pester tests go through.

### 29 — Open generic `[OutputType]` types break member completion

**Where:** `[OutputType(typeof(List<>))]` in `src/engine/ListFunctions-Next/Cmdlets/Constructs/NewListCmdlet.cs:24`, and the same pattern in `NewHashSetCmdlet.cs:32`, `NewSortedSetCmdlet.cs:27`, and `NewDictionaryCmdlet.cs:34`. ConvertTo-Dictionary has no `[OutputType]`.

```powershell
TabExpansion2 -inputScript '$l = New-List; $l.Ad' -cursorColumn 20
# Exception calling "CompleteInput" with "3" argument(s): "startIndex cannot be larger than length of string."
```

- **The other sets fail the same way:** `(New-List).Ad`, `(New-HashSet).Ad`, and `(New-SortedSet).Ad`.
- **New-Dictionary completes,** because it also lists `Hashtable`.
- **A closed type completes:** a function with `[OutputType([System.Collections.Generic.List[object]])]` completes `Add(` and `AddRange(`.

**Fixed** using `[object]`.

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

**Decided on 2026-10-04:** every comparison that the module chooses is ordinal.

- **Strings** compare with `StringComparer.OrdinalIgnoreCase`, or with `StringComparer.Ordinal` under `-CaseSensitive` (25).
- **`[object]` elements and keys** follow the `Hashtable`'s rule everywhere: two strings compare as strings, with `OrdinalIgnoreCase`, and any other two values with their own `Equals` and `GetHashCode`.

Where it changes the code:

- **`-CaseSensitive`:** `StringComparer.CurrentCulture` becomes `StringComparer.Ordinal` in `EqualityConstructingCmdlet.GetCustomEqualityComparer` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/EqualityConstructingCmdlet.cs:349`) and in `DictionaryCtor.GetObjectKeyComparer` (`src/engine/ListFunctions.Engine/Modern/Constructors/DictionaryCtor.cs:104`). That's 25's fix.
- **New-SortedSet `[string]`:** `InvariantCultureIgnoreCase` and `InvariantCulture` become `OrdinalIgnoreCase` and `Ordinal` (`src/engine/ListFunctions.Engine/Modern/Constructors/SortingCollectorCtor.cs:96`). After 24, this is New-SortedSet's default order.
- **New-HashSet `[object]`:** `ObjectEqualityComparer`, which calls `LanguagePrimitives.Equals` and hashes each element's string form (`src/engine/ListFunctions.Engine/Modern/Constructors/HashSetCtor.cs:106`), gives way to the comparer that `DictionaryCtor` gives `[object]` keys.
- **ConvertTo-Dictionary:** its `[string]` keys already use `OrdinalIgnoreCase`. Its `[object]` keys follow the `Hashtable`'s rule too (see 31).
- **Engine's default for `[string]`:** `EqualityCollectionCtor.GetDefaultComparer` returns `InvariantCultureIgnoreCase` or `InvariantCulture` (`src/engine/ListFunctions.Engine/Modern/Constructors/EqualityCollectionCtor.cs:180`). The cmdlets pass their own comparer for `[string]`, so users don't reach it, but it should match.
- **README:** the `[object]` row at `README.md:202`, which says "Like PowerShell's `-eq` operator", and New-SortedSet's description at `README.md:275`.
- **Not element comparisons:** the variable names in `ScriptBlockInvocationException` (`src/engine/ListFunctions.Engine/Modern/Exceptions/ScriptBlockInvocationException.cs:262`) and the dead `PSVariableNameEquality` (46) use `InvariantCultureIgnoreCase`. Users don't see those comparisons, so the decision doesn't need them, but they can switch for consistency.

What changes for users, measured on 2026-10-04 in both editions:

```powershell
(New-HashSet -InputObject 1, '1').Count        # 1 today. 2 under the Hashtable's rule
(New-HashSet -InputObject 1, [long]1).Count    # 1 today. 2
(New-HashSet -InputObject 1, 1.0).Count        # 1 today. 2
(New-HashSet -InputObject 'a', 'A').Count      # 1, unchanged
```

- **`[object]` sets stop converting between types.** `1`, `'1'`, `[long]1`, and `1.0` are four elements, as they're four keys in `@{}`. So an `[int]` and a `[long]` with the same value, such as a literal `0` and a file's `Length`, aren't duplicates. Equality and hashing follow the same rule, so `1, '01'` is no longer two elements that `-eq` calls equal.
- **The sort order of strings changes.** `OrdinalIgnoreCase` compares the strings as if they were uppercased, by code point. Punctuation such as `_` sorts after the letters, and every letter outside ASCII sorts after `Z`:

  ```text
  Today, and Sort-Object:  _x, 10, 9, a, Äpfel, apple, b, co-op, coop, éclair, Z, Zebra, zoo
  OrdinalIgnoreCase:       10, 9, a, apple, b, co-op, coop, Z, Zebra, zoo, _x, Äpfel, éclair
  ```

  Today's order also differs between editions: `co-op` sorts before `coop` in PowerShell 7 and after it in Windows PowerShell 5.1. The ordinal order is the same in both.
- **`OrdinalIgnoreCase` still differs between editions for a few letters outside ASCII.** It treats final sigma `ς` and `Σ`, and `ǅ` and `Ǆ`, as equal in PowerShell 7 and as different in Windows PowerShell 5.1. Case-sensitive `Ordinal` has no such differences. The culture rule differs between editions too: `InvariantCultureIgnoreCase` treats `ß` and `SS` as equal only in 5.1.

**Fixed:** as decided, together with 31, 32, and 35.

- **Engine's default comparer:** `EqualityCollectionCtor.GetDefaultComparer` returns `StringComparer.OrdinalIgnoreCase`, or `Ordinal` with `IsCaseSensitive`, for `[object]` as well as `[string]`. For `[object]`, `GetComparerOrDefault` wraps it in an `EqualityComparerAdapter[object]`. A `StringComparer` compares two strings as strings and any other two values with their own `Equals` and `GetHashCode`, which is the rule that `DictionaryCtor` gave `[object]` keys since `bugs.md` item 20. Since `DictionaryCtor`'s override and the new default were the same, the override is gone, and the method is private and no longer virtual.
- **New-HashSet `[object]`:** `HashSetCtor` lost `ObjectEqualityComparer` and the fallback set it was created for, so a set of `[object]` is created like a set of any other type, with the adapted comparer.
- **New-SortedSet `[string]`:** `SortingCollectorCtor.GetComparer` returns `OrdinalIgnoreCase`, or `Ordinal` with `IsCaseSensitive`.
- **Variable names:** `ScriptBlockInvocationException` and the dead `PSVariableNameEquality` compare them with `OrdinalIgnoreCase`, which is how PowerShell compares variable names.
- **Results, in both editions:** the repro gives 2 for every line, including `strasse` and `straße` in Windows PowerShell 5.1. `1` with `'1'`, `[long]1`, or `1.0` gives 2 elements, and `'a'` with `'A'` gives 1. The review's list of words sorts in the `OrdinalIgnoreCase` order above, and 25's leftovers, the `[object]` sets of the decomposed and precomposed `é` with `-CaseSensitive`, and of `'ab'` and `$hyphenated`, hold 2 elements each.
- **Docs:** the XML docs of `EqualityCollectionCtor`, `HashSetCtor`, `DictionaryCtor`, `SortingCollectorCtor.GetComparer`, `NewHashSetCmdlet`, and `NewSortedSetCmdlet`, and in the README, New-HashSet's `[object]` row and New-SortedSet's description, which has a new example of the order. The docs state the rule instead of comparing it to `@{}`: measured on 2026-10-05, PowerShell 7.6's hashtable literals compare string keys with `OrdinalIgnoreCase`, and Windows PowerShell 5.1's with `CurrentCultureIgnoreCase`.
- **Tests:**
  - `tests/New-HashSet.Tests.ps1` has a new `Object elements` context, for `1` with `'1'`, `[long]1`, and `1.0`, the soft hyphen pair, `'a'` and `'A'`, and the two `é`s with `-CaseSensitive`. `tests/New-SortedSet.Tests.ps1` has a new `String order` context, for the order of `'b', '_x', 'a', 'é', 'Z'` and for `'a'` and `'A'`.
  - In Engine, `HashSetCtorTests.Construct_CreatesAnObjectSetThatComparesLikeTheEqOperator` became `Construct_CreatesAnObjectSetThatComparesOnlyStringsAsStrings`. The new `Construct_ComparesStringsInAnObjectSetOrdinally` checks the soft hyphen and `é` pairs with and without `IsCaseSensitive`.
  - **Pester 6.2's `Should-BeCollection` ignores order.** It passes for `3, 1, 2` against `1, 2, 3` in both editions, and so does `Should-BeEquivalent`. The order test compares the joined elements instead, and it fails against commit `94a9c5b`. The older New-SortedSet tests that expected an order with `Should-BeCollection`, the two `Bug05` tests and the three order tests that 24 added, didn't check it. They compare the joined elements now too. With every expected order reversed, all nine order cases in the file fail in both editions, and the other twelve pass. The `lf-testing` skill describes the trap.

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
- **`[object]` keys follow the `Hashtable`'s rule** when `-KeyComparer` isn't given, as New-Dictionary's do (`bugs.md` item 20): strings compare with `OrdinalIgnoreCase`, and other keys with their own `Equals` (see 30). Without that rule, `$people | ConvertTo-Dictionary Name` would become case-sensitive, because today the cmdlet compares only `[string]` keys without regard to case.
- **`-KeyComparer` fits any key type.** ConvertTo-Dictionary builds its dictionary through `DictionaryCtor`, as New-Dictionary does, instead of calling `Activator.CreateInstance` itself (`src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:317`). `EqualityCollectionCtor.AdaptComparer` then wraps a comparer that isn't an `IEqualityComparer[TKey]` in an `EqualityComparerAdapter[TKey]`. Measured on 2026-10-04 in both editions:
  - **Why it's needed:** a `StringComparer` is an `IEqualityComparer[string]` but not an `IEqualityComparer[object]`, so `Dictionary[object, object]`'s constructor rejects it. `$people | ConvertTo-Dictionary Name -KeyComparer ([StringComparer]::Ordinal)` works today only because the keys are inferred as `[string]`. With `[object]` keys and no adapter, it would fail the way 37's `Id` example does.
  - **What the adapter does:** it calls the comparer's non-generic `IEqualityComparer` methods. A `StringComparer` compares two strings as strings and any other two keys with `Equals`, so with `Ordinal`, `Ann`, `ann`, `1`, and `'1'` are four keys.
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

**Fixed:** as decided, together with 35, which this item needs.

- **Types:** the new `-KeyType` parameter has `[ArgumentToTypeTransform]` and no position, and it and `-ValueType` default to `[object]`. `BeginCore` creates the dictionary through `DictionaryCtor` before any input is read. `InferTypes`, `GetInferredType`, `FindFirstObject`, `CreateDictionary`'s `ListFunctionsException`, and the fields that kept the first object's selector outputs are gone. A dictionary that can't be created, such as one with a pointer type, is a terminating error from `BeginCore`, as in New-Dictionary. With `Concatenate`, the values are still `[object]`, and `BeginCore` writes the warning about `-ValueType`, even when there's no input.
- **Key comparison:** without `-KeyComparer`, `[object]` keys get `OrdinalIgnoreCase` from `DictionaryCtor` (see 30), so `Ann` and `ann` are still one key. `-KeyComparer` works with any key type, because `EqualityCollectionCtor.AdaptComparer` wraps a comparer that isn't an `IEqualityComparer[TKey]`.
- **`$null` keys:** an object whose key is `$null`, or converts to `$null` as `[NullString]::Value` does for `[string]`, writes a non-terminating error and is skipped, and its value selector doesn't run. A `$null` key isn't converted first, so `-KeyType ([int])` doesn't turn it into `0`. The error wraps an `ArgumentNullException`, its ID is `System.ArgumentNullException,ListFunctions.Cmdlets.Constructs.ConvertToDictionaryCmdlet`, its category is `InvalidData`, and its target is the input object. The message says where the key came from, such as "Cannot add the object to the dictionary, because it has no 'Nope' property, or the property's value is $null. A dictionary key can't be $null.", so 28's `($people | ConvertTo-Dictionary Nope).Count` is still 0, but with an error for each person. A `$null` input object is still skipped without an error.
- **An object that is its own value** is unwrapped from its `PSObject`, unless it's a custom object, before it's converted. Measured on 2026-10-05 in both editions, `LanguagePrimitives.ConvertTo` keeps the wrapper of a `DirectoryInfo` for `[object]`, while `$d.Add('k', (Get-Item $env:windir))` stores the `DirectoryInfo`. With the unwrapping, `ConvertTo-Dictionary -InputObject (Get-Item ...) Name` stores `DirectoryInfo` and `FileInfo` objects too.
- **Results, in both editions:** in the repros, `['b']` is `2.5`, and the keys are `1, 1.6, 2` without an error. `$a` and `$b` each give one entry and two errors, one for the `$null` key and one for the duplicate `'a'`. The skipped `$null` key no longer decides a type, so `['a']` is the `[int]` `1` in a `Dictionary[object, object]`, and the `-KeyComparer` repro works. The `$null` value stays `$null` in either order. The Windows folder and notepad.exe are both stored, without an error. 37's `$people | ConvertTo-Dictionary Id -KeyComparer ([System.StringComparer]::Ordinal)` works too, and so does the same command with `-KeyType ([int])`.
- **Docs:** the XML docs of the class, `InputObject`, `KeyComparer`, `KeyType`, `ValueType`, `BeginCore`, `CreateDictionary`, `AddToDictionary`, `WriteNullKeyError`, and `EndCore`. In the README, ConvertTo-Dictionary's description, examples, and parameter rows. The examples use the indexer, and the description says that dot notation doesn't read `[object]` keys.
- **Tests:** in `tests/ConvertTo-Dictionary.Tests.ps1`:
  - New `Key and value types`, `Key comparison`, and `Null keys` contexts, for the repro's `2.5` without `-KeyType` and `-ValueType`, both parameters, the empty dictionary when there's no input, `Ann` and `ann`, `1` and `'1'`, `-KeyComparer` with `[object]` and `[int]` keys, and the `$null` key error from `-KeyPropertyName` and `-KeySelector`.
  - The tests that expected inferred types now pass `-KeyType` or `-ValueType`, or no longer check the type. The test of inference past a `$null` first key is gone, because the `Null keys` context covers that key. The `Bug06` test that inferred the key type past a `$null` input object now checks that the object is skipped without an error. The test of `-InputObject` with `Get-Item` passes `-ValueType ([System.IO.DirectoryInfo])`, so that the conversion still has to unwrap.
  - Against commit `94a9c5b`, 10 cases of the new and changed tests fail in each edition. The others pass, because they check behavior that didn't change, such as `Ann` and `ann`.
- **Related items:** 37's `-KeyType` is done, and the rest of 37 is open. 38's ConvertTo-Dictionary bullet now holds, but the README doesn't give the reasons that 38 asks for.

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

**Fixed:** as decided.

- **The conversion:** `NewDictionaryCmdlet.Process` converts a `$null` value to `-ValueType` like any other value. As before, nothing is converted to `[object]`, so a `$null` stays `$null` there.
- **The skip:** `EqualityConstructingCmdlet.AddToCollection(T, object[], bool)` became `AddToCollection(T, object[])`, which calls `Add` even when an argument is `$null`, through `AddMethodInvoker.TryInvoke` with `addIfNull: true`. Its `addIfNull` parameter, which 47 lists as doing nothing, and its TODO are gone. New-Dictionary is the only caller. A key that converts to `$null`, such as a `[NullString]::Value` key with `[string]` keys, now gets the dictionary's `ArgumentNullException`, "Value cannot be null. (Parameter 'key')", as a non-terminating error instead of a silent skip.
- **Results, in both editions:** `(@{ a = $null; b = 1 } | New-Dictionary).Count` is 2, and the second repro holds `x = 0`. A `[string]` dictionary stores `''`, and a `[Nullable[int]]` dictionary `$null`. A `[datetime]` dictionary writes an `LFInvalidCastException` error for the entry and skips it.
- **Docs:** the XML docs of `InputObject`, `Process`, and `AddToCollection`, and the README's `-InputObject` row.
- **Tests:** `tests/New-Dictionary.Tests.ps1` checks that an entry whose value is `$null` is copied as `$null`, `''`, `0`, and `$null` for `[object]`, `[string]`, `[int]`, and `[Nullable[int]]` values. All four cases fail against commit `94a9c5b`.
- **Related items:** 38's New-Dictionary bullet now holds.

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

**Decided on 2026-10-04:** `Stop` ends the script, as it does for the condition cmdlets. New-HashSet, New-SortedSet, and New-Dictionary let errors from `-EqualityScript`, `-HashCodeScript`, and `-ComparingScript` reach PowerShell unchanged through `PassesThrough`, the rule that the condition cmdlets and ConvertTo-Dictionary's selectors have followed since `bugs.md` item 21. When one of these errors reaches PowerShell, the cmdlet writes no collection.

- **With the default `-ScriptBlockErrorAction Stop`:** an error that the script block writes ends the whole script, a failed method call ends only the statement, and `break` leaves the enclosing loop. A `throw` ends the whole script unless `-ScriptBlockErrorAction` is `SilentlyContinue`. Under `Continue`, an error that the script block writes doesn't stop it, and the comparison uses its output.
- **Output that a comparer can't use,** such as no output, `$null`, or a value that can't be converted to `[int]` from `-HashCodeScript` or `-ComparingScript`, ends the statement, as a failed method call does. The `HashCodeScriptException` and `ComparingScriptException` that report it are `RuntimeException`s, so `PassesThrough` passes them on too. Today New-SortedSet and New-Dictionary write an error for each element instead (`bugs.md` items 01 and 10).
- **Other failures in `Add`,** which don't come from a script block, such as a duplicate key in New-Dictionary or an element type whose own `GetHashCode` throws, still write a non-terminating error for their element. New-HashSet writes them that way too, instead of stopping and writing no set, so the three cmdlets match.

Where it changes the code:

- **`HashBlock`:** `GetHashObject` runs the script block through `TryInvokeWithContext`, which catches every exception, the `BreakException` from `break` included, and then wraps it in a `HashCodeScriptException` (`src/engine/ListFunctions.Engine/Modern/HashBlock.cs:109`). Passing that wrapper on isn't enough. Measured on 2026-10-04 in both editions, with a test cmdlet that runs a script block under `Stop` and rethrows its exception, either as it is or wrapped by `HashCodeScriptException.FromBlockException`:
  - **A `throw`** ends the script either way, because the wrapper copies `WasThrownFromThrowStatement`.
  - **An error that the script block writes** ends the script when it's rethrown as it is, but only the statement when it's wrapped. PowerShell ends the whole script for an `ActionPreferenceStopException`, not for an exception that wraps one.
  - **A failed method call** ends only the statement either way.

  A wrapped `BreakException` isn't a `FlowControlException` anymore, so it can't leave the loop either. Instead, `HashBlock` lets the script block's exceptions through unwrapped, as `EqualityBlock` and `ComparingBlock<T>` already do, and throws `HashCodeScriptException` only for output it can't use. That changes the `throw` case of Engine's `HashBlockTests.GetHashCode_ThrowsWhenTheScriptDoesNotReturnAHashCode`, and the XML docs of `IHashBlock.GetHashCode`, `HashBlock.GetHashCode`, and `EqualityBlock.GetHashCode`, which say that a failing script block becomes a `HashCodeScriptException`.
- **Adding elements:** `AddMethodInvoker.TryInvoke` catches every exception and returns it, and its callers write it as a non-terminating error: both `EqualityConstructingCmdlet.AddToCollection` overloads (`src/engine/ListFunctions-Next/Cmdlets/Constructs/EqualityConstructingCmdlet.cs:280` and `:316`) and `NewSortedSetCmdlet.ProcessCore` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewSortedSetCmdlet.cs:136`). They rethrow an exception that `PassesThrough` accepts, with `ExceptionDispatchInfo` so it keeps its stack trace, and `ListFunctionCmdletBase.ProcessRecord` passes it on. After 36, a typed New-HashSet with script equality adds through `AddToCollection` too.
- **New-HashSet:** the two `catch` blocks in `Process` (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewHashSetCmdlet.cs:168` and `:191`) let those exceptions through as well, and `Process` stops returning `false`. Nothing else returns `false` from `Process`, so the `wantsToStop` parameter of `End` becomes unused (see 47).
- **XML docs:** the remarks of `NewHashSetCmdlet.Process`, `NewSortedSetCmdlet.ProcessCore` and `ComparingScript`, `NewDictionaryCmdlet.Process`, and both `AddToCollection` overloads describe today's non-terminating errors, and New-HashSet's also describe the stop.
- **README:** the `Stop` row of the table at `README.md:436` says that errors in these script blocks "are terminating errors". The paragraphs from `README.md:450` describe what reaches PowerShell only for `-Condition`, and can describe every script block instead.

What changes for users. The results for today were measured on 2026-10-04 in both editions, with each command followed by `'still running'` and run in a new runspace. The results after the change are what the condition cmdlets and the test cmdlet give for the same errors.

```powershell
$hash = { $_.ToUpperInvariant().GetHashCode() }
'a', 'A', 'b' | New-HashSet -EqualityScript { if ($x -or $y) { Write-Error 'oops' }; $x -eq $y } -HashCodeScript $hash
# Today: one error from New-HashSet, no set, and the script goes on. After: Write-Error's error ends the script.
@{ a = 1; b = 2 } | New-Dictionary -EqualityScript { $x -eq $y } -HashCodeScript { if ($_) { Write-Error 'oops' }; 1 }
# Today: two HashCodeScriptException errors, an empty dictionary, and the script goes on. After: the first error ends
# the script.
foreach ($i in 1..2) { 5, 3 | New-SortedSet [int] -ComparingScript { if ($x -or $y) { break }; $x.CompareTo($y) } }
# Today: a BreakException error and a set that holds 5, in each pass. After: break leaves the loop.
5, 3, 1 | New-SortedSet [int] -ComparingScript { $null = $x, $y }
# Today: two ComparingScriptException errors, a set that holds 5, and the script goes on. After: one error that ends
# the statement, and no set.
```

- **Today, no failure ends the script or leaves a loop.** New-HashSet writes one error and no set. New-SortedSet and New-Dictionary write an error for each element that fails, and the collection without those elements. A `throw`, an error written under `Stop`, a failed method call, `break`, and output that a comparer can't use all behave that way. After the change, this item's repro ends at New-HashSet's `throw`, and `$null -eq $set` never runs.
- **The errors keep their own records.** Today the cmdlet writes each error itself, with the exception's type as the error ID, such as `System.Management.Automation.ActionPreferenceStopException,ListFunctions.Cmdlets.Constructs.NewHashSetCmdlet`, and every error from `-HashCodeScript` is a `HashCodeScriptException`. After the change, they're the errors that the conditions give: `Microsoft.PowerShell.Commands.WriteErrorException` for `Write-Error`, `boom` for `throw 'boom'`, and `InvokeMethodOnNull` with the cmdlet's class for `$null.Foo()`.
- **`-ErrorAction` doesn't reach them anymore.** Today they're non-terminating errors, which `-ErrorAction SilentlyContinue` hides and `-ErrorAction Stop` makes end the script. After the change, they end the statement or the script whatever `-ErrorAction` says, and a `try` block handles them, as it does for conditions.
- **Tests:** two tests collect these errors with `-ErrorAction SilentlyContinue`, and both change. They're `tests/New-HashSet.Tests.ps1:211`, whose `ThrowingHashCode` case then gets an error for each element and an empty set, and New-SortedSet's `Bug10` test (`tests/New-SortedSet.Tests.ps1:48`). Tests in the shape of the conditions' `Bug21` tests have merit for each cmdlet, and one for an error written in `-HashCodeScript` catches a wrapper that comes back.

**Fixed:** as decided.

- **`HashBlock`:** `GetHashObject` runs the script block through `InvokeWithContext` and doesn't catch its exceptions, as `EqualityBlock` and `ComparingBlock<T>` already did. A `throw`, an `ActionPreferenceStopException`, and a `BreakException` reach the caller as they are, and `HashCodeScriptException` reports only a `$null` object and output that isn't a hash code.
  - `ScriptBlockExtensions.TryInvokeWithContext`, whose only caller was `GetHashObject`, is gone. Its generic overload, which nothing calls, is left for 47.
- **Adding elements:** the new `ListFunctionCmdletBase.RethrowIfPassesThrough`, which is `private protected`, rethrows an exception that `PassesThrough` accepts, through `ExceptionDispatchInfo`. Both `EqualityConstructingCmdlet.AddToCollection` overloads and `NewSortedSetCmdlet.ProcessCore` call it for the exception that `AddMethodInvoker.TryInvoke` returns. Only an exception that doesn't pass through gets the non-terminating error.
  - **The single-element overload** lost its conversion delegate and its return value. It converts with `TryConvertItem`, which writes the error that `New-List` writes, so New-HashSet no longer wraps the call in a `catch` for `PSInvalidCastException`. That `catch` would also have caught a `PSInvalidCastException` from a script block, which passes through, and reported it as a failed conversion of the element.
- **New-HashSet:** `Process` adds an element of an `[object]` set in a `catch` with a `when (!PassesThrough(e))` filter, and an element of a typed set through `AddToCollection`. It always returns `true`. So nothing returns `false` from `EqualityConstructingCmdlet<T>.Process` anymore, and the `wantsToStop` parameter of `End` is always `false` (47).
- **Results, in both editions:** measured on 2026-10-05 against the Debug build, with each command followed by `'still running'` in a new runspace.
  - This item's repro ends at New-HashSet's `throw`, with the error `boom`, and nothing after it runs. New-SortedSet's command ends the script the same way.
  - The four commands under "What changes for users" give what the decision describes. The `Write-Error` in New-HashSet's `-EqualityScript` and the one in New-Dictionary's `-HashCodeScript` end the script, with the error ID `Microsoft.PowerShell.Commands.WriteErrorException`. `break` leaves the loop in its first pass. The `-ComparingScript` without output writes one `ComparingScriptException` error, which ends the statement, and no set.
  - A failed method call, such as `$null.Foo()`, ends the statement with the error ID `InvokeMethodOnNull,ListFunctions.Cmdlets.Constructs.<class>`. Output that a comparer can't use ends it with a `HashCodeScriptException` or a `ComparingScriptException` whose error ID is `RuntimeException,ListFunctions.Cmdlets.Constructs.<class>`. The command's own `-ErrorAction SilentlyContinue` doesn't hide these errors, and a `try` block catches them.
  - Under `-ScriptBlockErrorAction Continue`, `Write-Error` in `-HashCodeScript` writes its errors, and the set holds both elements. Under `SilentlyContinue`, a `throw` in `-EqualityScript` is suppressed, the elements compare as unequal, and the set holds both.
  - Failures that don't come from a script block are unchanged, except in New-HashSet. A duplicate key in New-Dictionary still writes its `ArgumentException` as a non-terminating error, and the dictionary is still written. A typed New-HashSet whose element type's own `GetHashCode` throws now writes an error for each piped element, and an empty set, instead of stopping at the first error without a set.
- **Docs:** the XML docs of `IHashBlock.GetHashCode`, `HashBlock`, its `GetHashCode` and `GetHashObject`, `EqualityBlock.GetHashCode`, both `AddToCollection` overloads, `NewHashSetCmdlet.Process`, `NewSortedSetCmdlet.ProcessCore` and `ComparingScript`, and `NewDictionaryCmdlet.Process`. The three cmdlets' class remarks describe their script blocks' errors, the way the condition cmdlets' remarks do. In the README:
  - The `Stop` row of the table in Errors in script blocks says that an error the script block writes ends the whole script.
  - The section's paragraphs describe every script block instead of only `-Condition`. They add what `Continue` does, that `-ErrorAction` doesn't change any of it, that output a comparer can't use ends the statement, and that the commands write no collection, with an example.
  - The `-ScriptBlockErrorAction` rows of the three commands link to the section.
- **Tests:** against commit `b563e31`, before these changes, 23 cases of the new and changed Pester tests fail in each edition, and so do all 6 cases of the new Engine test. The 2 Pester cases that pass check behavior that didn't change.
  - `tests/New-HashSet.Tests.ps1`: the test that expected a stop and no set is gone. Its `ThrowingHashCode` case joined the test of the errors that `-InputObject` gets, which now runs for piped input too and checks that an empty set is written. A new `Errors in script blocks` context has the `Bug21` shape for `-EqualityScript`, a case for an error that `-HashCodeScript` writes, and a test for output that isn't a hash code. That test passes `-ErrorAction SilentlyContinue`: one error and no set was also the old result, but the old error was non-terminating, and that switch hid it.
  - `tests/New-SortedSet.Tests.ps1`: the `Bug10` test checks that the output ends the statement with one `ComparingScriptException` error and no set. A new `Errors in ComparingScript` context has the `Bug21` shape. The `Bug14` test no longer passes `-ErrorAction SilentlyContinue`, which can't hide the error anymore.
  - `tests/New-Dictionary.Tests.ps1`: a new `Errors in script blocks` context has the `Bug21` shape, with its `Write-Error` and `throw` cases in `-HashCodeScript`.
  - Engine: the `throw` case of `HashBlockTests.GetHashCode_ThrowsWhenTheScriptDoesNotReturnAHashCode` moved to the new `GetHashCode_LetsTheExceptionsOfTheScriptThrough`. It checks that a `throw`, a `Write-Error` under `Stop`, and `break` throw exactly `RuntimeException`, `ActionPreferenceStopException`, and `BreakException`.

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

**Decided on 2026-10-05:** `-Condition $null` is the same as leaving `-Condition` out, which is how PowerShell treats `$null` for a parameter.

- **Assert-AnyObject:** its `-Condition` is optional, so `$null` tests whether any element isn't `$null`, as it does now.
- **Assert-AllObject, Find-IndexOf, and Find-LastIndexOf:** their `-Condition` is mandatory, so PowerShell rejects `$null` when it binds the parameter, with "Cannot bind argument to parameter 'Condition' because it is null." Only Assert-AllObject changes. Its run-time error appeared only when an element reached the condition, so `@() | Assert-AllObject -Condition $null` gave `$true`.
- **The other script block parameters already follow the rule:** a mandatory one, such as `-EqualityScript` or `-KeySelector`, rejects `$null`, and ConvertTo-Dictionary's optional `-ValueSelector` treats it as no value selector (28).

**Fixed:** as the fix idea says, with the decision above.

- **One shape check:** `[IsScriptBlock]` is on every parameter that takes a script block. New-HashSet's two already had it. It's new on New-Dictionary's `-EqualityScript` and `-HashCodeScript`, New-SortedSet's `-ComparingScript`, the `-Condition` of the four condition cmdlets, and ConvertTo-Dictionary's `-KeySelector`, `-ValueSelector`, and `-ValuePropertyName`. On `-ValuePropertyName`, which takes a property name too, it checks only a script block, after `[StringOrScriptBlockTransform]` unwraps it.
- **One message:** `IsScriptBlockAttribute` and `ComparingBase`'s constructor check share the new `ScriptBlockExtensions.ImproperScriptBlockMessage`, which states the rule: "The script block must contain at least one statement, and it can't have a begin block, a clean block, or both a process block and an end block." The `netstandard2.0` build leaves out the clean block, which Windows PowerShell 5.1 doesn't have. The message replaces "block is not a proper script block." and "scriptBlock is not a script block."
- **`-Condition $null`:** Assert-AllObject's `-Condition` lost `[PSAllowNull]` and `[AllowEmptyString]`.
  - **`[AllowEmptyString]` never applied.** PowerShell doesn't convert a string to a script block, so `-Condition ''` gives a `ParameterArgumentTransformationError` with or without it, measured on 2026-10-05 in both editions. Assert-AnyObject keeps its own.
  - **`ProcessWhenNoCondition`** still throws its `ArgumentException`, but binding no longer lets a `$null` condition reach it.
  - **`AssertObjectCmdlet.HasCondition`** is `Condition is not null`. A script block that's empty or only white space fails validation now, so the check for one had nothing left to catch.
- **Results, in both editions:** measured on 2026-10-05 against the Debug build.
  - This item's four shape repros end the statement with a `ParameterArgumentValidationError` for their parameter, with the new message, before any input is read. So does the same `begin`/`process` script block passed to Assert-AllObject, Find-IndexOf, Find-LastIndexOf, and ConvertTo-Dictionary's three parameters. `$people | ConvertTo-Dictionary Id Name` still works.
  - `1 | Assert-AllObject -Condition $null` and `@() | Assert-AllObject -Condition $null` give the `ParameterArgumentValidationErrorNullNotAllowed` error that `1 | Find-IndexOf -Condition $null` gives. `1, $null | Assert-AnyObject -Condition $null` is `True`, and `$null | Assert-AnyObject -Condition $null` is `False`.
- **Docs:** the XML docs of `IsScriptBlockAttribute`, of every parameter that got the attribute, of `AssertObjectCmdlet.Condition`, `HasCondition`, `Process`, and `ProcessWhenNoCondition`, and of `AssertAllObjectsCmdlet` and its `ProcessWhenNoCondition`. In the README, the Script blocks section states the shape rule and the `$null` rule, and Assert-AnyObject's `-Condition` row says that `$null` is the same as no condition.
- **Tests:** all fail against commit `b563e31`, except Assert-AnyObject's, which records behavior that didn't change.
  - `tests/New-Dictionary.Tests.ps1`: the test that rejects a `begin` block expects `ParameterArgumentValidationError`, as New-HashSet's does, instead of `System.ArgumentException`.
  - `tests/New-SortedSet.Tests.ps1`: a new test checks that `-ComparingScript` rejects a `begin` block. It has merit because the command accepted one before, and wrote an error for each comparison and a set without those elements.
  - `tests/Assert-AnyObject.Tests.ps1` and `tests/Assert-AllObject.Tests.ps1`: a new `Condition` context records the decision: `$null` as no condition for Assert-AnyObject, and the binding error for Assert-AllObject, with the empty input that used to give `$true`.
  - The shape check on the conditions and on ConvertTo-Dictionary has no test. A run-time error became a binding error there, so a test would mostly restate the fix.
- **Not changed:**
  - **A function's script block:** `${function:Test-It}` and `(Get-Command Test-It).ScriptBlock` have a `FunctionDefinitionAst`, which `IsProperScriptBlock` rejects, although `InvokeWithContext` runs them. `[ValidateScriptVariable]` already rejected them on every parameter, because it doesn't search the function's body, measured on 2026-10-05 in both editions. So the attribute stops nothing that worked. Accepting them belongs with 48.
  - **`-ValuePropertyName`'s variables:** a script block passed to it still isn't checked for `$_`, as `-ValueSelector`'s is. `[ValidateScriptVariable]` can't go on that parameter, because it would parse a property name as a script block.

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

**Decided on 2026-10-04:** ConvertTo-Dictionary and New-Dictionary always write a `Dictionary[TKey, TValue]`, whatever their input. Neither cmdlet writes a `Hashtable`.

- **New-Dictionary:** its default `[object]` keys and values give a `Dictionary[object, object]`. The keys keep the `Hashtable`'s comparison, which `DictionaryCtor` already gives `[object]` keys for any other value type: strings compare without regard to case unless `-CaseSensitive` is given (see 25 and 30), and other keys with their own `Equals`. The `Hashtable` comes from `DictionaryCtor.ShouldConstructDefault` and `ConstructTDefault` (`src/engine/ListFunctions.Engine/Modern/Constructors/DictionaryCtor.cs:139` and `:64`).
- **ConvertTo-Dictionary:** with no input, it writes the empty dictionary that it created before the first input object, typed by `-KeyType` and `-ValueType` (see 31), instead of the `Hashtable` at `src/engine/ListFunctions-Next/Cmdlets/Constructs/ConvertToDictionaryCmdlet.cs:429`. Once `DictionaryCtor` stops creating a `Hashtable`, ConvertTo-Dictionary can build its dictionary with it without getting one back, which 31's `-KeyComparer` decision needs.
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

**Fixed:** as decided, together with 31, which needs this item's change to `DictionaryCtor`.

- **`DictionaryCtor`:** it derives from `EqualityCollectionCtor` instead of `EqualityCollectionCtor<Hashtable>`, and lost `ShouldConstructDefault`, `ConstructTDefault`, and `GetObjectKeyComparer`, so it always creates a `Dictionary[TKey, TValue]`. A comparer passed for `[object]` keys, which the `Hashtable` ignored unless it was an `IEqualityBlock`, is now used.
- **No more fallback collections:** with `HashSetCtor`'s fallback set gone too (see 30), no `EqualityCollectionCtor` has one. `EqualityCollectionCtor` seals `ShouldConstructDefault` to return `false` and `ConstructDefault` to throw `NotSupportedException`, as `SortingCollectorCtor` does since 24. Its abstract `ConstructDefault(IEqualityComparer)`, its virtual `ShouldConstructDefault(IEqualityComparer, Type[])`, and the `EqualityCollectionCtor<TDefault>` class are gone.
- **New-Dictionary:** its `[OutputType]` lists only `Dictionary[object, object]`, and `GetGenericTypes` always returns both types, instead of `$null` when both are `[object]`. With no types, the dictionary is a `Dictionary[object, object]`, with or without `-InputObject` and `-CaseSensitive`.
- **ConvertTo-Dictionary:** see 31. With no input, it writes the empty dictionary that `BeginCore` created.
- **Results, in both editions:** the first three lines of the repro give ``Dictionary`2``. `TabExpansion2 -inputScript '$l = New-Dictionary; $l.Ad' -cursorColumn 26` completes `Add(`. Measured on 2026-10-05 with `New-Dictionary`, what changes for users happens as described above: `$d.Ann` is `$null`, `$d.Bob = 2` fails, `$d -is [hashtable]` is `$false`, `$d + @{ q = 1 }` is a `Hashtable`, `$d['missing']` is `$null` under `Set-StrictMode -Version Latest`, and `z`, `y`, `x`, `w`, `v` enumerate in that order.
- **Unchanged:** with `-DuplicateKeyBehavior Concatenate`, a key with one value still holds the value itself, and a key with more holds an `ObjectList`, so the repro's last two lines still give `System.String` and `ListFunctions.Modern.ObjectList`.
- **Docs:** the XML docs of `DictionaryCtor`, `EqualityCollectionCtor`, and `NewDictionaryCmdlet`, and in the README, the command table and New-Dictionary's section. Its examples use the indexer, and the section says that dot notation doesn't read or set `[object]` keys.
- **Tests:** `tests/New-Dictionary.Tests.ps1` has a new `Dictionary type` context, which checks the type with no input and with a piped hashtable, and the `Capacity` case for the `Hashtable` is now one for `Dictionary[object, object]`. In Engine, `DictionaryCtorTests.Construct_PassesTheCapacityToTheDictionary` expects a `Dictionary<object, object>`, and the new `Construct_UsesAStringComparerForObjectKeys` checks that `StringComparer.Ordinal` keeps `"a"` and `"A"`, and `1` and `"1"`, apart. Both fail against commit `94a9c5b`.
- **Left for other items:** `ReflectionResolver.GetAddMethod`, which is public, still handles a `Hashtable`, which no cmdlet creates anymore (see 45). `GenericCollectionCtor` still has the fallback hooks that `EqualityCollectionCtor` and `SortingCollectorCtor` now stub out (see 47).

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

**Fixed:** as the fix idea says. `GenericType` has a third `[Parameter]`, at position 0 in `WithCustomEquality`. The fix for `bugs.md` item 03 kept it out of that set before 09 added `EqualityComparerAdapter<T>`, which a set of a value type needs to use an `EqualityBlock`.

- **Results, in both editions:** measured on 2026-10-05 against the Debug build.
  - The repro's first line gives a `HashSet[int]` whose comparer is an `EqualityComparerAdapter[int]` around the `EqualityBlock`, as New-Dictionary's `Dictionary[int, object]` has. `-GenericType` by name, and the `-Type` alias with `[Nullable[int]]`, work too. A `[string]` set gets the `EqualityBlock` itself.
  - The cmdlet converts each element to the element type before it adds it, so the script blocks receive values of that type. `1, 11, 2, '21' | New-HashSet -GenericType ([int]) -EqualityScript { $x % 10 -eq $y % 10 } -HashCodeScript { $_ % 10 }` holds 1 and 2.
  - `Get-Command New-HashSet -Syntax` lists `[[-GenericType] <type>]` in the syntax with the script blocks, as 3.1.0's did.
- **Unchanged:** `New-HashSet [int]` without script blocks still gets the `SpecifiedType` set and `EqualityComparer<int>.Default`. `-CaseSensitive` still can't be combined with the script blocks: `New-HashSet [string] -CaseSensitive -EqualityScript { $x -eq $y } -HashCodeScript { $_.GetHashCode() }` gives "Parameter 'CaseSensitive' cannot be specified in parameter set 'WithCustomEquality'."
- **Docs:** the XML docs of `GenericType`. In the README, New-HashSet's script block section says that `T` can be any type and has an `[int]` example, and the `-GenericType` row no longer says that the parameter can't be used with the script blocks.
- **Tests:** two new tests in the `Script block equality` context of `tests/New-HashSet.Tests.ps1` pipe elements into sets of `[int]` and `[Nullable[int]]`, and of `[string]`, which is also offered `-CaseSensitive`. All 3 cases fail against commit `df27654` in both editions, with the repro's binding error. They have merit because the decision reverses 03's, and a later change to the parameter sets, such as 37's, could undo it. Engine gets no test: `HashSetCtor` adapts a comparer with the code it shares with `DictionaryCtor`, which the `Bug09` tests in `DictionaryCtorTests` cover.

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

**Decided on 2026-10-04:** ConvertTo-Dictionary gets `-KeyType`, and stops inferring its key and value types (see 31). For the rest, New-List's `-Capacity` loses its position and its default of 4, the missing aliases and New-SortedSet's `-CaseSensitive` are added, and nothing is removed. Checked against the `v3.1.0` tag, everything this item lists shipped in 3.1.0, except the `ScriptErrorAction` alias on Assert-AllObject and Find-LastIndexOf. So the `-Capacity` change breaks 3.1.0 scripts, not only unreleased code.

- **`-Capacity`:** New-List's is named-only and defaults to 0, as on New-HashSet and New-Dictionary.
  - **What goes:** `Position = 1`, `PSDefaultValue(Value = 4)`, and the rule in `BeginCore` that turns 0 into 4 (`src/engine/ListFunctions-Next/Cmdlets/Constructs/NewListCmdlet.cs:101`). `ListWrapper.CreateTyped` already leaves the capacity at 0 when it's given 0.
  - **What users see:** `New-List [int] 5`, which sets the capacity in 3.1.0, fails with a binding error, because no parameter takes a second positional argument. So does `New-List [int] 1, 2, 3`. A new list's `Capacity` is 0 instead of 4, as it is for `[System.Collections.Generic.List[int]]::new()`.
  - **No positional `-InputObject` yet:** position 1 stays free, so one can be added later without a break. Adding it now would make 3.1.0's `New-List [int] 5` build a list that holds 5, with no error.
- **Aliases:** the missing ones are added where they fit, and none is removed.
  - `FilterScript` on the `-Condition` of Find-IndexOf and Find-LastIndexOf, as on Assert-AnyObject and Assert-AllObject.
  - `ScriptErrorAction` on the `-ScriptBlockErrorAction` of New-HashSet, New-SortedSet, and New-Dictionary, as on the four condition cmdlets.
  - `Size` on New-HashSet's `-Capacity`, as on New-List and New-Dictionary.
  - `List` and `CopyFrom` stay on their own cmdlets, because each names what that cmdlet's `-InputObject` is: the list that Find-IndexOf and Find-LastIndexOf search, and the hashtable that New-Dictionary copies.
- **New-SortedSet's parameter set:** the set of `-ComparingScript` is renamed `WithComparingScript`, because it has nothing to do with equality. The other cmdlets keep `WITH_CUSTOM_EQUALITY` (`src/engine/ListFunctions-Next/Cmdlets/ListFunctionCmdletBase.cs:35`). No parameter, alias, or position changes.
- **ConvertTo-Dictionary's selectors:** the cmdlet doesn't get `-ScriptBlockErrorAction`. `-KeySelector` and `-ValueSelector` keep running under the caller's `$ErrorActionPreference`, as `ForEach-Object`'s script blocks do, and the README already says so (`README.md:457`). The parameter can be added later without a break, as long as leaving it out still means the caller's preference.
- **String comparison:** New-SortedSet gets `-CaseSensitive`, and no cmdlet gets any other comparison parameter.
  - **What it does:** it sets `SortingCollectorCtor.IsCaseSensitive`, so `[string]` elements sort with `StringComparer.Ordinal` instead of `OrdinalIgnoreCase` (see 30). `'b', 'a', 'B', 'A' | New-SortedSet -CaseSensitive` holds all four, in the order `A, B, a, b`, which the README's example gets from `-ComparingScript { [string]::CompareOrdinal($x, $y) }` today (`README.md:291`).
  - **When it's available:** it's a dynamic parameter like New-HashSet's. It appears only for `[string]` elements, which after 24 include the default, and never with `-ComparingScript`. New-SortedSet doesn't derive from `EqualityConstructingCmdlet<T>`, whose `TryGetDynamicCaseParam` is private, so it can't reuse that method as it is.
  - **Item 47:** `SortingCollectorCtor.IsCaseSensitive` stays.
  - **Comparer objects** stay ConvertTo-Dictionary's alone. It doesn't get `-CaseSensitive`, because `-KeyComparer ([StringComparer]::Ordinal)` does the same. A comparer parameter on New-HashSet, New-SortedSet, or New-Dictionary can be added later without a break.
- **README:**
  - New-List's `-Capacity` row (`README.md:190`) says "Position 1" and "Default: `4`".
  - The new aliases go in the `-Condition` row of Find-IndexOf (`README.md:147`), which Find-LastIndexOf shares, the `-Capacity` row of New-HashSet (`README.md:264`), and the `-ScriptBlockErrorAction` rows of New-HashSet (`README.md:269`), New-SortedSet (`README.md:310`), and New-Dictionary (`README.md:359`).
  - New-SortedSet's table (`README.md:305`) needs a `-CaseSensitive` row, and its description (`README.md:275`) says that a `[string]` set holds only one of `'a'` and `'A'`.
- **Tests:** New-SortedSet's `-CaseSensitive` has merit for a test, because nothing else pins down that `'a'` and `'A'` stay apart, or their ordinal order. The aliases, the set name, and `-Capacity` need none. No test passes New-List's `-Capacity` by position.

**Fixed:** as decided. `-KeyType` came with 31. Measured on 2026-10-06 against the Debug build, in both editions:

- **`-Capacity`:** New-List's has no position, and `[PSDefaultValue(Value = 0)]` replaces `Value = 4`. `BeginCore` passes the capacity on as it is. `New-List [int] 5` and `New-List [int] 1, 2, 3` fail with `PositionalParameterNotFound`, and `(New-List).Capacity` is 0, as `[System.Collections.Generic.List[int]]::new().Capacity` is.
- **Aliases:** `FilterScript` on the `-Condition` of Find-IndexOf and Find-LastIndexOf, `ScriptErrorAction` on the `-ScriptBlockErrorAction` of New-HashSet, New-SortedSet, and New-Dictionary, and `Size` on New-HashSet's `-Capacity`. Each one binds.
- **New-SortedSet's parameter sets:** `-ComparingScript` and `-ScriptBlockErrorAction` are in `WithComparingScript`, named by the new private constant `WITH_COMPARING_SCRIPT`. The default set keeps its name, `None`, through the new constant `DEFAULT_ORDER`.
- **New-SortedSet's `-CaseSensitive`:** `NewSortedSetCmdlet` implements `IDynamicParameters`. `GetDynamicParameters` offers the switch, in the default set only, when `-GenericType` is `[string]` or isn't given. `BeginCore` passes it to `SortingCollectorCtor.IsCaseSensitive`. `CASE_SENSE` moved from `EqualityConstructingCmdlet<T>` to `ListFunctionCmdletBase`, as `private protected`, so that both cmdlets use it.
  - `'b', 'a', 'B', 'A' | New-SortedSet -CaseSensitive` holds `A, B, a, b`, and so does `New-SortedSet [string] -CaseSensitive -InputObject 'b', 'a', 'B', 'A'`. `-CaseSensitive:$false` keeps the default order, `a, b`.
  - `New-SortedSet [int] -CaseSensitive` and `New-SortedSet ([object]) -CaseSensitive` fail with `NamedParameterNotFound`. With `-ComparingScript`, in either order, the switch fails with `ParameterNotInParameterSet`: "Parameter 'CaseSensitive' cannot be specified in parameter set 'WithComparingScript'."
  - **The switch before a positional type:** PowerShell takes a positional argument that follows an unknown parameter for that parameter's value, so it asks for the dynamic parameters before it binds the type. `New-SortedSet -CaseSensitive [int]` was offered the switch, and gave a `SortedSet[int]` that ignored it. `BeginCore` rejects that case with an `ArgumentException`, a terminating error whose ID is `System.ArgumentException,ListFunctions.Cmdlets.Constructs.NewSortedSetCmdlet`: 'Cannot sort elements of type "System.Int32" with -CaseSensitive, because the switch applies only to [string] elements.'
- **Not changed:** New-HashSet and New-Dictionary have the same gap. `New-HashSet -CaseSensitive [int]` gives a `HashSet[int]`, and `New-Dictionary -CaseSensitive [int]` a `Dictionary[int, object]`, without an error, while the same commands with the switch after `[int]` fail with `NamedParameterNotFound`. The same check in `EqualityConstructingCmdlet<T>.BeginCore` would close it.
- **Docs:** the XML docs of New-List's `Capacity`, of `NewSortedSetCmdlet` and its `GetDynamicParameters` and `BeginCore`, and of `ListFunctionCmdletBase.WITH_CUSTOM_EQUALITY`, which now describes only equality script blocks. In the README: New-List's `-Capacity` row, Find-IndexOf's `-Condition` row, New-HashSet's `-Capacity` row, the three `-ScriptBlockErrorAction` rows, and New-SortedSet's description, examples, and table, which has a `-CaseSensitive` row. The example that sorted with `-ComparingScript { [string]::CompareOrdinal($x, $y) }` now uses `-CaseSensitive`.
- **Tests:** a new `CaseSensitive` context in `tests/New-SortedSet.Tests.ps1` checks that `'a'` and `'A'` stay apart in ordinal order, with no element type and with `[string]`, and that the switch is rejected after `[int]`, before `[int]`, and with `-ComparingScript`. Against commit `d0637d3`, 4 of the 5 cases fail in each edition. The fifth, the switch after `[int]`, passes, because that error didn't change.

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

**Fixed:** as decided. 31 and 32 gave the dictionary cmdlets the rules above, so the code doesn't change, and the fix is the README.

- **README:** a new `$null` input section, under Input, gives each command's rule and the reason for it:
  - **The condition cmdlets** pass a `$null` element to `-Condition`, so the condition decides what it means, and an index counts it, so the index is the element's position in the input. Without a condition, Assert-AnyObject asks whether the input holds anything, so it doesn't count `$null`.
  - **New-List** skips `$null` unless `-IncludeNullElements` is given, because a list keeps every element, so `$null` elements, which usually stand for missing values, would pile up in it.
  - **New-HashSet** adds `$null` to an `[object]` set, which holds it once, as it is, so `$set.Contains($null)` tells whether the input had one. A set of any other type skips it, because most types would turn it into a value that wasn't in the input, such as `0` in an `[int]` set.
  - **New-SortedSet** skips `$null` for every element type. The default `[string]` set would turn it into `''`, and an `[object]` set would sort it first without running `-ComparingScript`.
  - **New-Dictionary** copies a hashtable, so a `$null` in its place is a parameter binding error. A `$null` value is copied, converted to `-ValueType`, because it may be intentional.
  - **ConvertTo-Dictionary** skips a `$null` input object, which has no key or value to select. A `$null` key writes an error, because a dictionary can't hold one, and the error names the objects without a key, such as when a property name is misspelled. A `$null` value is stored, because it may be intentional.

  The sections of New-List, New-HashSet, New-SortedSet, and ConvertTo-Dictionary link to it. New-HashSet's and New-SortedSet's sections now state their rules too, which they didn't before.
- **Results, in both editions:** measured on 2026-10-06 against the Debug build. The repro gives what the item shows, except that `$null | New-Dictionary` writes an empty `Dictionary[object, object]` since 35, with the same binding error. `New-Dictionary -InputObject $null` ends the statement with that error and writes nothing. Every claim in the new section holds: for example, `$s.Add($null)` on an `[object]` set from `New-SortedSet -ComparingScript { throw "ran $x $y" }` puts `$null` first without running the script block.
  - **The reasons cover the common types only.** New-HashSet and New-SortedSet also skip `$null` for `[Nullable[int]]` and `[version]`, in which it would stay `$null`. They skip an element that converts to `$null` too, such as `[NullString]::Value` in a `[string]` set.
  - **A variable that holds no output sends no `$null`.** After `$files = Get-ChildItem -Path $env:TEMP -Filter 'no-such-file-*.xyz'`, `$files | New-HashSet` gives an empty set, and `$files | ForEach-Object { 'ran' }` outputs nothing, because the variable holds `AutomationNull.Value`. Only a real `$null`, such as one in an array or one assigned to a variable, reaches the commands.
- **Tests:** two rules that the decision keeps had no test, and each got one. Both pass against commit `d0637d3`, because the behavior didn't change. Each of the other rules already had a test.
  - `tests/New-HashSet.Tests.ps1`: a typed set skips a piped `$null` without an error, for `[int]` and `[string]`. It's the other half of the existing test for an `[object]` set, which adds it.
  - `tests/New-Dictionary.Tests.ps1`: a piped `$null` gets a `ParameterArgumentValidationErrorNullNotAllowed` error, and the command still writes an empty dictionary. A later change that gives `-InputObject` `[AllowNull()]`, like the other commands' `-InputObject`, would skip it silently instead.

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

**Fixed 2026-10-05**

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
- The `addIfNull` parameter of `AddToCollection` does nothing (`src/engine/ListFunctions-Next/Cmdlets/Constructs/EqualityConstructingCmdlet.cs:311`). 32's fix removed the parameter and its TODO.
- `ArraySlice<T>(T[], int, int)` doesn't validate its offset (`src/engine/ListFunctions.Engine/Internal/ArraySlice.cs:228`).

**Extension points nothing uses:**

- `CreateConstructingType` and the constructor parameters that take it. Every caller passes `null`.
- `EqualityConstructingCmdlet<T>.Begin` and `TryGetDynamicParameters`. No class overrides either one.
- `ListFunctionCmdletBase.GetErrorPreference()`. Nothing calls it.
- `CmdletRunState.Flags`, `IsStopping`, `HadError`, `BeginFailed`, and `ProcessFailed`. Nothing reads them.
- `SortingCollectorCtor.IsCaseSensitive`. Since 37, New-SortedSet's `-CaseSensitive` sets it.
- The return value of `EqualityConstructingCmdlet<T>.Process`. Since 33, both derived classes always return `true`, so the `wantsToStop` parameter of `End` is always `false`.
- `GenericCollectionCtor.ShouldConstructDefault` and `ConstructDefault`, added on 2026-10-05. Since 30 and 35, no class creates a fallback collection: `EqualityCollectionCtor` and `SortingCollectorCtor` return `false` from the first and throw from the second.

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
- `ScriptBlockExtensions.TryInvokeWithContext<T>`. 33's fix removed the non-generic overload, whose only caller was `HashBlock`.
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
- **So is a function's own script block:** `${function:Test-It}` is rejected although its body reads `$_`, because the check doesn't search a function's body. Since 34, `[IsScriptBlock]` rejects it too, because its syntax tree is a `FunctionDefinitionAst`.
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

**Decision made** `Remove-All` and `Remove-At` will be removed.

## Outside this list

Found in the same review, but not about the module's code:

- **Edition name:** `CompatiblePSEditions` in `ListFunctions/ListFunctions.psd1:18` is `@('Desk', 'Core')`. The edition is named `Desktop`, and `Test-ModuleManifest` doesn't catch the mistake. `bugs.md` item 15 doesn't list it.
- **A stale paragraph in `CLAUDE.md`:** its Code style section says most C# doesn't follow `.editorconfig` yet. On 2026-10-04, no `.cs` file in Engine, Next, or NETFramework had CRLF line endings or 4-space indentation. The only file with block-scoped namespaces was `src/engine/ListFunctions.Engine/Internal/ReadOnlySet.cs`, which needs them because it declares types in two namespaces.
