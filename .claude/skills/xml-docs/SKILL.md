---
name: xml-docs
description: XML documentation rules for the ListFunctions solution — tag order, placement relative to attributes, wording (American English, third-person singular present tense, active voice, and how each kind of symbol's summary starts), cross-reference syntax (including `<c>` for PowerShell syntax and crefs that clash with ZLinq's internal types), extension block docs, constructor and property phrasing, public-API hygiene. Trigger when writing or editing `<summary>`, `<remarks>`, `<param>`, `<returns>`, `<exception>`, `<typeparam>`, `<value>`, or `<example>` blocks on any type or member, or when fixing CS0419 or CS1574 cref warnings. Defers to `.github/copilot-instructions.md` for full detail.
---

# ListFunctions XML Documentation Standards

Follow the rules in `.github/copilot-instructions.md` exactly. Key points:

## Scope

Document every type, every public, protected, or internal member, and every private method.

## Tag order

`<summary>` → `<remarks>` → `<typeparam>` → `<param>` → `<returns>` → `<exception>` → `<value>` (properties and indexers) → `<example>`

- `<returns>` and `<value>` never appear together. `<value>` keeps its place after `<exception>`.
- Write one `<exception>` tag per exception type, covering every condition that throws it.

## Placement

XML docs go **above all attributes**, never between an attribute and its declaration.

```csharp
/// <summary>...</summary>
[DebuggerStepThrough]
[SomeOtherAttribute]
public void Foo() { }
```

## Language

- American English, concise, active voice, present tense, in the third-person singular: "Gets the comparer.", not the imperative "Get the comparer."
- How a `<summary>` starts depends on the kind of symbol:
  - Methods: a verb, such as "Gets ...", "Creates ...", "Returns ...", or "Determines whether ...".
  - Abstract and virtual members: "When implemented in a derived class, ..." or "When overridden in a derived class, ...", then the verb.
  - Types: "Represents ..." or "Provides ...".
  - Fields, constants, and enum members: a noun phrase or a short sentence, such as "The shared empty collection."
  - Constructors and properties: see their sections below.
- `<typeparam>`, `<param>`, `<returns>`, and `<value>` are noun phrases, such as "The element type." A Boolean result reads "`<see langword="true"/>` if ...; otherwise, `<see langword="false"/>`."
- `<exception>` text starts with "Thrown when ...". That form and the "When implemented/overridden in a derived class" openings are the only exceptions to active voice.
- Doc comments use ASCII punctuation: straight quotes, not smart quotes, and three periods, not an ellipsis character.

## Cross-references

- `<see langword="null"/>`, `<see langword="true"/>`, `<see langword="false"/>`, `<see langword="ref"/>` for C# keywords.
- `<see cref="Type.Member"/>` for types/members.
- `<paramref name="..."/>` for parameter references inside docs.
- `<typeparamref name="..."/>` for type-parameter references.
- `<c>...</c>` for inline code that isn't a reference to a symbol: PowerShell syntax such as `$_`, `-CaseSensitive`, and `begin`, and C# expressions. PowerShell's `break` is `<c>break</c>`; `<see langword="break"/>` would mean C#'s.
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

## Extension blocks

- A C# 14 `extension(...)` block gets its own doc comment above the `extension` keyword: a `<summary>`, an optional `<remarks>`, a `<typeparam>` for each of the block's own type parameters, and a `<param>` for the receiver when the block names one. A block that leaves its receiver unnamed, such as `extension(ArgumentNullException)` in `src/engine/ListFunctions.Engine/Extensions/NullGuardExtensions.cs`, has no `<param>`.
- Members inside the block never add a `<param>` for the receiver. They refer to a named receiver with `<paramref>`.
- A cref to an extension member names the block, as in `<see cref="ContainingClass.extension(ReceiverType).Member(ParameterTypes)"/>`. The pre-C# 14 static form and the bare member signature both give CS1574.

## Public API hygiene

Do not reference internal/private symbols in public docs. The reader of generated XML doc files only sees public surface — references to private types render as broken `cref` warnings.

## Thread safety

Always state if a member is or isn't thread-safe when it's non-obvious.

## Constructor phrasing

"Initializes a new `<see cref="TypeName"/>` instance ...", where `TypeName` is the constructor's own type. When the constructor has parameters, say what they supply, so the summary tells it apart from the type's other constructors: "Initializes a new `<see cref="HashBlock"/>` instance with the specified script block."

## Property phrasing

"Gets ..." (read-only) or "Gets or sets ..." (read/write). A Boolean property uses "Gets a value that indicates whether ...". Every property, indexers included, also gets a `<value>` that describes what it holds.

## Reference

`.github/copilot-instructions.md` — full XML documentation style guide. Read it for edge cases and examples beyond the rules above.
