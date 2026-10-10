using ListFunctions.Extensions;
using ListFunctions.Internal;

#nullable enable

namespace ListFunctions.Validation;

/// <summary>
/// Validates that a cmdlet parameter's script block uses at least one of the specified variables.
/// </summary>
/// <remarks>
/// <para>
/// The script block passes when it references at least one of the named variables, such as <c>$_</c> or <c>$left</c>, or
/// a variable that holds one of the accepted arguments. The cmdlets pass their elements to a script block as its
/// arguments, in order, and each <c>$args</c> index that the constructor receives, such as <c>args[1]</c>, accepts the
/// argument at that index. Without a <c>param()</c> block, the script block reads that argument as <c>$args[1]</c>. With
/// one, PowerShell binds the arguments to the parameters in the order they're declared, whatever their attributes, and
/// <c>$args</c> holds only the arguments left over. So the argument at index 1 is the second parameter, or
/// <c>$args[0]</c> when the script block declares one parameter.
/// </para>
/// <para>
/// Names are compared without regard to case, against the variable path as it is written, so <c>$script:x</c> doesn't
/// match <c>x</c>. A named variable that the script block declares as a parameter counts only when the parameter receives
/// an accepted argument, because the parameter takes the place of the variable that the cmdlet defines.
/// </para>
/// <para>
/// Only the block that PowerShell runs counts: the <c>process</c> block when there is one, and otherwise the <c>end</c>
/// block. Variables in the <c>param()</c> block, in nested script blocks, and in functions that the script block calls,
/// splatted variables, the constant variables <c>$true</c>, <c>$false</c>, and <c>$null</c>, and a bare <c>$args</c> are
/// ignored. A function's script block, such as <c>${function:Test-It}</c>, is checked by its body, with the parameters
/// that the function declares either in parentheses or in a <c>param()</c> block. A script block that has no block to run
/// passes, because <see cref="IsScriptBlockAttribute"/> rejects it.
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
internal sealed class ValidateScriptVariableAttribute : ValidateArgumentsAttribute
{
	/// <summary>
	/// The name of PowerShell's automatic <c>$args</c> variable.
	/// </summary>
	/// <remarks>
	/// A constructor entry that starts with this name and ends with a bracketed index, such as <c>args[0]</c>, specifies
	/// the index of an accepted argument instead of a variable name.
	/// </remarks>
	public const string Args = "args";

	/// <summary>
	/// The indexes of the accepted arguments, or an empty slice when the constructor specified none.
	/// </summary>
	private readonly ArraySlice<int> _mustContainIndexes;

	/// <summary>
	/// The variable names that satisfy the check, or an empty slice when the constructor specified none.
	/// </summary>
	private readonly ArraySlice<string> _mustContainNames;

	/// <summary>
	/// Initializes a new instance of <see cref="ValidateScriptVariableAttribute"/> with the specified variable names and
	/// argument indexes, any one of which satisfies the check.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An entry such as <c>args[0]</c> accepts the argument at that index, in whichever variable the script block receives
	/// it, as the class remarks describe; every other entry is a variable name without the <c>$</c>, such as <c>_</c> or
	/// <c>left</c>. Names and indexes can be mixed in any order.
	/// </para>
	/// <para>
	/// The constructor reorders the elements of <paramref name="variableNames"/> in place and keeps a reference to the
	/// array. Don't change the array afterward.
	/// </para>
	/// </remarks>
	/// <param name="variableNames">The variable names and argument indexes to accept. This value must not be <see langword="null"/> or empty.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="variableNames"/> is null.</exception>
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
	/// The error message lists the variables that the script block can use: the accepted names that it doesn't declare as
	/// parameters, and then, for each accepted argument, the parameter that receives it, as the script block writes it, or
	/// the <c>$args</c> index that holds it. The names aren't necessarily in the order that the constructor received them.
	/// </remarks>
	/// <param name="arguments">The argument to validate. Only a <see cref="ScriptBlock"/> or a <see cref="string"/> is checked.</param>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. The method doesn't use it.</param>
	/// <exception cref="ParseException">Thrown when <paramref name="arguments"/> is a string that isn't a valid script.</exception>
	/// <exception cref="ValidationMetadataException">Thrown when the block of the script block that PowerShell runs uses none of the accepted variables.</exception>
	protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
	{
		if (arguments is string str)
		{
			arguments = ScriptBlock.Create(str);
		}

		// InvokeWithContext runs only the process block or, without one, the end block. [IsScriptBlock] rejects a script
		// block that has neither.
		if (arguments is not ScriptBlock block
			|| !block.TryGetBody(out ScriptBlockAst? body, out ReadOnlyCollection<ParameterAst>? parameters)
			|| (body.ProcessBlock ?? body.EndBlock) is not NamedBlockAst blockThatRuns)
		{
			return;
		}

		if (blockThatRuns.Find(ast => this.IsAccepted(ast, parameters), searchNestedScriptBlocks: false) is null)
		{
			throw new ValidationMetadataException(this.FormatMessage(parameters));
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
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="variableNames"/> is null.</exception>
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
	/// Determines whether the specified syntax tree node uses one of the accepted variables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A variable that <paramref name="parameters"/> declares is accepted when its parameter receives an accepted argument.
	/// Any other variable is accepted when its name is.
	/// </para>
	/// <para>
	/// An index expression on <c>$args</c> is accepted when its index is a non-negative constant <see cref="int"/>, such
	/// as the <c>0</c> in <c>$args[0]</c>, and the argument that it holds is accepted. An index that is a variable or an
	/// expression never matches.
	/// </para>
	/// </remarks>
	/// <param name="ast">The node to check.</param>
	/// <param name="parameters">
	/// The parameters that the script block declares, in the order it declares them, or <see langword="null"/> when it
	/// declares none.
	/// </param>
	/// <returns><see langword="true"/> if <paramref name="ast"/> uses an accepted variable; otherwise, <see langword="false"/>.</returns>
	private bool IsAccepted(Ast ast, ReadOnlyCollection<ParameterAst>? parameters)
	{
		if (ast is VariableExpressionAst varAst)
		{
			if (!CanMatch(varAst))
			{
				return false;
			}

			string name = varAst.VariablePath.UserPath;
			int position = IndexOfParameter(parameters, name);

			return position >= 0
				? ArraySlice.Contains(_mustContainIndexes, position)
				: ArraySlice.Contains(_mustContainNames, name, StringComparer.OrdinalIgnoreCase);
		}

		// The parameters receive the first arguments, so $args[i] holds the argument at index i plus the number of
		// parameters.
		return ast is IndexExpressionAst indexAst
			&& IsArgsIndexed(indexAst)
			&& indexAst.Index is ConstantExpressionAst { Value: int index }
			&& index >= 0
			&& ArraySlice.Contains(_mustContainIndexes, index + (parameters?.Count ?? 0));
	}

	/// <summary>
	/// Determines whether the specified variable reference can match an accepted name or parameter.
	/// </summary>
	/// <remarks>
	/// The method rejects the constant variables <c>$true</c>, <c>$false</c>, and <c>$null</c>, splatted variables such
	/// as <c>@params</c>, drive-qualified paths that aren't variables, such as <c>$env:Path</c>, and <c>$args</c>.
	/// </remarks>
	/// <param name="varAst">The variable reference to check.</param>
	/// <returns><see langword="true"/> if <paramref name="varAst"/> can match an accepted name or parameter; otherwise, <see langword="false"/>.</returns>
	private static bool CanMatch(VariableExpressionAst varAst)
	{
		return !varAst.IsConstantVariable()
			&& !varAst.Splatted
			&& varAst.VariablePath.IsVariable
			&& !Args.Equals(varAst.VariablePath.UserPath, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines whether the specified index expression indexes the <c>$args</c> variable, such as <c>$args[0]</c>.
	/// </summary>
	/// <remarks>
	/// The method checks only the target of the index expression. <see cref="IsAccepted(Ast, ReadOnlyCollection{ParameterAst})"/>
	/// checks the index itself.
	/// </remarks>
	/// <param name="indexAst">The index expression to check.</param>
	/// <returns><see langword="true"/> if the target of <paramref name="indexAst"/> is <c>$args</c>; otherwise, <see langword="false"/>.</returns>
	private static bool IsArgsIndexed(IndexExpressionAst indexAst)
	{
		return indexAst.Target is VariableExpressionAst varAst
			&& varAst.VariablePath.IsVariable
			&& Args.Equals(varAst.VariablePath.UserPath, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Returns the index of the parameter with the specified name among the specified parameters.
	/// </summary>
	/// <remarks>
	/// Names are compared by ordinal comparison that ignores case, the way PowerShell compares variable names.
	/// </remarks>
	/// <param name="parameters">The parameters that the script block declares, in the order it declares them, or <see langword="null"/>.</param>
	/// <param name="name">The variable name to look for, without the <c>$</c>.</param>
	/// <returns>The zero-based index of the parameter named <paramref name="name"/>, or -1 when there is none.</returns>
	private static int IndexOfParameter(ReadOnlyCollection<ParameterAst>? parameters, string name)
	{
		if (parameters is not null)
		{
			for (int i = 0; i < parameters.Count; i++)
			{
				if (name.Equals(parameters[i].Name.VariablePath.UserPath, StringComparison.OrdinalIgnoreCase))
				{
					return i;
				}
			}
		}

		return -1;
	}

	/// <summary>
	/// Creates the message that reports a script block that uses none of the accepted variables.
	/// </summary>
	/// <remarks>
	/// The message lists the variables that the script block can use: each accepted name that it doesn't declare as a
	/// parameter, and then, for each accepted argument, the parameter that receives it, as the script block writes it, such
	/// as <c>$b</c> or <c>${the right}</c>, or the <c>$args</c> index that holds it.
	/// </remarks>
	/// <param name="parameters">
	/// The parameters that the script block declares, in the order it declares them, or <see langword="null"/> when it
	/// declares none.
	/// </param>
	/// <returns>The message, such as "The script block must use at least one of these variables: $y, $right, $b."</returns>
	private string FormatMessage(ReadOnlyCollection<ParameterAst>? parameters)
	{
		int parameterCount = parameters?.Count ?? 0;
		var variables = new List<string>(_mustContainNames.Length + _mustContainIndexes.Length);

		foreach (string name in _mustContainNames)
		{
			if (IndexOfParameter(parameters, name) < 0)
			{
				variables.Add("$" + name);
			}
		}

		foreach (int index in _mustContainIndexes)
		{
			variables.Add(index < parameterCount
				? parameters![index].Name.Extent.Text
				: $"$args[{index - parameterCount}]");
		}

		return string.Concat("The script block must use at least one of these variables: ", string.Join(", ", variables), ".");
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
