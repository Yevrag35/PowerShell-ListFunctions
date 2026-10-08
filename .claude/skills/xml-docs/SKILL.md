---
name: xml-docs
description: XML documentation rules for the ListFunctions solution — tag order, placement relative to attributes, American English / present tense / active voice, cross-reference syntax (including crefs that clash with ZLinq's internal types), constructor phrasing, public-API hygiene. Trigger when writing or editing `<summary>`, `<remarks>`, `<param>`, `<returns>`, `<exception>`, `<typeparam>`, `<value>`, or `<example>` blocks on public, internal, or protected members, or when fixing CS0419 or CS1574 cref warnings. Defers to `.github/copilot-instructions.md` for full detail.
---

# Realta XML Documentation Standards

Follow the rules in `.github/copilot-instructions.md` exactly. Key points:

## Tag order

`<summary>` → `<remarks>` → `<typeparam>` → `<param>` → `<returns>` → `<exception>` → `<value>` (properties) → `<example>`

## Placement

XML docs go **above all attributes**, never between an attribute and its declaration.

```csharp
/// <summary>...</summary>
[DebuggerStepThrough]
[SomeOtherAttribute]
public void Foo() { }
```

## Language

- American English, present tense, active voice.
- Prefer "Gets", "Sets", "Creates", "Returns".

## Cross-references

- `<see langword="null"/>`, `<see langword="true"/>`, `<see langword="false"/>`, `<see langword="ref"/>` for keywords.
- `<see cref="Type.Member"/>` for types/members.
- `<paramref name="..."/>` for parameter references inside docs.
- `<typeparamref name="..."/>` for type-parameter references.
- **Never** `<see langword="..."/>` inside an `<exception>` tag — write the keyword as plain text there:
  `<exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>`

### Files with `using ZLinq;`

In a file with `using ZLinq;`, write every cref to `ArgumentNullException` as `System.ArgumentNullException`, whether it's in `<exception cref>`, `<see cref>`, or `<seealso cref>`. Other exceptions, such as `ArgumentException`, keep the short form. From `src/engine/ListFunctions.Engine/Modern/ComparingBlock.cs`:

```csharp
/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="genericType"/> is null.</exception>
/// <exception cref="ArgumentException">Thrown when <paramref name="genericType"/> can't be used as a generic type argument.</exception>
```

- ZLinq's `netstandard2.0` build declares an internal `ZLinq.ArgumentNullException`. Code can't see an internal type in another assembly, but cref lookup still finds it, so the short form is ambiguous: warning CS0419, "Ambiguous reference in cref attribute: 'ArgumentNullException'."
- Engine's `netstandard2.0` build and the `net48` build of `ListFunctions-NETFramework`, which compiles Next's files, report the warning. The `net10.0` builds don't, so building only `ListFunctions-Next` won't catch it. Build the full solution, and don't shorten the cref because one target is quiet.
- ZLinq also declares an internal `ZLinq.GC`, in every target, so write `System.GC` in crefs too, as in `<see cref="System.GC.KeepAlive(object)"/>`. A bare `<see cref="GC"/>` gives CS0419, and a bare member cref such as `GC.KeepAlive(object)` gives CS1574, "could not be resolved."
- Code isn't affected. `throw new ArgumentNullException(...)` and `GC.KeepAlive(...)` compile without the namespace; only crefs need it.
- Files without `using ZLinq;` keep the short form. When you add `using ZLinq;` to a file, qualify the `ArgumentNullException` and `GC` crefs it already has.

## Public API hygiene

Do not reference internal/private symbols in public docs. The reader of generated XML doc files only sees public surface — references to private types render as broken `cref` warnings.

## Thread safety

Always state if a member is or isn't thread-safe when it's non-obvious.

## Constructor phrasing

"Initializes a new instance of `<see cref="ClassName"/>` …"

## Property phrasing

"Gets ..." (read-only) or "Gets or sets ..." (read/write). Use `<value>` to describe what the property holds when it's not obvious from the summary.

## Reference

`.github/copilot-instructions.md` — full XML documentation style guide. Read it for edge cases and examples beyond the rules above.
