# Converting elements

## The rule

When a cmdlet converts an element to a typed collection's element type, it stores what `$list.Add($x)` stores in a typed list of the same element type, `$null` included. The user decided this on 2026-09-30, answering the open question under item 04 in `.work/bugs.md`, and showed it with `[System.Collections.Generic.List[int]]::new()`, where `.Add('123')` stores `123` and `.Add($null)` stores `0`.

- The rule covers dictionary values too. Item 19 applied it to ConvertTo-Dictionary.
- `-IncludeNullElements` decides only whether a `$null` reaches the collection, not what it converts to.
- When a change affects how elements convert, compare the result with `$list.Add(...)` on the same type in both editions, instead of choosing a rule.

## What `$null` converts to

`LanguagePrimitives.ConvertTo($null, T)`, measured in both editions on 2026-09-30, with the structs, `[bool]`, `[char]`, and `[hashtable]` added on 2026-10-04:

| Element type | Result |
|---|---|
| `[int]` | `0` |
| `[string]` | `''` |
| `[bool]` | `$false` |
| `[char]` | `[char]0` |
| `Nullable[int]`, and most classes, such as `[version]`, `[System.IO.FileInfo]`, and `[hashtable]` | `$null` |
| `[ref]` | a `PSReference` that wraps `$null` |
| `[datetime]`, `[guid]`, `[timespan]`, enums such as `[ConsoleColor]`, and other structs such as `KeyValuePair[string, int]` | the conversion throws |

`$list.Add($null)` stores the same value as `ConvertTo` for `[int]`, `[string]`, `Nullable[int]`, `[version]`, `[System.IO.FileInfo]`, and `[ref]` (measured 2026-09-30). So a `$null` in a typed collection of a struct that throws is a conversion error, not `default(T)`.

Dictionary values follow the same rule (measured 2026-10-03): `Add('b', $null)` and the indexer store `''` in a `Dictionary[string, string]`, `0` for `[int]` values, and `$null` for `[object]` and `[version]` values.
