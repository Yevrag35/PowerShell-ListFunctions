# Git commit message mood

Use the "third-person singular simple present tense" in git commit titles.

# Use the following instructions when asked for comments on C# source files.

# WHAT TO DOCUMENT
* Types: classes, structs (including ref struct and readonly struct), records, interfaces, enums, delegates, attributes.
* Members: constructors, finalizers, methods, operators, properties (including indexers), events, and fields that are public/protected/internal.
* Fully document Private methods.
* Generic parameters: add a <typeparam> for each generic type parameter.
* Constraints: reflect practical effects inside <remarks>.
* Return values: add <returns> for all non-void methods.
* Properties: include a <value> describing what the property represents.
* Exceptions: add <exception cref="..."> entries for all guards and throw sites that are reasonably inferable.

# STYLE AND SYNTAX RULES (MANDATORY)

XML tag order for every documented symbol:
a) <summary> (one concise sentence stating what it is/does; imperative mood)
b) <remarks> (immediately after <summary>; deeper details, invariants, perf notes)
c) <typeparam> and <param> in declaration order
d) <returns> (mutually exclusive with "<value>")
e) <exception> (one tag per possible exception)
f) <value> (properties only and is to be used instead of "<returns>")
g) <example> (only when it adds clarity)

## Cross-references:
* Use <see langword="..."/> for language keywords: null, true, false, default, checked, unchecked, stackalloc, etc.
* Use <see cref="..."/> for types and members, including generics like List{T}, Dictionary{TKey,TValue}, ReadOnlySpan{T}, IMemoryOwner{T}, etc.
* Disambiguate overloaded members with full signatures, e.g., <see cref="SomeType.SomeMethod(int, string)"/>.
* NEVER use <see langword="..."/> inside an <exception> tag. Write the keyword as plain text instead.
  * Correct: <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
  * Incorrect: <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
  * This applies to <exception> only; <summary>, <remarks>, <param>, <returns>, and <value> still use <see langword="..."/> normally.
* Do not use smart quotes.

## Crefs in files that use ZLinq
* In a file with `using ZLinq;`, write every cref to ArgumentNullException as System.ArgumentNullException, and every cref to GC as System.GC. This applies to <exception cref>, <see cref>, and <seealso cref> alike. Other types, such as ArgumentException, keep the short form.
  * Correct: <exception cref="System.ArgumentNullException">Thrown when <paramref name="variableNames"/> is null.</exception>
  * Incorrect: <exception cref="ArgumentNullException">Thrown when <paramref name="variableNames"/> is null.</exception>
* ZLinq declares an internal ArgumentNullException in its netstandard2.0 build, and an internal GC in every target. Code can't see them, but cref lookup still finds them, so the short names are ambiguous. A type cref such as <see cref="GC"/> produces CS0419, and a member cref such as <see cref="GC.KeepAlive(object)"/> produces CS1574.
* Only the netstandard2.0 and net48 builds report the ArgumentNullException warning; the net10.0 builds don't. Keep the namespace even when one target builds cleanly.
* Code is not affected: `throw new ArgumentNullException(...)` compiles without the namespace. Files without `using ZLinq;` keep the short form.

# Language and tone (MANDATORY):
* American English, concise, active voice.
* MANDATORY - Use present tense!
* Prefer "Gets …", "Sets …", "Creates …", "Returns …".

# Code style assumptions (do not alter code to enforce these; just respect them):
* File-scoped namespaces.
* Using directives outside the namespace; prefer qualified using directives in nested scope.
* Modifier order: public, private, protected, internal, file, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async.
* Prefer regular constructors over primary constructors (do not convert forms).

# PUBLIC SURFACE HYGIENE (MANDATORY)
* When documenting public or protected types/members, do not reference internal or private types, members, or namespaces in text or cref. Avoid <see cref="InternalType"/> or <see cref="SomeType.InternalMember"/>.
* If behavior depends on internal implementation details, describe it generically without naming internal identifiers. Example: "Uses an internal cache to avoid repeated allocations" rather than naming the cache type.
* Examples must compile against the public surface only and must not construct or reference internal helpers.
* Do not expose internal exceptions or error codes; describe observable conditions using public concepts (e.g., "Throws an ArgumentException when the format is invalid.").
* Do not imply stability of non-public contracts; link only to public, stable symbols in <see cref="..."/>.

# CONTENT GUIDANCE
* Summaries: one crisp sentence about purpose (what), not implementation (how).
* Remarks: nontrivial behavior, invariants, concurrency and thread-safety, performance and allocation characteristics, ownership semantics for spans/buffers/pools, and edge cases. Use separate <para> blocks within <remarks> for clarity.
* Parameters: meaning, allowed ranges, preconditions; indicate null allowance with <see langword="null"/> when applicable.
* Exceptions: "Thrown when …" with the exact parameter name and condition where evident.
* Thread-safety: state clearly if members are thread-safe or not when inferable.
* Collections and spans: call out mutability, copying vs referencing, lifetime (especially for ref struct), and ownership/disposal expectations.
* Enums: each enum member should have a brief inline summary comment.
* Operators: summarize semantics and note overflow/checked behavior if it is implied by usage.
* Delegates: document the role of the callback and each parameter/return.
* Constructor comments should follow the standard .NET commenting phraseology of "Initializes a new <see cref/> instance...". If the constructor has parameters, those should be mentioned in the summary to avoid confusion with other constructors with different parameters (or none at all).

# DOC COMMENT PLACEMENT (MANDATORY)
* XML documentation comments (/// ...) must be the outermost block immediately preceding the documented symbol.
* If a symbol has one or more attribute declarations (e.g., [DebuggerStepThrough], [MethodImpl], [JsonPropertyName], etc.), place the XML documentation comments above the entire attribute list, not between attributes and the declaration.
* Required order for a documented declaration is:
1. /// XML doc comments
2. One or more attribute lines (if present)
3. The member/type declaration
* NEVER place/insert comments in lines between the attributes and the declaration.

## GENERICS AND CONSTRAINTS
* For each <typeparam name="T"> provide a brief semantic description (e.g., "The element type.").
* In <remarks>, restate practical effects of constraints (e.g., "When T is unmanaged, instances can be copied without boxing.").

## SPANS/UNSAFE/STACK SEMANTICS
* When code uses Span<T>, ReadOnlySpan<T>, Memory<T>, IMemoryOwner<T>, stackalloc, pinnable references, or unsafe code, describe lifetime and safety constraints in <remarks>.
* Mention slicing and bounds expectations where obvious.

# EXTENSION BLOCKS (MANDATORY)

A C# 14 `extension(...)` block is a documented symbol in its own right, not just a container. Document the block, then document each member inside it.

## The block declaration
* Every extension block gets its own /// doc comment placed immediately above the `extension` keyword.
* Required tags, in the standard order:
	a) <summary> - what the block's members do for the receiver type. Example: "Provides extension methods for <see cref="Span{T}"/> that allow for bounds-check-free element access and slicing."
	b) <remarks> - optional; behavior or invariants shared by every member in the block.
	c) <typeparam> - one per type parameter declared on the block itself (`extension<T>(...)`), in declaration order.
	d) <param> - one for the receiver parameter. This is MANDATORY. The receiver is documented here and nowhere else.
* Restate the practical effect of block-level constraints (`where TKey : notnull`, `allows ref struct`) inside the block's <remarks>, per GENERICS AND CONSTRAINTS.
* No <returns>, <value>, or <exception> on the block itself; those belong to individual members.

## Members inside the block
* A member documents only its own signature: <summary>, <remarks>, its own <typeparam> and <param>, <returns> (methods) or <value> (properties), and <exception>.
* NEVER add a <param> for the receiver on a member. The receiver is not one of that member's parameters; that tag belongs on the block.
* DO use <paramref name="receiver"/> inside a member's docs to refer to the receiver. The name resolves from the enclosing block.

## Cross-referencing an extension member
* Neither the pre-C# 14 static form nor the bare member signature resolves; both produce CS1574.
	* Incorrect: <see cref="BattleFlowExtensions.NewBattleFlowAsync(Orchestrator, Enemy, ICombatantAI)"/>
	* Incorrect: <see cref="NewBattleFlowAsync(Enemy, ICombatantAI)"/>
	* Correct: <see cref="BattleFlowExtensions.extension(Orchestrator).NewBattleFlowAsync(Enemy, ICombatantAI)"/>
* Form the cref as: containing static class, then `extension(ReceiverType)` - the receiver type only, no parameter name - then the member signature with its own parameter types. The receiver type is not repeated in that parameter list.

## Layout
* Group every member that shares a receiver type into a single block rather than splitting them across several.
* Separate consecutive blocks with a blank line so a block's doc comment never sits flush against the previous block's closing brace.

## Worked example

```csharp
/// <summary>
/// Provides alternate lookup helpers for string-keyed collections.
/// </summary>
public static class AlternateLookupExtensions
{
	/// <summary>
	/// Returns an alternate lookup that accepts <see cref="ReadOnlySpan{Char}"/> keys for a <see cref="Dictionary{TKey, TValue}"/>.
	/// </summary>
	/// <remarks>
	/// The returned lookup is a lightweight view over the source dictionary and does not copy its contents.
	/// </remarks>
	/// <typeparam name="TValue">The value type stored in the dictionary.</typeparam>
	/// <param name="dictionary">The dictionary to view through the alternate lookup. This value must not be <see langword="null"/>.</param>
	extension<TValue>(Dictionary<string, TValue> dictionary)
	{
		/// <summary>
		/// Returns an alternate lookup that accepts <see cref="ReadOnlySpan{Char}"/> keys.
		/// </summary>
		/// <returns>An alternate lookup for span-based key access.</returns>
		public Dictionary<string, TValue>.AlternateLookup<ReadOnlySpan<char>> AsAlternate()
		{
			return dictionary.GetAlternateLookup<ReadOnlySpan<char>>();
		}
	}
}
```

# PERFORMANCE NOTES
* If the implementation clearly aims to reduce allocations, branch mispredictions, or leverage pooling/vectorization, add a short "Performance:" paragraph inside <remarks> using <para><b>Performance:</b> ...</para>.

# EXAMPLES
* Include <example> with minimal, focused code only when it materially clarifies behavior. Omit otherwise.

# EDGE CASES
* Overloads: disambiguate cref targets with parameter lists.
* Indexers: use <param name="index"> and <value>.
* Records: document equality and identity semantics.
* Partial types/members: document as if other parts may not be visible; avoid contradictions.
* Avoid <inheritdoc/> unless the member is an override or interface implementation where inherited docs are fully sufficient and accurate.

# INFERENCE RULES
* Infer intent from names, guards, throws, and obvious contracts.
* If a parameter is validated against null, assert that it must not be <see langword="null"/>.
* If returning pooled or cached instances, state ownership and disposal rules.
* If some behavior cannot be determined with confidence, include a neutral line and add a TODO note inside <remarks> as a separate <para> (e.g., <para>TODO: Clarify behavior when X is null.</para>).

# VALIDATION CHECKLIST (APPLY BEFORE RETURNING)
* Every documented symbol has a <summary>; <remarks> is present when there is any nontrivial behavior.
* All <param> and <typeparam> entries exist and are in declaration order.
* <returns> is present for all non-void members.
* <value> is present for properties.
* Reasonable <exception> tags are included where guard conditions or throw sites are evident.
* All cross-references use <see cref="..."/> or <see langword="..."/> appropriately.
* No <exception> tag contains a <see langword="..."/> element; keywords there are written as plain text.
* <remarks> immediately follows <summary> everywhere.
* Every extension block has its own doc comment carrying a <param> for the receiver, and no member inside it re-documents that receiver.
* Every cref to an extension member uses the ContainingClass.extension(ReceiverType).Member(...) form.
* In files with `using ZLinq;`, every cref to ArgumentNullException or GC is written as System.ArgumentNullException or System.GC.
* No references (text or cref) to internal/private symbols for public/protected APIs.
* No smart quotes; no extra prose outside the updated code.