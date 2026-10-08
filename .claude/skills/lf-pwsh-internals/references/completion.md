# Argument completion

Measured 2026-10-07 on PowerShell 7.6.6 and Windows PowerShell 5.1.26100.9444, with `TabExpansion2`, with completers on throwaway functions, and with the cmdlets' completer.

## What a completer receives

- **The whole argument:** `wordToComplete` is the argument's whole text, wherever the cursor is in it, so `[guid]` with the cursor after `gu` arrives as `[guid]`. The results replace the whole argument too: their replacement range starts where the argument starts.
- **Strings:** a quoted argument arrives as its value between straight quotes. PowerShell unescapes the value, adds the closing quote when it's missing, and replaces typographic quotation marks, U+2018 through U+201E, with straight ones. So `'gu` arrives as `'gu'`, and so does `gu` after U+2018, with or without U+2019 after it. `gu` after U+201C arrives as `"gu"`, and `'it''s` as `'it's'`.
- **Commas:** PowerShell splits an argument that isn't in quotes at each comma, and passes only the part after the last comma: in `[System.Collections.Generic.Dictionary[string,in`, the completer gets `in`, and the results replace only `in`. In quotes, the whole string arrives.
- **Which parameter:** the completer runs for a named parameter, for its alias, for `-GenericType:[gu`, and for a positional argument, such as New-Dictionary's second argument, which binds to `-ValueType`. It runs for the cmdlets that have dynamic parameters too, as in `New-HashSet -CaseSensitive [gu`.

## What a completer returns

- An `IArgumentCompleter` that returns `null` gets PowerShell's default completion, which offers file names. One that returns an empty collection gets no completions at all.

## Completing type names

- `CompletionCompleters.CompleteType(string)` works in PowerShell 7, but in Windows PowerShell 5.1 it throws a `NullReferenceException` for every name tried, including an empty one.
- `CommandCompletion.CompleteInput('[gu', 3, $null)` completes a type literal in both editions, and works when an argument completer calls it. It completes the type name that ends at the cursor, including a type argument of a generic type: in `[System.Collections.Generic.List[in`, it replaces `in`. It offers nothing for `[` alone, or with the cursor after the closing bracket, as in `[gu]`.
- The results are type accelerators, types, and namespaces, sorted by `ListItemText` without regard to case in every list seen. A namespace has the `Namespace` result type. A generic type comes without its type arguments: its completion text is `System.Collections.Generic.List`, its `ListItemText` is `List<>`, and its tooltip ends with `[T]`.
- The results include names that PowerShell can't parse as a type name, such as the nested types that the compiler generates for a public C# 14 extension block, whose names start with `<G>$` or `<M>$`. For `[System.`, PowerShell 7.6.6 offers four of them, nested in `System.ExceptionPolyfills`, ahead of every other name. Windows PowerShell 5.1 offered none for the names tried, but it offers them for an assembly that has such a block, as the `net48` run of `TypeNameCompleterTests` shows.
- `New-Object -TypeName` completes type names too, but offers nothing for one that starts with `[`. With nothing typed, it offers every type: 8,843 names in PowerShell 7 and 4,796 in Windows PowerShell 5.1.
- Speed: completing `New-List [S`, about 1,000 names, took 50 to 70 ms in PowerShell 7, and 35 ms in Windows PowerShell 5.1 after a first call of 340 ms.

## In ListFunctions

Engine's `Completion/TypeNameCompleter` is the `[ArgumentCompleter]` of every parameter that has `[ArgumentToTypeTransform]`, and it relies on each fact above.

- It removes the quotes and the closing brackets from the argument, completes the rest as a type literal through `CompleteInput`, and puts the text before the completed name back in front of each result.
- It closes the brackets and the quote only for a type, and leaves them open for a namespace and for a result whose `ListItemText` ends with `<>`.
- It leaves out the names that don't parse as a single type literal, and it returns an empty collection, never `null`, when there's nothing to complete.
- It can't see the part of an argument before a comma, so after a comma, it completes the name on its own and doesn't close the brackets that the earlier part opened.
