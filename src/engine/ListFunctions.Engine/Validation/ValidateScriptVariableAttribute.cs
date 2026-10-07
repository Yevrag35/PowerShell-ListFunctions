using ListFunctions.Internal;
using ZLinq;

#nullable enable

namespace ListFunctions.Validation;

/// <summary>
/// Validates that a cmdlet parameter's script block uses at least one of the specified variables.
/// </summary>
/// <remarks>
/// <para>
/// The script block passes when it references at least one of the named variables, such as <c>$_</c> or <c>$left</c>, or
/// indexes <c>$args</c> with one of the specified constant indexes, such as <c>$args[0]</c>. Names are compared without
/// regard to case, against the variable path as it is written, so <c>$script:x</c> doesn't match <c>x</c>.
/// </para>
/// <para>
/// Only the script block's own body counts. Variables in nested script blocks, splatted variables, the constant
/// variables <c>$true</c>, <c>$false</c>, and <c>$null</c>, and a bare <c>$args</c> are ignored.
/// </para>
/// <para>
/// A <see cref="string"/> argument is parsed as a script block, without running it, before the check. An argument of
/// any other type, including <see langword="null"/>, passes.
/// </para>
/// <para>
/// Apply the attribute more than once to require one variable from each of several groups, such as one for the left
/// operand and one for the right. Instances don't change after construction, so validation is thread-safe.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class ValidateScriptVariableAttribute : ValidateArgumentsAttribute
{
	/// <summary>
	/// The name of PowerShell's automatic <c>$args</c> variable.
	/// </summary>
	/// <remarks>
	/// A constructor entry that starts with this name and ends with a bracketed index, such as
	/// <c>args[0]</c>, specifies an index of <c>$args</c> instead of a variable name.
	/// </remarks>
	public const string Args = "args";

	/// <summary>
	/// The <c>$args</c> indexes that satisfy the check, or an empty slice when the constructor specified none.
	/// </summary>
	private readonly ArraySlice<int> _mustContainIndexes;

	/// <summary>
	/// The variable names that satisfy the check, or an empty slice when the constructor specified none.
	/// </summary>
	private readonly ArraySlice<string> _mustContainNames;

	/// <summary>
	/// Initializes a new instance of <see cref="ValidateScriptVariableAttribute"/> with the specified variable names and
	/// <c>$args</c> indexes, any one of which satisfies the check.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An entry such as <c>args[0]</c> specifies an index of <c>$args</c>; every other entry is a variable name without
	/// the <c>$</c>, such as <c>_</c> or <c>left</c>. Names and indexes can be mixed in any order.
	/// </para>
	/// <para>
	/// The constructor reorders the elements of <paramref name="variableNames"/> in place and keeps a reference to the
	/// array. Don't change the array afterward.
	/// </para>
	/// </remarks>
	/// <param name="variableNames">The variable names and <c>$args</c> indexes to accept. This value must not be <see langword="null"/> or empty.</param>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="variableNames"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="variableNames"/> is empty.</exception>
	public ValidateScriptVariableAttribute(params string[] variableNames)
	{
		int indexCount = ParseIndexes(variableNames, out int[]? indexes);
		int nameCount = variableNames.Length - indexCount;

		_mustContainIndexes = indexCount != 0
			? new(indexes!, 0, indexCount)
			: [];

		_mustContainNames = nameCount != 0
			? new(variableNames, 0, nameCount)
			: [];
	}

	/// <summary>
	/// Validates that the specified argument, when it is a script block or a string, uses at least one of the accepted
	/// variables.
	/// </summary>
	/// <remarks>
	/// The error message lists the accepted variable names but not the accepted <c>$args</c> indexes, and it doesn't
	/// necessarily list the names in the order that the constructor received them.
	/// </remarks>
	/// <param name="arguments">The argument to validate. Only a <see cref="ScriptBlock"/> or a <see cref="string"/> is checked.</param>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. The method doesn't use it.</param>
	/// <exception cref="ParseException">Thrown when <paramref name="arguments"/> is a string that isn't a valid script.</exception>
	/// <exception cref="ValidationMetadataException">Thrown when the script block references none of the accepted variable names and indexes none of the accepted <c>$args</c> indexes.</exception>
	protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
	{
		if (arguments is string str)
		{
			arguments = ScriptBlock.Create(str);
		}

		if (arguments is not ScriptBlock block)
		{
			return;
		}

		if (!IsAllValid(block, _mustContainNames, _mustContainIndexes))
		{
			const string message = "At least one of the following variables must be included in the script block: ";
			const string sep = ", $";
			string vars = string.Join(
				separator: sep,
#if NET9_0_OR_GREATER
				values: _mustContainNames.AsSpan()
#else
				values: (IEnumerable<string>)_mustContainNames
#endif
			);
			throw new ValidationMetadataException(string.Concat(message, "$", vars));
		}
	}

	/// <summary>
	/// Separates the <c>$args</c> index entries in the specified array from the variable names, and parses the indexes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method partitions <paramref name="variableNames"/> in place: when it returns, the variable names come first and
	/// the index entries, such as <c>args[0]</c>, come last. The partition doesn't keep the original order within either
	/// group.
	/// </para>
	/// <para>
	/// The method allocates <paramref name="indexes"/> only when it finds an index entry. The array can be longer than
	/// the number of entries found; only the first elements, up to the return value, hold indexes.
	/// </para>
	/// </remarks>
	/// <param name="variableNames">The entries to partition. This value must not be <see langword="null"/> or empty.</param>
	/// <param name="indexes">
	/// When this method returns, contains the parsed indexes in the order that the method found them, or
	/// <see langword="null"/> when there are no index entries.
	/// </param>
	/// <returns>The number of index entries, which is also the number of entries at the end of <paramref name="variableNames"/> that aren't variable names.</returns>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="variableNames"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="variableNames"/> is empty.</exception>
	private static int ParseIndexes(string[]? variableNames, out int[]? indexes)
	{
		ArgumentNullException.ThrowIfNull(variableNames);
		if (variableNames.Length == 0)
		{
			throw new ArgumentException("Must contain at least 1 variable name.", nameof(variableNames));
		}

		int left = 0;
		int right = variableNames.Length - 1;

		indexes = null;
		int parsedCount = 0;

		// Invariant:
		// [0, left)     => non-parsable strings
		// (right, end]  => parsable strings (moved to the back)
		// [left, right] => unknown / not yet processed
		while (left <= right)
		{
			string current = variableNames[left];

			if (!TryParseIndexFromName(current, out int value))
			{
				// Non-parsable: stays in the front partition.
				left++;
			}
			else
			{
				// Parsable: goes to the back partition, and we collect the int value.

				// Lazy allocation with a tighter upper bound:
				//   - left elements are *known* non-parsable,
				//   - so at most (buffer.Length - left) elements can ever be parsable.
				int arrayLength = variableNames.Length - left;
				Debug.Assert(arrayLength > 0, "The array length should be greater than zero here.");
				indexes ??= new int[arrayLength];

				indexes[parsedCount++] = value;

				// Swap the current element with the element at 'right'
				(variableNames[right], variableNames[left]) = (variableNames[left], variableNames[right]);

				right--;
				// Do NOT increment 'left' here – the swapped-in element at buffer[left]
				// still needs to be examined.
			}
		}

		return parsedCount;
	}

	/// <summary>
	/// Determines whether the specified script block references at least one of the accepted variable names or indexes
	/// <c>$args</c> with at least one of the accepted indexes.
	/// </summary>
	/// <remarks>
	/// The method searches only the script block's own body, not its nested script blocks. It looks for <c>$args</c>
	/// index expressions only when <paramref name="mustContainIndexes"/> isn't empty.
	/// </remarks>
	/// <param name="block">The script block to check. This value must not be <see langword="null"/>.</param>
	/// <param name="mustContainNames">The accepted variable names, without the <c>$</c>. This slice can be empty.</param>
	/// <param name="mustContainIndexes">The accepted <c>$args</c> indexes. When this slice is empty, only variable names are checked.</param>
	/// <returns><see langword="true"/> if <paramref name="block"/> uses at least one accepted variable or index; otherwise, <see langword="false"/>.</returns>
	private static bool IsAllValid(ScriptBlock block, ArraySlice<string> mustContainNames, ArraySlice<int> mustContainIndexes)
	{
		bool allowsArgs = mustContainIndexes.Length != 0;

		IEnumerable<Ast> asts = allowsArgs
			? block.Ast.FindAll(searchNestedScriptBlocks: false, predicate: x => IsVariableAst(x) || IsArgsIndexed(x))
			: block.Ast.FindAll(searchNestedScriptBlocks: false, predicate: IsVariableAst);

		return allowsArgs
			? IsValidWithIndexes(asts, mustContainNames, mustContainIndexes!)
			: IsValidNoIndexes(asts, mustContainNames!);
	}

	/// <summary>
	/// Determines whether the specified syntax tree node is a variable reference that can match an accepted name.
	/// </summary>
	/// <remarks>
	/// The method rejects the constant variables <c>$true</c>, <c>$false</c>, and <c>$null</c>, splatted variables such
	/// as <c>@params</c>, drive-qualified paths that aren't variables, such as <c>$env:Path</c>, and <c>$args</c>.
	/// </remarks>
	/// <param name="ast">The node to check.</param>
	/// <returns><see langword="true"/> if <paramref name="ast"/> is a variable reference that can match an accepted name; otherwise, <see langword="false"/>.</returns>
	private static bool IsVariableAst(Ast ast)
	{
		return ast is VariableExpressionAst varAst
			&& !varAst.IsConstantVariable()
			&& !varAst.Splatted
			&& varAst.VariablePath.IsVariable
			&& !Args.Equals(varAst.VariablePath.UserPath, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines whether the specified syntax tree node indexes the <c>$args</c> variable, such as <c>$args[0]</c>.
	/// </summary>
	/// <remarks>
	/// The method checks only the target of the index expression. <see cref="IsValidWithIndexes(IEnumerable{Ast}, ArraySlice{string}, ArraySlice{int})"/>
	/// checks the index itself.
	/// </remarks>
	/// <param name="ast">The node to check.</param>
	/// <returns><see langword="true"/> if <paramref name="ast"/> is an index expression whose target is <c>$args</c>; otherwise, <see langword="false"/>.</returns>
	private static bool IsArgsIndexed(Ast ast)
	{
		return ast is IndexExpressionAst indexAst
			&& indexAst.Target is VariableExpressionAst varAst
			&& varAst.VariablePath.IsVariable
			&& Args.Equals(varAst.VariablePath.UserPath, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines whether any of the specified syntax tree nodes references an accepted variable name or indexes
	/// <c>$args</c> with an accepted index.
	/// </summary>
	/// <remarks>
	/// An index matches only when it is a constant <see cref="int"/>, such as the <c>0</c> in <c>$args[0]</c>. An index
	/// that is a variable or an expression never matches.
	/// </remarks>
	/// <param name="asts">The variable references and <c>$args</c> index expressions to search.</param>
	/// <param name="anyNames">The accepted variable names, compared by ordinal comparison that ignores case. This slice can be empty.</param>
	/// <param name="orAnyIndexes">The accepted <c>$args</c> indexes.</param>
	/// <returns>
	/// <see langword="true"/> if a node is a variable reference whose name is in <paramref name="anyNames"/>, or an index
	/// expression whose constant index is in <paramref name="orAnyIndexes"/>; otherwise, <see langword="false"/>.
	/// </returns>
	private static bool IsValidWithIndexes(IEnumerable<Ast> asts, ArraySlice<string> anyNames, ArraySlice<int> orAnyIndexes)
	{
		bool isNotNull = anyNames.Length > 0;
		foreach (Ast ast in asts.AsValueEnumerable())
		{
			if (ast is VariableExpressionAst varAst && isNotNull && ArraySlice.Contains(anyNames, varAst.VariablePath.UserPath, StringComparer.OrdinalIgnoreCase))
			{
				return true;
			}
			else if (ast is IndexExpressionAst indexAst && indexAst.Index is ConstantExpressionAst constAst && constAst.Value is int index
				&&
				ArraySlice.Contains(orAnyIndexes, index))
			{
				return true;
			}
		}

		return false;
	}
	/// <summary>
	/// Determines whether any of the specified variable references uses an accepted variable name.
	/// </summary>
	/// <remarks>
	/// Every node in <paramref name="asts"/> must be a <see cref="VariableExpressionAst"/>. The method casts each node,
	/// so any other node type causes an <see cref="InvalidCastException"/>.
	/// </remarks>
	/// <param name="asts">The variable references to search.</param>
	/// <param name="anyNames">The accepted variable names, compared by ordinal comparison that ignores case.</param>
	/// <returns><see langword="true"/> if at least one node references a name in <paramref name="anyNames"/>; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="InvalidCastException">Thrown when <paramref name="asts"/> contains a node that isn't a variable reference.</exception>
	private static bool IsValidNoIndexes(IEnumerable<Ast> asts, ArraySlice<string> anyNames)
	{
		foreach (VariableExpressionAst varAst in asts.AsValueEnumerable())
		{
			if (ArraySlice.Contains(anyNames, varAst.VariablePath.UserPath, StringComparer.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Attempts to parse a <c>$args</c> index from a constructor entry such as <c>args[2]</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The entry must start with <see cref="Args"/>, compared without regard to case, and be at least three characters
	/// longer than it. The rest of the entry, with one pair of enclosing brackets removed, must parse as a non-negative
	/// integer.
	/// </para>
	/// <para>
	/// Because of the length check, an entry without brackets parses only when its index has at least three digits, such
	/// as <c>args100</c>, so <c>args1</c> is treated as a variable name.
	/// </para>
	/// </remarks>
	/// <param name="name">The constructor entry to parse. This value must not be <see langword="null"/>.</param>
	/// <param name="parsedIndex">When this method returns, contains the parsed index if parsing succeeds; otherwise, -1.</param>
	/// <returns><see langword="true"/> if <paramref name="name"/> specifies a non-negative <c>$args</c> index; otherwise, <see langword="false"/>.</returns>
	private static bool TryParseIndexFromName(string name, out int parsedIndex)
	{
		if (name.Length < Args.Length + 3 || !name.StartsWith(Args, StringComparison.OrdinalIgnoreCase))
		{
			parsedIndex = -1;
			return false;
		}

#if NETCOREAPP
		ReadOnlySpan<char> span = name.AsSpan(Args.Length);
		if (span[0] == '[' && span[^1] == ']')
		{
			span = span.Slice(1, span.Length - 2);
		}

		if (int.TryParse(span, out int index) && index >= 0)
		{
			parsedIndex = index;
			return true;
		}
#else
		string trimmed = name[Args.Length] == '[' && name[name.Length - 1] == ']'
			? name.Substring(Args.Length + 1, name.Length - Args.Length - 2)
			: name.Substring(Args.Length);

		if (int.TryParse(trimmed, out int index) && index >= 0)
		{
			parsedIndex = index;
			return true;
		}
#endif

		parsedIndex = -1;
		return false;
	}
}
