# <img height="30px" src="./.icon/list-functions.png" alt="ListFunctions"></img> ListFunctions for PowerShell

[![version](https://img.shields.io/powershellgallery/v/ListFunctions.svg?include_prereleases)](https://www.powershellgallery.com/packages/ListFunctions)
[![downloads](https://img.shields.io/powershellgallery/dt/ListFunctions.svg?label=downloads)](https://www.powershellgallery.com/stats/packages/ListFunctions?groupby=Version)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/097d27365fac4fc69ac2c45570db85d6)](https://www.codacy.com/gh/Yevrag35/PowerShell-ListFunctions/dashboard?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=Yevrag35/PowerShell-ListFunctions&amp;utm_campaign=Badge_Grade)

ListFunctions is a PowerShell module for testing, searching, and building generic .NET collections: `List[T]`, `HashSet[T]`, `SortedSet[T]`, and `Dictionary[TKey, TValue]`. Its main feature is that equality comparers, hash code functions, and sort orders are ordinary PowerShell script blocks, so you don't have to write and compile an `IEqualityComparer[T]` or `IComparer[T]` class.

The name comes from `System.Collections.Generic.List[T]`. Several of the commands are modeled on its methods, such as `Exists`, `TrueForAll`, `FindIndex`, and `FindLastIndex`.

## Installation

```powershell
Install-Module -Name ListFunctions
```

ListFunctions runs on Windows PowerShell 5.1 (.NET Framework 4.8) and on PowerShell 7.6 or later. When you import it, the module loads the build for the edition you're running.

## Commands

| Command | Aliases | Output | Description |
| --- | --- | --- | --- |
| [Assert-AnyObject](#assert-anyobject) | `Any`, `Any-Object`, `Assert-Any` | `[bool]` | Tests whether any element satisfies a condition, or whether there are any elements at all. |
| [Assert-AllObject](#assert-allobject) | `All`, `All-Object`, `All-Objects`, `Assert-All`, `Assert-AllObjects` | `[bool]` | Tests whether every element satisfies a condition. |
| [Find-IndexOf](#find-indexof) | `IndexOf`, `Find-Index` | `[int]` | Finds the index of the first element that satisfies a condition. |
| [Find-LastIndexOf](#find-lastindexof) | `LastIndexOf`, `Find-LastIndex` | `[int]` | Finds the index of the last element that satisfies a condition. |
| [New-List](#new-list) | | `List[T]` | Creates a list. |
| [New-HashSet](#new-hashset) | | `HashSet[T]` | Creates a set of distinct elements, optionally with script block equality. |
| [New-SortedSet](#new-sortedset) | | `SortedSet[T]` | Creates a sorted set of distinct elements, optionally with a script block sort order. |
| [New-Dictionary](#new-dictionary) | | `Dictionary[TKey, TValue]` or `Hashtable` | Creates a dictionary, optionally with script block key equality. |
| [ConvertTo-Dictionary](#convertto-dictionary) | | `Dictionary[TKey, TValue]` | Indexes objects in a dictionary by a property or a computed key. |

## Input

Every command except `New-Dictionary` takes its elements from the pipeline or from `-InputObject`:

- Each object that comes through the pipeline is one element, even when it's `$null` or an array, the same as with `ForEach-Object`.
- An array or a list that you pass to `-InputObject` supplies its elements, and `$null` supplies none. Any other value, such as a string or a hashtable, is one element.

```powershell
1, $null, 3 | Find-IndexOf { $_ -eq 3 }              # 2
@(1, @(2, 3), 4) | Find-IndexOf { $_ -is [array] }   # 1
Find-IndexOf -InputObject 1, 2, 3 { $_ -eq 3 }       # 2
```

## Script blocks

Most commands take script blocks, which they run once for each element or once for each pair of elements they compare. The command sets variables that hold the elements:

| Script block | Parameters | Variables |
| --- | --- | --- |
| Receives one element | `-Condition`, `-HashCodeScript`, `-KeySelector`, `-ValueSelector` | `$_`, `$this`, `$PSItem`, or `$args[0]` |
| Compares two elements | `-EqualityScript`, `-ComparingScript` | `$x`, `$left`, or `$args[0]` for the first element, and `$y`, `$right`, or `$args[1]` for the second |

A script block has to use at least one of these variables for each element it receives. Otherwise, the command fails with a parameter validation error. A variable that appears only inside a nested script block doesn't count.

Only the first value that a script block outputs is used. The output of a condition or an equality script block is converted to `[bool]` by PowerShell's usual rules, so `0`, `''`, `$null`, and no output at all count as `$false`.

## Generic types

`-GenericType`, `-KeyType`, and `-ValueType` accept a type in any of these forms:

```powershell
New-List [guid]         # A type literal. PowerShell passes it as the string '[guid]'.
New-List '[guid]'       # The same string, quoted.
New-List ([guid])       # A System.Type object.
New-List { [guid] }     # A script block that contains a type literal.
```

A type literal that contains a comma, as some generic types do, works as it is. PowerShell splits the argument at each comma, and the command joins the parts back together.

```powershell
New-List [System.Collections.Generic.KeyValuePair[string, int]]
```

The parts have to make up a single type, so `New-Dictionary [string],[int]` fails. To pass a key type and a value type, separate them with a space: `New-Dictionary [string] [int]`.

## Assertions

### Assert-AnyObject

Aliases: `Any`, `Any-Object`, `Assert-Any`

Returns `$true` if at least one input element satisfies `-Condition`. Without a condition, it returns `$true` if the input has at least one element that isn't `$null`. After the first match, it stops: it doesn't test the remaining elements, and it [stops the commands that send it pipeline input](#stopping-early).

```powershell
$numbers = 1, 2, 3

$numbers | Any { $_ -gt 2 }     # True
$numbers | Any { $_ -gt 5 }     # False
$numbers | Any                  # True
@($null, $null) | Any           # False
@() | Any                       # False

if (Get-ChildItem -File | Any { $_.Length -gt 1GB }) {
    # ...at least one file is larger than 1 GB.
}
```

| Parameter | Description |
| --- | --- |
| `-Condition` | Position 0. Aliases: `ScriptBlock`, `FilterScript`. Optional. The test to run on each element. |
| `-InputObject` | The elements to test. Accepts pipeline input. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. The `$ErrorActionPreference` inside `-Condition`. Default: `SilentlyContinue`. See [Errors in script blocks](#errors-in-script-blocks). |

### Assert-AllObject

Aliases: `All`, `All-Object`, `All-Objects`, `Assert-All`, `Assert-AllObjects`

Returns `$true` if every input element satisfies `-Condition`, or if there are no elements, the same as `List[T].TrueForAll`. After the first element that fails, it stops: it doesn't test the remaining elements, and it [stops the commands that send it pipeline input](#stopping-early).

```powershell
1, 2, 3 | All { $_ -is [int] }          # True
1, 2, 'John' | All { $_ -is [int] }     # False
@() | All { $_ -is [int] }              # True

$array = 1, 2, 'John'
if (-not ($array | All { $_ -is [int] })) {
    # ...at least one element isn't an [int].
}
```

| Parameter | Description |
| --- | --- |
| `-Condition` | Position 0. Aliases: `ScriptBlock`, `FilterScript`. Required. The test to run on each element. |
| `-InputObject` | The elements to test. Accepts pipeline input. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. The `$ErrorActionPreference` inside `-Condition`. Default: `SilentlyContinue`. |

## Searching

### Find-IndexOf

Aliases: `IndexOf`, `Find-Index`

Returns the zero-based index of the first input element that satisfies `-Condition`, or `-1` if none does. After the first match, it stops: it doesn't test the remaining elements, and it [stops the commands that send it pipeline input](#stopping-early).

```powershell
$names = 'Ann', 'Bob', 'Cid', 'Bob'

$names | Find-IndexOf { $_ -eq 'Bob' }                          # 1
$names | IndexOf { $_ -like 'Z*' }                              # -1
Find-IndexOf -InputObject $names -Condition { $_ -like 'C*' }   # 2
```

| Parameter | Description |
| --- | --- |
| `-Condition` | Position 0. Alias: `ScriptBlock`. Required. The test to run on each element. |
| `-InputObject` | Alias: `List`. The elements to search. Accepts pipeline input. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. The `$ErrorActionPreference` inside `-Condition`. Default: `SilentlyContinue`. |

### Find-LastIndexOf

Aliases: `LastIndexOf`, `Find-LastIndex`

Returns the zero-based index of the last input element that satisfies `-Condition`, or `-1` if none does. It collects all of its input before it searches backward from the end.

```powershell
$names = 'Ann', 'Bob', 'Cid', 'Bob'

$names | Find-LastIndexOf { $_ -eq 'Bob' }     # 3
```

It takes the same parameters as [Find-IndexOf](#find-indexof).

## Building collections

Each of these commands outputs the collection it builds as a single object. When you assign the output to a variable, you get the collection itself, even if it's empty or holds only one element.

### New-List

Creates a `System.Collections.Generic.List[T]`. Input elements are converted to `T` as they're added. An element that can't be converted writes a non-terminating error and isn't added. `$null` elements are skipped unless you pass `-IncludeNullElements`.

```powershell
# A List[object]. Like an ArrayList, it holds elements of any type.
$list = New-List

# A List[guid] with an initial capacity of 10,000 elements.
$list = New-List [guid] -Capacity 10000

# A List[int], filled from the pipeline. The string '3' is converted to 3.
$list = 1, 2, '3' | New-List [int]

# The same list, filled through -InputObject.
$list = New-List [int] -InputObject 1, 2, '3'
```

| Parameter | Description |
| --- | --- |
| `-GenericType` | Position 0. Alias: `Type`. The element type, `T`. Default: `[object]`. |
| `-Capacity` | Position 1. Alias: `Size`. The initial capacity, which is how many elements the list can hold before it has to grow. Default: `4`. |
| `-InputObject` | The elements to add. Accepts pipeline input. |
| `-IncludeNullElements` | Alias: `IncludeNulls`. Adds `$null` elements instead of skipping them. |

### New-HashSet

Creates a `System.Collections.Generic.HashSet[T]`, which holds each distinct element once. Input elements are converted to `T` as they're added. An element that can't be converted writes a non-terminating error and isn't added.

Unless you supply script block equality, the set compares elements like this:

| Element type | Comparison |
| --- | --- |
| `[object]` (the default) | Like PowerShell's `-eq` operator. Strings are compared without regard to case, and values of different types are converted before they're compared, so `1` and `'1'` are the same element. |
| `[string]` | Ordinal, without regard to case. |
| Any other type | The type's default equality. |

`-CaseSensitive` makes string comparisons case-sensitive.

```powershell
$set = New-HashSet
$set.Add('apple')       # True
$set.Add('APPLE')       # False, because the set already holds 'apple'

$set = New-HashSet -CaseSensitive
$set.Add('apple')       # True
$set.Add('APPLE')       # True

# The set drops duplicates as it fills. The string '3' is converted to 3.
$set = 1, 2, 2, 3, '3' | New-HashSet [int]
$set.Count              # 3
```

#### Script block equality

To decide for yourself which elements are equal, pass `-EqualityScript` and `-HashCodeScript` together. You don't need a compiled `IEqualityComparer[T]` class. In this mode, the element type is always `[object]`.

- `-EqualityScript` receives two elements, as `$x` or `$left` and `$y` or `$right`, and returns `$true` if they're equal.
- `-HashCodeScript` receives one element, as `$_`, `$this`, or `$PSItem`, and returns its `[int]` hash code. Elements that are equal must return the same hash code.

Say you import a CSV file in which some employees appear more than once, and you want only the first row for each:

```csv
"Id","Name","Job"
"1","John","The Guy"
"1","John","The Guy?"
"2","Jane","[redacted]"
```

Two rows describe the same employee when their `Id` and `Name` columns match:

```powershell
$equality = {
    $left.Id -eq $right.Id -and $left.Name -eq $right.Name
}
$hashCode = {
    # -eq ignores case, so the hash code has to ignore case as well.
    "$($_.Id)|$($_.Name.ToUpperInvariant())".GetHashCode()
}

$csv = Import-Csv -Path .\Employees.csv
$set = New-HashSet -EqualityScript $equality -HashCodeScript $hashCode

$set.Add($csv[0])       # True
$set.Add($csv[1])       # False, because its Id and Name match $csv[0]
$set.Add($csv[2])       # True

# The same set, filled from the pipeline.
$set = $csv | New-HashSet -EqualityScript $equality -HashCodeScript $hashCode
$set.Count              # 2
```

| Parameter | Description |
| --- | --- |
| `-GenericType` | Position 0. Alias: `Type`. The element type, `T`. Default: `[object]`. Can't be used with `-EqualityScript` and `-HashCodeScript`. |
| `-Capacity` | The initial capacity, which is how many elements the set can hold before it has to grow. Default: `0`. |
| `-InputObject` | The elements to add. Accepts pipeline input. |
| `-CaseSensitive` | Compares strings with regard to case. Available when the element type is `[object]` or `[string]`. |
| `-EqualityScript` | A script block that returns whether two elements are equal. Requires `-HashCodeScript`. |
| `-HashCodeScript` | A script block that returns an element's hash code. Requires `-EqualityScript`. |
| `-ScriptBlockErrorAction` | The `$ErrorActionPreference` inside `-EqualityScript` and `-HashCodeScript`. Default: `Stop`. |

### New-SortedSet

Creates a `System.Collections.Generic.SortedSet[T]`, which holds each distinct element once and keeps the elements in sorted order. Input elements are converted to `T` as they're added. An element that can't be converted writes a non-terminating error and isn't added.

Unless you supply a script block sort order, an `[object]` set compares elements the way PowerShell's `-lt` and `-gt` operators do, a `[string]` set compares strings without regard to case, and a set of any other type uses the type's default order. Elements that compare as equal are duplicates, so a `[string]` set holds only one of `'a'` and `'A'`.

```powershell
$set = 5, 3, 1, 3 | New-SortedSet [int]
$set                    # 1, 3, 5
```

#### Script block sort order

To define the order yourself, pass `-ComparingScript`. It receives two elements, as `$x` or `$left` and `$y` or `$right`, and returns an `[int]`: less than zero if the first element sorts before the second, zero if they're equal, and greater than zero if the first element sorts after the second.

```powershell
# Descending order.
$set = 5, 3, 1 | New-SortedSet [int] -ComparingScript { $y.CompareTo($x) }
$set                    # 5, 3, 1

# Case-sensitive order, in which 'a' and 'A' are different elements.
$set = 'b', 'a', 'B', 'A' | New-SortedSet [string] -ComparingScript { [string]::CompareOrdinal($x, $y) }
$set                    # A, B, a, b

# Order by a property. Jim is the same age as Ann, so the set treats him as a duplicate.
$people = @(
    [pscustomobject]@{ Name = 'Ann'; Age = 30 }
    [pscustomobject]@{ Name = 'Bob'; Age = 20 }
    [pscustomobject]@{ Name = 'Jim'; Age = 30 }
)
$set = $people | New-SortedSet -ComparingScript { $x.Age.CompareTo($y.Age) }
$set.Name               # Bob, Ann
```

| Parameter | Description |
| --- | --- |
| `-GenericType` | Position 0. Alias: `Type`. The element type, `T`. Default: `[object]`. |
| `-InputObject` | The elements to add. Accepts pipeline input. |
| `-ComparingScript` | A script block that returns the sort order of two elements. |
| `-ScriptBlockErrorAction` | The `$ErrorActionPreference` inside `-ComparingScript`. Default: `Stop`. |

### New-Dictionary

Creates a `System.Collections.Generic.Dictionary[TKey, TValue]`. If `-KeyType` and `-ValueType` are both `[object]`, which is the default, it creates a `System.Collections.Hashtable` instead. `[string]` and `[object]` keys, including the keys of a `Hashtable`, are compared without regard to case unless you pass `-CaseSensitive`. `[object]` keys that aren't both strings are compared with their own `Equals` method, so `1` and `'1'` are different keys.

```powershell
# A Hashtable.
$table = New-Dictionary

# A Dictionary[string, int].
$dict = New-Dictionary [string] [int]
$dict['apple'] = 1
$dict['APPLE']          # 1

# A Dictionary[string, int] with case-sensitive keys.
$dict = New-Dictionary [string] [int] -CaseSensitive

# A Dictionary[string, int] that starts with a copy of a hashtable's entries.
$dict = @{ one = 1; two = 2 } | New-Dictionary [string] [int]

# A Hashtable that starts with a copy of $source's entries. -CloneValues gives the copy
# its own ArrayList, so changes to $source.Items don't show up in $copy.Items.
$source = @{ Items = [System.Collections.ArrayList]@(1, 2) }
$copy = $source | New-Dictionary -CloneValues
```

#### Script block key equality

Like `New-HashSet`, `New-Dictionary` takes `-EqualityScript` and `-HashCodeScript` to decide which keys are equal:

```powershell
# Two keys are equal if their Id properties match.
$dict = New-Dictionary -EqualityScript { $x.Id -eq $y.Id } -HashCodeScript { $_.Id.GetHashCode() }

$dict.Add([pscustomobject]@{ Id = 1; Name = 'first' }, 'a')
$dict.Add([pscustomobject]@{ Id = 1; Name = 'second' }, 'b')    # Error: the key is already in the dictionary
```

| Parameter | Description |
| --- | --- |
| `-KeyType` | Position 0. The key type, `TKey`. Default: `[object]`. |
| `-ValueType` | Position 1. The value type, `TValue`. Default: `[object]`. |
| `-Capacity` | Alias: `Size`. The initial capacity, which is how many entries the dictionary can hold before it has to grow. Default: `0`. |
| `-InputObject` | Alias: `CopyFrom`. A hashtable whose entries are copied into the new dictionary. Its keys and values are converted to `-KeyType` and `-ValueType`, and an entry that can't be converted writes a non-terminating error and isn't copied. Accepts pipeline input. |
| `-CloneValues` | Clones the values copied from `-InputObject`, so the new dictionary doesn't share them with the hashtable. Applies to values that implement `ICloneable` and to `PSObject` values. |
| `-CaseSensitive` | Compares string keys with regard to case. Available when the key type is `[object]` or `[string]`. |
| `-EqualityScript` | A script block that returns whether two keys are equal. Requires `-HashCodeScript`. |
| `-HashCodeScript` | A script block that returns a key's hash code. Requires `-EqualityScript`. |
| `-ScriptBlockErrorAction` | The `$ErrorActionPreference` inside `-EqualityScript` and `-HashCodeScript`. Default: `Stop`. |

### ConvertTo-Dictionary

Builds a `Dictionary[TKey, TValue]` that indexes the input objects by a key: either a property's value or a value that a script block returns. Each dictionary value is the input object itself, unless you choose a property or a script block for the value. When that property or script block gives `$null`, the value is `$null` converted to the value type, which is what `$dict.Add($key, $null)` would store: `''` for `[string]`, `0` for `[int]`, and `$null` for `[object]`.

The key type is the type of the first input object's key. The value type is the type of the first object's value, or `[object]` if that value is a custom object, unless you pass `-ValueType`. `[string]` keys are compared without regard to case unless you pass a different `-KeyComparer`. Keys, and values that a property or script block gives, are converted to these types. An object whose key or value can't be converted writes a non-terminating error and isn't added. The command skips input objects that are `$null` and objects whose key is `$null`. With no input, it returns an empty `Hashtable`.

```powershell
$people = @(
    [pscustomobject]@{ Id = 1; Name = 'John'; Dept = 'IT' }
    [pscustomobject]@{ Id = 2; Name = 'Jane'; Dept = 'HR' }
    [pscustomobject]@{ Id = 3; Name = 'Jim'; Dept = 'IT' }
)

# A Dictionary[int, object] of people, keyed by Id.
$byId = $people | ConvertTo-Dictionary -KeyPropertyName Id
$byId[2].Name           # Jane

# A Dictionary[int, string] of names, keyed by Id.
$names = $people | ConvertTo-Dictionary Id Name
$names[3]               # Jim

# Keys and values that script blocks return.
$ids = $people | ConvertTo-Dictionary -KeySelector { $_.Name.ToLower() } -ValueSelector { $_.Id }
$ids['jane']            # 2

# A Dictionary[int, System.Diagnostics.Process] of processes, keyed by process ID.
$processes = Get-Process | ConvertTo-Dictionary Id
```

`-DuplicateKeyBehavior` decides what happens when two input objects have the same key:

| Value | Result |
| --- | --- |
| `Error` (the default) | Writes a non-terminating error and keeps the first value. |
| `Skip` | Writes a warning and keeps the first value. |
| `Concatenate` | Keeps every value. A key with more than one value holds a list of them. The value type is `[object]`, and `-ValueType` is ignored. |

```powershell
$byDept = $people | ConvertTo-Dictionary Dept Name -DuplicateKeyBehavior Concatenate
$byDept['IT']           # John, Jim
$byDept['HR']           # Jane
```

| Parameter | Description |
| --- | --- |
| `-InputObject` | The objects to index. Accepts pipeline input. |
| `-KeyPropertyName` | Position 0. Aliases: `KeyName`, `Key`. The name of the property that holds each object's key. |
| `-KeySelector` | Position 0. A script block that returns each object's key. Use it instead of `-KeyPropertyName`. |
| `-ValuePropertyName` | Position 1. Aliases: `ValueName`, `Value`. The name of the property that holds each object's value, or a script block that returns the value. |
| `-ValueSelector` | A script block that returns each object's value. |
| `-ValueType` | The value type, `TValue`. Default: the type of the first object's value. |
| `-KeyComparer` | The `IEqualityComparer` for the keys, such as `([System.StringComparer]::Ordinal)` for case-sensitive string keys. |
| `-DuplicateKeyBehavior` | `Error`, `Skip`, or `Concatenate`. Default: `Error`. |

## Stopping early

`Assert-AnyObject`, `Assert-AllObject`, and `Find-IndexOf` have their answer as soon as they reach a deciding element: the first match for `Assert-AnyObject` and `Find-IndexOf`, or the first failure for `Assert-AllObject`. At that point, the command writes its result and stops the commands before it in the pipeline, the same way `Select-Object -First` does. Those commands produce no more output, so a search of a large or slow source ends as soon as it has an answer.

```powershell
# Get-ChildItem stops walking the directory tree after it finds the first file larger than 1 GB.
Get-ChildItem -Path $HOME -File -Recurse | Any { $_.Length -gt 1GB }
```

- The stopped commands don't run their `end` blocks. On PowerShell 7.3 and later, an advanced function's `clean` block still runs.
- Only the pipeline that contains the command stops. Statements after that pipeline still run, and so does any pipeline that runs it, for example through `ForEach-Object`.
- With `-InputObject`, there are no commands before it to stop. PowerShell evaluates the whole argument before the command starts, so `Any -InputObject (Get-ChildItem -Recurse) { $_.Length -gt 1GB }` still walks the entire tree. To stop early, pipe the input instead.
- `Find-LastIndexOf` and the commands that build collections always read all of their input.

## Errors in script blocks

`-ScriptBlockErrorAction` sets `$ErrorActionPreference` inside a command's script blocks. Its default depends on the command:

| Commands | Default | Effect |
| --- | --- | --- |
| `Assert-AnyObject`, `Assert-AllObject`, `Find-IndexOf`, `Find-LastIndexOf` | `SilentlyContinue` | Errors in `-Condition` are suppressed. |
| `New-HashSet`, `New-SortedSet`, `New-Dictionary` | `Stop` | Errors in `-EqualityScript`, `-HashCodeScript`, and `-ComparingScript` are terminating errors. |

Because of the `SilentlyContinue` default, an error in a condition can go unnoticed. To see it, pass `-ScriptBlockErrorAction Stop`:

```powershell
$files = 'missing-1.txt', 'missing-2.txt'

$files | Any { (Get-Item -Path $_).Length -gt 0 }
# False, and no error appears

$files | Any { (Get-Item -Path $_).Length -gt 0 } -ScriptBlockErrorAction Stop
# Error: Cannot find path '...\missing-1.txt' because it does not exist.
```

Errors in `-Condition` reach PowerShell unchanged, the same as errors in a `ForEach-Object` script block. With `-ScriptBlockErrorAction Stop`:

- An error that the condition writes, such as the one from `Get-Item` above, ends the whole script, as `-ErrorAction Stop` would. To handle it, run the command in a `try` block.
- A failed method call, such as `$null.Foo()`, ends only the statement that runs the command.

A `throw` in a condition ends the whole script unless `-ScriptBlockErrorAction` is `SilentlyContinue`, and `break` leaves the loop that runs the command.

`ConvertTo-Dictionary` has no `-ScriptBlockErrorAction`. Its `-KeySelector` and `-ValueSelector` run under your own `$ErrorActionPreference`, and their errors reach PowerShell the same way.

## License

ListFunctions is released under the [MIT License](LICENSE).
