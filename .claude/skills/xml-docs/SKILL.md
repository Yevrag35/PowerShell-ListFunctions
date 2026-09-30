---
name: xml-docs
description: XML documentation rules for the ListFunctions solution — tag order, placement relative to attributes, American English / present tense / active voice, cross-reference syntax, constructor phrasing, public-API hygiene. Trigger when writing or editing `<summary>`, `<remarks>`, `<param>`, `<returns>`, `<exception>`, `<typeparam>`, `<value>`, or `<example>` blocks on public, internal, or protected members. Defers to `.github/copilot-instructions.md` for full detail.
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
