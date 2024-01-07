# <img height="30px" src="./.icon/list-functions.png" alt="ListFunctions"></img> ListFunctions for PowerShell

[![version](https://img.shields.io/powershellgallery/v/ListFunctions.svg?include_prereleases)](https://www.powershellgallery.com/packages/ListFunctions)
[![downloads](https://img.shields.io/powershellgallery/dt/ListFunctions.svg?label=downloads)](https://www.powershellgallery.com/stats/packages/ListFunctions?groupby=Version)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/097d27365fac4fc69ac2c45570db85d6)](https://www.codacy.com/gh/Yevrag35/PowerShell-ListFunctions/dashboard?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=Yevrag35/PowerShell-ListFunctions&amp;utm_campaign=Badge_Grade)

This is a simple module that provides functions to manipulate and search through Arrays, Collections, and Lists.  

"ListFunctions" refers to the fact that some the functions are inspired by <code>System.Collections.Generic.List</code>'s methods.

## Assert Cmdlets (Any / All)

Sometimes you may want to just check if a specific collection has specific elements in it but don't need them themselves.

### Assert-AnyObject

_Aliases: __Any-Object__, __Any___

This cmdlet returns <code>true</code> or <code>false</code> if at least 1 element in the collection matches a condition - or - if no condition is provided, just if it contains at least 1 element.

If a scriptblock condition is specified, then it will substitute any of the following variables for each element: <code>\$\_</code>, <code>\$this</code>, <code>\$psitem</code>

```powershell
$array = @(1, 2, 3)

if (($array | Any { $_ -gt 2})) {

    # ... at least 1 element is greater than 2.
}

if (($array | Any-Object)) {

    # ... at least 1 element exists.
}
```

### Assert-AllObject(s)

This cmdlet returns <code>true</code> or <code>false</code> indicating whether __all__ elements in a collection match the specified condition.

The scriptblock condition will substitute any of the following variables for each element: <code>\$\_</code>, <code>\$this</code>, <code>\$psitem</code>

```powershell
$array = @(1, 2, 'John')

if (-not ($array | All { $_ -is [int] })) {

    #... at least 1 element is NOT an [int] object.
}
```

---

## Collection Constructors

These cmdlets provide easier ways of constructing the more nuanced, generic types within the <code>System.Collections.Generic</code> API namespace.

### New-List

Constructs and returns a list of type `[System.Collections.Generic.List[T]]` where `T` is the generic type defined through the `-GenericType` parameter (defaults to `[object]`).

```powershell
# Create a list of objects with the default capacity (0). 
# Like [System.Collections.ArrayList], objects of *any* type can be added.
$list = New-List

# Create a list of System.Guid objects with an initial capacity of 10,000.
$list = New-List [guid] -Capacity 10000

# Create a list of integers and provide it with the initial values to be added.
$list = @(1, 2, '3') | New-List [int]
# -or-
$list = New-List [int] -InputObject @(3, '100', 56)

```