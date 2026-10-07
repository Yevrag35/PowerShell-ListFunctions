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
| [Test-AnyObject](#test-anyobject) | `Any`, `Any-Object`, `Assert-Any`, `Assert-AnyObject` | `[bool]` | Tests whether any element satisfies a condition, or whether there are any elements at all. |
| [Test-AllObject](#test-allobject) | `All`, `All-Object`, `All-Objects`, `Assert-All`, `Assert-AllObject`, `Assert-AllObjects` | `[bool]` | Tests whether every element satisfies a condition. |
| [Find-Index](#find-index) | `Find-IndexOf`, `IndexOf` | `[int]` | Finds the index of the first element that satisfies a condition. |
| [Find-LastIndex](#find-lastindex) | `Find-LastIndexOf`, `LastIndexOf` | `[int]` | Finds the index of the last element that satisfies a condition. |
| [New-List](#new-list) | | `List[T]` | Creates a list. |
| [New-HashSet](#new-hashset) | | `HashSet[T]` | Creates a set of distinct elements, optionally with script block equality. |
| [New-SortedSet](#new-sortedset) | | `SortedSet[T]` | Creates a sorted set of distinct elements, optionally with a script block sort order. |
| [New-Dictionary](#new-dictionary) | | `Dictionary[TKey, TValue]` | Creates a dictionary, optionally with script block key equality. |
| [ConvertTo-Dictionary](#convertto-dictionary) | | `Dictionary[TKey, TValue]` | Indexes objects in a dictionary by a property or a computed key. |

## Input

Every command except `New-Dictionary` takes its elements from the pipeline or from `-InputObject`:

- Each object that comes through the pipeline is one element, even when it's `$null` or an array, the same as with `ForEach-Object`.
- An array or a list that you pass to `-InputObject` supplies its elements, and `$null` supplies none. Any other value, such as a string or a hashtable, is one element.

`Find-Index` and `Find-LastIndex` search what you pass to `-InputObject` the same way they'd search it piped, so a set, a queue, or any other collection supplies its elements too. A value that isn't a collection is one element, and they write a warning for it, unless it's a string, which PowerShell never treats as a collection. For a dictionary, such as a hashtable, the warning suggests `$dict.GetEnumerator()`, which supplies the entries.

```powershell
1, $null, 3 | Find-Index { $_ -eq 3 }              # 2
@(1, @(2, 3), 4) | Find-Index { $_ -is [array] }   # 1
Find-Index -InputObject 1, 2, 3 { $_ -eq 3 }       # 2

$set = 1, 2, 3 | New-HashSet [int]
Find-Index -InputObject $set { $_ -eq 2 }          # 1, the same as $set | Find-Index { $_ -eq 2 }
```

A command takes its input from the pipeline or from `-InputObject`, not from both. When it gets both, it writes an error before it reads any input, and no result:

```powershell
'a', 'b' | Find-Index { $_ -eq 1 } -InputObject 1, 2
# Error: Cannot use -InputObject and pipeline input together, because both supply the command's input. Pipe the input,
# or pass it to -InputObject, but not both.
```

### `$null` input

The commands don't all treat `$null` the same way. Each one does what fits its job:

- **`Test-AnyObject`, `Test-AllObject`, `Find-Index`, and `Find-LastIndex`** pass a `$null` element to `-Condition` like any other element, so your condition decides what it means. An index counts every element, which is why `1, $null, 3 | Find-Index { $_ -eq 3 }` above is `2`, the index of `3` in the input. Without a condition, `Test-AnyObject` tests whether the input holds anything, so it doesn't count `$null` elements.
- **`New-List`** skips `$null` elements unless you pass `-IncludeNullElements`. A list keeps every element it's given, so `$null` elements, which usually stand for missing values, would pile up in it. Pass the switch when they matter, for example to keep the list's positions in step with the input. In a typed list, a `$null` element then becomes what `$list.Add($null)` would store, such as `0` in a `List[int]`.
- **`New-HashSet`** adds a `$null` element to an `[object]` set, which stores it as it is, and only once, like any other distinct element. So `$set.Contains($null)` tells you whether the input had one. A set of any other type skips `$null` elements, because in most types, `$null` would become a value that wasn't in the input, such as `0` in an `[int]` set.
- **`New-SortedSet`** skips `$null` elements, whatever the element type. In the default `[string]` set, `$null` would become `''`, a value that wasn't in the input, and in an `[object]` set, the set would put it first without running your `-ComparingScript`.
- **`New-Dictionary`** copies the entries of a hashtable, so `$null` in place of the hashtable is a parameter binding error. A `$null` value in the hashtable is copied, because it may be intentional. It's converted to `-ValueType` like any other value, so it becomes `''` for `[string]` and `0` for `[int]`.
- **`ConvertTo-Dictionary`** skips a `$null` input object, which has no key or value to select. An object whose key is `$null` writes a non-terminating error and isn't added, because a dictionary can't hold a `$null` key, and the error tells you which objects lack a key, for example because the property name is misspelled. A `$null` value is stored, because it may be intentional, such as a property that's empty on some of the objects.

## Script blocks

Most commands take script blocks, which they run once for each element or once for each pair of elements they compare. The command sets variables that hold the elements:

| Script block | Parameters | Variables |
| --- | --- | --- |
| Receives one element | `-Condition`, `-HashCodeScript`, `-KeySelector`, `-ValueSelector` | `$_`, `$this`, `$PSItem`, or `$args[0]` |
| Compares two elements | `-EqualityScript`, `-ComparingScript` | `$x`, `$left`, or `$args[0]` for the first element, and `$y`, `$right`, or `$args[1]` for the second |

A script block has to use at least one of these variables for each element it receives. Otherwise, the command fails with a parameter validation error. A variable that appears only inside a nested script block doesn't count.

The commands run one block of a script block: its `process` block if it has one, and otherwise its `end` block, which holds the statements of a script block without named blocks. So a script block can't have a `begin` block, a `clean` block, or both a `process` block and an `end` block, and the block that runs has to contain at least one statement. A script block that breaks this rule fails with a parameter validation error too, before the command reads any input.

Passing `$null` to a script block parameter is the same as leaving the parameter out. So when the command requires the parameter, as `Test-AllObject`, `Find-Index`, and `Find-LastIndex` require `-Condition`, PowerShell rejects `$null` with a parameter binding error. `Test-AnyObject -Condition $null` tests for elements that aren't `$null`, the same as `Test-AnyObject` without a condition.

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

### Test-AnyObject

Aliases: `Any`, `Any-Object`, `Assert-Any`, `Assert-AnyObject`

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
| `-Condition` | Position 0. Aliases: `ScriptBlock`, `FilterScript`. Optional. The test to run on each element. `$null` is the same as no condition. |
| `-InputObject` | The elements to test. Accepts pipeline input. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. What happens to errors in `-Condition`. Default: `SilentlyContinue`, which writes each error as a warning. See [Errors in script blocks](#errors-in-script-blocks). |

### Test-AllObject

Aliases: `All`, `All-Object`, `All-Objects`, `Assert-All`, `Assert-AllObject`, `Assert-AllObjects`

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
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. What happens to errors in `-Condition`. Default: `SilentlyContinue`, which writes each error as a warning. See [Errors in script blocks](#errors-in-script-blocks). |

## Searching

### Find-Index

Aliases: `Find-IndexOf`, `IndexOf`

Returns the zero-based index of the first input element that satisfies `-Condition`, or `-1` if none does. After the first match, it stops: it doesn't test the remaining elements, and it [stops the commands that send it pipeline input](#stopping-early).

```powershell
$names = 'Ann', 'Bob', 'Cid', 'Bob'

$names | Find-Index { $_ -eq 'Bob' }                          # 1
$names | IndexOf { $_ -like 'Z*' }                            # -1
Find-Index -InputObject $names -Condition { $_ -like 'C*' }   # 2
```

| Parameter | Description |
| --- | --- |
| `-Condition` | Position 0. Aliases: `ScriptBlock`, `FilterScript`. Required. The test to run on each element. |
| `-InputObject` | Alias: `List`. The elements to search. Accepts pipeline input. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. What happens to errors in `-Condition`. Default: `SilentlyContinue`, which writes each error as a warning. See [Errors in script blocks](#errors-in-script-blocks). |

### Find-LastIndex

Aliases: `Find-LastIndexOf`, `LastIndexOf`

Returns the zero-based index of the last input element that satisfies `-Condition`, or `-1` if none does. It collects all of its input before it searches backward from the end.

```powershell
$names = 'Ann', 'Bob', 'Cid', 'Bob'

$names | Find-LastIndex { $_ -eq 'Bob' }     # 3
```

It takes the same parameters as [Find-Index](#find-index).

## Building collections

Each of these commands outputs the collection it builds as a single object. When you assign the output to a variable, you get the collection itself, even if it's empty or holds only one element.

### New-List

Creates a `System.Collections.Generic.List[T]`. Input elements are converted to `T` as they're added. An element that can't be converted writes a non-terminating error and isn't added. `$null` elements are skipped unless you pass `-IncludeNullElements`. See [`$null` input](#null-input).

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
| `-Capacity` | Alias: `Size`. The initial capacity, which is how many elements the list can hold before it has to grow. Default: `0`. |
| `-InputObject` | The elements to add. Accepts pipeline input. |
| `-IncludeNullElements` | Alias: `IncludeNulls`. Adds `$null` elements instead of skipping them. |

### New-HashSet

Creates a `System.Collections.Generic.HashSet[T]`, which holds each distinct element once. Input elements are converted to `T` as they're added. An element that can't be converted writes a non-terminating error and isn't added. An `[object]` set adds a `$null` element, and a set of any other type skips it. See [`$null` input](#null-input).

Unless you supply script block equality, the set compares elements like this:

| Element type | Comparison |
| --- | --- |
| `[object]` (the default) | Two strings are compared the same way as `[string]` elements, and any other two elements with their own `Equals` method. Values aren't converted before they're compared, so `1`, `'1'`, and `[long]1` are three different elements. |
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

To decide for yourself which elements are equal, pass `-EqualityScript` and `-HashCodeScript` together. You don't need a compiled `IEqualityComparer[T]` class, and `T` can be any type.

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

With `-GenericType`, the script blocks compare elements of that type:

```powershell
# Numbers that end in the same digit are equal, so 11 and 21 are duplicates of 1.
$set = 1, 2, 11, 21 | New-HashSet [int] -EqualityScript { $x % 10 -eq $y % 10 } -HashCodeScript { $_ % 10 }
$set.Count              # 2
```

| Parameter | Description |
| --- | --- |
| `-GenericType` | Position 0. Alias: `Type`. The element type, `T`. Default: `[object]`. |
| `-Capacity` | Alias: `Size`. The initial capacity, which is how many elements the set can hold before it has to grow. Default: `0`. |
| `-InputObject` | The elements to add. Accepts pipeline input. |
| `-CaseSensitive` | Compares strings with regard to case. Available when the element type is `[object]` or `[string]`. |
| `-EqualityScript` | A script block that returns whether two elements are equal. Requires `-HashCodeScript`. |
| `-HashCodeScript` | A script block that returns an element's hash code. Requires `-EqualityScript`. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. The `$ErrorActionPreference` inside `-EqualityScript` and `-HashCodeScript`. Default: `Stop`. See [Errors in script blocks](#errors-in-script-blocks). |

### New-SortedSet

Creates a `System.Collections.Generic.SortedSet[T]`, which holds each distinct element once and keeps the elements in sorted order. Input elements are converted to `T` as they're added. An element that can't be converted writes a non-terminating error and isn't added. `$null` elements are skipped. See [`$null` input](#null-input).

Unless you supply a script block sort order, the set uses the default order of `T`, which is `[string]` unless you pass `-GenericType`. A `[string]` set compares strings ordinally, without regard to case: it orders them by the codes of their characters, as if they were uppercase. So digits sort before letters, and punctuation such as `_` and letters outside ASCII, such as `é`, sort after `Z`. The order doesn't depend on your culture or on the PowerShell edition. Elements that compare as equal are duplicates, so a `[string]` set holds only one of `'a'` and `'A'`. With `-CaseSensitive`, a `[string]` set compares the codes of the characters as they are, so `'a'` and `'A'` are different elements, and the uppercase letters `A` to `Z` sort before the lowercase ones.

`T` needs a default order: it has to implement `IComparable[T]`, as `[int]` implements `IComparable[int]`, or be an enum, or be a `Nullable[U]` whose `U` qualifies. `[string]`, `[int]`, `[datetime]`, and `[version]` all qualify. Any other type, such as `[object]` or `[psobject]`, is an error before the command reads any input. To sort elements of those types, pass `-ComparingScript`.

```powershell
$set = 5, 3, 1, 3 | New-SortedSet [int]
$set                    # 1, 3, 5

# Without a type, the elements are strings, so numbers sort as text.
$set = 5, 3, 10 | New-SortedSet
$set                    # 10, 3, 5

# Strings sort by the codes of their uppercase characters.
$set = 'b', '_x', 'a', 'Z' | New-SortedSet
$set                    # a, b, Z, _x

# With -CaseSensitive, 'a' and 'A' are different elements, and uppercase letters sort first.
$set = 'b', 'a', 'B', 'A' | New-SortedSet -CaseSensitive
$set                    # A, B, a, b
```

#### Script block sort order

To define the order yourself, pass `-ComparingScript`. It receives two elements, as `$x` or `$left` and `$y` or `$right`, and returns an `[int]`: less than zero if the first element sorts before the second, zero if they're equal, and greater than zero if the first element sorts after the second. With `-ComparingScript`, `T` can be any type, and it's `[object]` unless you pass `-GenericType`.

```powershell
# Descending order.
$set = 5, 3, 1 | New-SortedSet [int] -ComparingScript { $y.CompareTo($x) }
$set                    # 5, 3, 1

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
| `-GenericType` | Position 0. Alias: `Type`. The element type, `T`. Default: `[string]`, or `[object]` with `-ComparingScript`. |
| `-InputObject` | The elements to add. Accepts pipeline input. |
| `-CaseSensitive` | Compares strings with regard to case. Available when the element type is `[string]`, the default, but not with `-ComparingScript`. |
| `-ComparingScript` | A script block that returns the sort order of two elements. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. The `$ErrorActionPreference` inside `-ComparingScript`. Default: `Stop`. See [Errors in script blocks](#errors-in-script-blocks). |

### New-Dictionary

Creates a `System.Collections.Generic.Dictionary[TKey, TValue]`. `-KeyType` and `-ValueType` are both `[object]` unless you pass them. `[string]` keys are compared ordinally, without regard to case unless you pass `-CaseSensitive`. `[object]` keys are compared the same way when both are strings, and with their own `Equals` method otherwise, so `1` and `'1'` are different keys.

With `[object]` keys, PowerShell's dot notation, such as `$dict.apple`, doesn't read or set keys. Use the indexer, `$dict['apple']`, or pass `[string]` as the key type.

```powershell
# A Dictionary[object, object].
$dict = New-Dictionary
$dict['apple'] = 1
$dict['APPLE']          # 1

# A Dictionary[string, int].
$dict = New-Dictionary [string] [int]

# A Dictionary[string, int] with case-sensitive keys.
$dict = New-Dictionary [string] [int] -CaseSensitive

# A Dictionary[string, int] that starts with a copy of a hashtable's entries.
$dict = @{ one = 1; two = 2 } | New-Dictionary [string] [int]

# A Dictionary[object, object] that starts with a copy of $source's entries. -CloneValues gives
# the copy its own ArrayList, so changes to $source.Items don't show up in $copy['Items'].
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
| `-InputObject` | Alias: `CopyFrom`. A hashtable whose entries are copied into the new dictionary. Its keys and values are converted to `-KeyType` and `-ValueType`, and an entry that can't be converted writes a non-terminating error and isn't copied. A `$null` value is converted too, to what `$dict.Add($key, $null)` would store, such as `''` for `[string]` and `0` for `[int]`. Accepts pipeline input. |
| `-CloneValues` | Clones the values copied from `-InputObject`, so the new dictionary doesn't share them with the hashtable. Applies to values that implement `ICloneable` and to `PSObject` values. |
| `-CaseSensitive` | Compares string keys with regard to case. Available when the key type is `[object]` or `[string]`. |
| `-EqualityScript` | A script block that returns whether two keys are equal. Requires `-HashCodeScript`. |
| `-HashCodeScript` | A script block that returns a key's hash code. Requires `-EqualityScript`. |
| `-ScriptBlockErrorAction` | Alias: `ScriptErrorAction`. The `$ErrorActionPreference` inside `-EqualityScript` and `-HashCodeScript`. Default: `Stop`. See [Errors in script blocks](#errors-in-script-blocks). |

### ConvertTo-Dictionary

Builds a `Dictionary[TKey, TValue]` that indexes the input objects by a key: either a property's value or a value that a script block returns. Each dictionary value is the input object itself, unless you choose a property or a script block for the value.

`TKey` and `TValue` are `[object]` unless you pass `-KeyType` and `-ValueType`, whatever the input objects are. Every key and value is converted to these types the way `$dict.Add($key, $value)` would convert it, including an input object that is its own value. An object whose key or value can't be converted writes a non-terminating error and isn't added. When a value is `$null`, the dictionary stores what `$dict.Add($key, $null)` would store: `$null` for `[object]`, `''` for `[string]`, and `0` for `[int]`.

`[string]` keys are compared ordinally, without regard to case. `[object]` keys are compared the same way when both are strings, and with their own `Equals` method otherwise, so `1` and `'1'` are different keys. To compare keys another way, pass `-KeyComparer`. With `[object]` keys, PowerShell's dot notation, such as `$byName.Jane`, doesn't read or set keys. Use the indexer, `$byName['Jane']`, or pass `[string]` as `-KeyType`.

The command skips input objects that are `$null`. An object whose key is `$null`, such as one that doesn't have the property that `-KeyPropertyName` names, writes a non-terminating error and isn't added. See [`$null` input](#null-input) for the reasons. With no input, the command returns an empty dictionary.

```powershell
$people = @(
    [pscustomobject]@{ Id = 1; Name = 'John'; Dept = 'IT' }
    [pscustomobject]@{ Id = 2; Name = 'Jane'; Dept = 'HR' }
    [pscustomobject]@{ Id = 3; Name = 'Jim'; Dept = 'IT' }
)

# A Dictionary[object, object] of people, keyed by Id.
$byId = $people | ConvertTo-Dictionary -KeyPropertyName Id
$byId[2].Name           # Jane

# A Dictionary[object, object] of names, keyed by Id.
$names = $people | ConvertTo-Dictionary Id Name
$names[3]               # Jim

# The same names in a Dictionary[int, string].
$names = $people | ConvertTo-Dictionary Id Name -KeyType [int] -ValueType [string]

# Keys and values that script blocks return.
$ids = $people | ConvertTo-Dictionary -KeySelector { $_.Name.ToLower() } -ValueSelector { $_.Id }
$ids['jane']            # 2

# A Dictionary[int, System.Diagnostics.Process] of processes, keyed by process ID.
$processes = Get-Process | ConvertTo-Dictionary Id -KeyType [int] -ValueType [System.Diagnostics.Process]
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
| `-KeyPropertyName` | Position 0. Aliases: `KeyName`, `Key`. Required unless you pass `-KeySelector`. The name of the property that holds each object's key. To compute keys with a script block, use `-KeySelector`. |
| `-KeySelector` | Position 0. A script block that returns each object's key. Use it instead of `-KeyPropertyName`. |
| `-ValuePropertyName` | Position 1. Aliases: `ValueName`, `Value`. The name of the property that holds each object's value, or a script block that returns the value. Any other value, such as a number, is an error. You can't use it with `-ValueSelector`. |
| `-ValueSelector` | A script block that returns each object's value. You can't use it with `-ValuePropertyName`. |
| `-KeyType` | The key type, `TKey`. Default: `[object]`. |
| `-ValueType` | The value type, `TValue`. Default: `[object]`. |
| `-KeyComparer` | The `IEqualityComparer` for the keys, such as `([System.StringComparer]::Ordinal)` for case-sensitive string keys. It works with any key type. A `StringComparer` compares two strings as strings, and any other two keys with their own `Equals` method. |
| `-DuplicateKeyBehavior` | `Error`, `Skip`, or `Concatenate`. Default: `Error`. |

## Stopping early

`Test-AnyObject`, `Test-AllObject`, and `Find-Index` have their answer as soon as they reach a deciding element: the first match for `Test-AnyObject` and `Find-Index`, or the first failure for `Test-AllObject`. At that point, the command writes its result and stops the commands before it in the pipeline, the same way `Select-Object -First` does. Those commands produce no more output, so a search of a large or slow source ends as soon as it has an answer.

```powershell
# Get-ChildItem stops walking the directory tree after it finds the first file larger than 1 GB.
Get-ChildItem -Path $HOME -File -Recurse | Any { $_.Length -gt 1GB }
```

- The stopped commands don't run their `end` blocks. On PowerShell 7.3 and later, an advanced function's `clean` block still runs.
- Only the pipeline that contains the command stops. Statements after that pipeline still run, and so does any pipeline that runs it, for example through `ForEach-Object`.
- With `-InputObject`, there are no commands before it to stop. PowerShell evaluates the whole argument before the command starts, so `Any -InputObject (Get-ChildItem -Recurse) { $_.Length -gt 1GB }` still walks the entire tree. To stop early, pipe the input instead.
- `Find-LastIndex` and the commands that build collections always read all of their input.

## Errors in script blocks

`-ScriptBlockErrorAction` decides what happens to errors in a command's script blocks. Its default depends on the command:

| Commands | Default | Effect |
| --- | --- | --- |
| `Test-AnyObject`, `Test-AllObject`, `Find-Index`, `Find-LastIndex` | `SilentlyContinue` | An error in `-Condition` becomes a warning, and the element doesn't satisfy the condition. |
| `New-HashSet`, `New-SortedSet`, `New-Dictionary` | `Stop` | An error that `-EqualityScript`, `-HashCodeScript`, or `-ComparingScript` writes ends the whole script. |

### Warnings from conditions

`Test-AnyObject`, `Test-AllObject`, `Find-Index`, and `Find-LastIndex` turn errors in `-Condition` into warnings when `-ScriptBlockErrorAction` is `SilentlyContinue`, the default, or `Ignore`. They run `-Condition` with `$ErrorActionPreference` set to `Stop`, so the first error that the condition doesn't handle itself, such as an error that a command writes, a failed method call, or a `throw`, ends the condition for that element. The command writes the error's message as a warning, and the element doesn't satisfy the condition, so `Test-AllObject` returns `$false`, and the other commands go on with the next element.

```powershell
$files = 'missing-1.txt', 'missing-2.txt'

$files | Any { (Get-Item -Path $_).Length -gt 0 }
# WARNING: Cannot find path '...\missing-1.txt' because it does not exist.
# WARNING: Cannot find path '...\missing-2.txt' because it does not exist.
# False
```

An error that the condition handles itself isn't a warning. So handle the errors that you expect, in a `try` block or with a command's `-ErrorAction SilentlyContinue` or `-ErrorAction Ignore`, and the condition goes on the way you wrote it:

```powershell
$files | Any { $null -ne (Get-Item -Path $_ -ErrorAction Ignore) }
# False, and no warnings
```

The warnings follow the command's `-WarningAction`, so `-WarningAction SilentlyContinue` hides them, and `-WarningAction Stop` makes the first one end the script. To have an error in the condition end the script instead, pass `-ScriptBlockErrorAction Stop`.

### Errors that reach PowerShell

In every other case, errors in script blocks reach PowerShell unchanged, the same as errors in a `ForEach-Object` script block. That includes every script block of `New-HashSet`, `New-SortedSet`, and `New-Dictionary`. With `-ScriptBlockErrorAction Stop`:

- An error that the script block writes, such as the one from `Get-Item` above, ends the whole script, as `-ErrorAction Stop` would. To handle it, run the command in a `try` block.
- A failed method call, such as `$null.Foo()`, ends only the statement that runs the command.

A `throw` in a script block ends the whole script unless `-ScriptBlockErrorAction` is `SilentlyContinue`, and `break` leaves the loop that runs the command. With `Continue`, an error that the script block writes appears, and the script block goes on, so the command uses its output. The command's own `-ErrorAction` doesn't change any of this, because these errors come from your script block, not from the command.

`-HashCodeScript` has to output an `[int]`, or a value that converts to one, and so does `-ComparingScript`. When either outputs nothing, `$null`, or a value that can't be converted, the error ends the statement that runs the command, the same as a failed method call.

`New-HashSet`, `New-SortedSet`, and `New-Dictionary` don't output a collection when an error from one of their script blocks reaches PowerShell. Their other errors are non-terminating, such as an element that can't be converted to the element type, or a duplicate key: the command writes the error, skips that element or entry, and goes on with the rest.

```powershell
$people = [pscustomobject]@{ Name = 'Ann' }, [pscustomobject]@{ FullName = 'Bob Smith' }
$set = $people | New-HashSet -EqualityScript { $x.Name -eq $y.Name } -HashCodeScript { $_.Name.ToUpperInvariant().GetHashCode() }
# Error: You cannot call a method on a null-valued expression.
# Bob has no Name property, so the statement ends there, and nothing is assigned to $set.
```

`ConvertTo-Dictionary` has no `-ScriptBlockErrorAction`. Its `-KeySelector` and `-ValueSelector` run under your own `$ErrorActionPreference`, and their errors reach PowerShell the same way.

## License

ListFunctions is released under the [MIT License](LICENSE).
