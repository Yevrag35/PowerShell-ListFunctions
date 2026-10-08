namespace ListFunctions.Completion;

/// <summary>
/// Completes the type names that a parameter with the <see cref="Validation.ArgumentToTypeTransformAttribute"/> accepts.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell completes a type name only in a type literal, such as <c>[gu</c>, where it offers type accelerators, types,
/// and namespaces. The completer writes the argument as a type literal and asks
/// PowerShell to complete it through <see cref="CommandCompletion.CompleteInput(string, int, Hashtable)"/>, so it offers the
/// same names, and it completes a type argument of a generic type too, such as the <c>in</c> of
/// <c>System.Collections.Generic.List[in</c>. It can't call <see cref="CompletionCompleters.CompleteType(string)"/>
/// instead, which throws a <see cref="NullReferenceException"/> in Windows PowerShell 5.1.
/// </para>
/// <para>
/// Each completion replaces the whole argument and keeps the form it's written in: its quotes, and the text before the name
/// that it completes, such as <c>[</c> or <c>System.Collections.Generic.List[</c>. A type closes the brackets and the quote
/// that are still open, so <c>[gu</c> completes to <c>[guid]</c>, and <c>'gu</c> to <c>'guid'</c>. A namespace, and a
/// generic type without its type arguments, such as <c>System.Collections.Generic.List</c>, leave them open, so that the
/// name can go on. PowerShell passes the completer a string argument between straight quotes, whichever quotation marks
/// it's written with, so a completion has straight quotes too.
/// </para>
/// <para>
/// PowerShell splits an argument that isn't in quotes at each comma, and passes the completer only the part after the last
/// comma. In <c>[System.Collections.Generic.Dictionary[string,in</c>, the completer completes <c>in</c> as a type name of its
/// own, without the brackets that the earlier part opens.
/// </para>
/// <para>
/// When the argument has no name to complete, such as when nothing is typed, the completer returns no completions, as a type
/// literal with nothing after its bracket gets none. It never returns <see langword="null"/>, which would make PowerShell
/// offer file names instead.
/// </para>
/// <para>
/// PowerShell's completion of a type literal also offers names that PowerShell can't read as a type name, such as the
/// nested types that the compiler generates for a C# 14 extension block, whose names contain characters such as
/// <c>&lt;</c> and <c>$</c>. The completer leaves them out, because they would break the command.
/// </para>
/// <para>
/// The completer keeps no state, so it is thread-safe. It must run on a thread whose default runspace is set, as PowerShell's
/// own completion does.
/// </para>
/// </remarks>
internal sealed class TypeNameCompleter : IArgumentCompleter
{
	/// <summary>
	/// Returns the completions of a type name argument.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method removes the quotes from <paramref name="wordToComplete"/> and the closing brackets from its end, adds an
	/// opening bracket unless it starts with one, and completes the result as a type literal whose cursor is at its end. It
	/// keeps only the type and namespace completions that PowerShell can read as a type name, in the order that PowerShell
	/// sorts them.
	/// </para>
	/// <para>
	/// A type completion closes the brackets and the quote that are still open. PowerShell lists a generic type without its
	/// type arguments as the type's name followed by <c>&lt;&gt;</c>, such as <c>List&lt;&gt;</c>, and such a type, like a
	/// namespace, leaves them open.
	/// </para>
	/// </remarks>
	/// <param name="commandName">The name of the command whose argument is completed. The method doesn't use it.</param>
	/// <param name="parameterName">The name of the parameter whose argument is completed. The method doesn't use it.</param>
	/// <param name="wordToComplete">
	/// The argument as it's written, such as <c>[gu</c>, or, for a string, its value between straight quotes, such as
	/// <c>'gu'</c>. PowerShell passes a string that way whichever quotation marks it's written with, and adds the closing
	/// quote when it's missing. The value can be <see langword="null"/>, which completes the same as an empty string.
	/// </param>
	/// <param name="commandAst">The syntax tree of the command whose argument is completed. The method doesn't use it.</param>
	/// <param name="fakeBoundParameters">The arguments that PowerShell binds to the other parameters. The method doesn't use them.</param>
	/// <returns>
	/// The completions, each of which replaces the whole argument. The collection is empty, never <see langword="null"/>, when
	/// there's no type name to complete.
	/// </returns>
	public IEnumerable<CompletionResult> CompleteArgument(
		string? commandName,
		string? parameterName,
		string? wordToComplete,
		CommandAst? commandAst,
		IDictionary? fakeBoundParameters)
	{
		string text = wordToComplete ?? string.Empty;
		string quote = string.Empty;
		if (text.Length > 1 && text[0] is '\'' or '"' && text[^1] == text[0])
		{
			quote = text.Substring(0, 1);
			text = text.Substring(1, text.Length - 2);
		}

		// A type literal completes only the name that ends at the cursor, so the closing brackets come off, and each
		// completion closes the brackets it needs again.
		text = text.TrimEnd(']');

		string bracket = text.StartsWith("[", StringComparison.Ordinal) ? string.Empty : "[";
		string literal = bracket + text;
		CommandCompletion completion = CommandCompletion.CompleteInput(literal, literal.Length, options: null);

		// A type name that PowerShell completes comes after the literal's opening bracket and ends at the cursor.
		if (completion.CompletionMatches.Count == 0
			|| completion.ReplacementIndex < 1
			|| completion.ReplacementIndex + completion.ReplacementLength != literal.Length)
		{
			return [];
		}

		string before = text.Substring(0, completion.ReplacementIndex - bracket.Length);
		List<CompletionResult> results = new(completion.CompletionMatches.Count);
		foreach (CompletionResult match in completion.CompletionMatches)
		{
			if (match.ResultType is not (CompletionResultType.Type or CompletionResultType.Namespace)
				|| !IsTypeName(match.CompletionText))
			{
				continue;
			}

			// PowerShell lists a generic type without its type arguments as Name<>, such as List<>.
			string completed = before + match.CompletionText;
			bool isCompleteType = match.ResultType == CompletionResultType.Type
				&& !match.ListItemText.EndsWith("<>", StringComparison.Ordinal);

			completed = isCompleteType
				? string.Concat(quote, completed, new string(']', CountOpenBrackets(completed)), quote)
				: quote + completed;

			results.Add(new CompletionResult(completed, match.ListItemText, match.ResultType, match.ToolTip));
		}

		return results;
	}

	/// <summary>
	/// Returns the number of opening brackets in the specified text that no closing bracket after them matches.
	/// </summary>
	/// <param name="text">The text to count in. This value must not be <see langword="null"/>.</param>
	/// <returns>The number of opening brackets that are still open at the end of <paramref name="text"/>.</returns>
	private static int CountOpenBrackets(string text)
	{
		int open = 0;
		foreach (char c in text)
		{
			if (c == '[')
			{
				open++;
			}
			else if (c == ']' && open > 0)
			{
				open--;
			}
		}

		return open;
	}

	/// <summary>
	/// Determines whether PowerShell reads the specified name as a type name.
	/// </summary>
	/// <remarks>
	/// The method parses the name in brackets, as a type literal, and the name passes when the literal parses without errors
	/// and is a single type literal. It doesn't resolve the type, because a namespace and a generic type without its type
	/// arguments don't resolve to a type, and the completer offers them so that the name can go on.
	/// </remarks>
	/// <param name="name">The name to check. This value must not be <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if PowerShell reads <paramref name="name"/> as a type name; otherwise, <see langword="false"/>.</returns>
	private static bool IsTypeName(string name)
	{
		string literal = "[" + name + "]";
		Ast ast = Parser.ParseInput(literal, out _, out ParseError[] errors);

		return errors.Length == 0
			&& ast.Find(static x => x is TypeExpressionAst, false) is TypeExpressionAst expression
			&& expression.Extent.Text.Length == literal.Length;
	}
}
