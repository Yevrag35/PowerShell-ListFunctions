using ListFunctions.Completion;

namespace ListFunctions.Engine.Tests.Completion;

public sealed class TypeNameCompleterTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public TypeNameCompleterTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	// PowerShell passes the whole argument, so [gu] arrives as it's written when the cursor is anywhere in it.
	[Theory]
	[InlineData("gu", "guid")]
	[InlineData("[gu", "[guid]")]
	[InlineData("[gu]", "[guid]")]
	public void CompleteArgument_CompletesATypeNameWithOrWithoutItsBracket(string word, string expected)
	{
		using RunspaceScope scope = _runspace.Enter();

		Assert.Contains(expected, Complete(word));
	}

	// PowerShell passes a string argument as its value between straight quotes, and adds the closing quote when it's
	// missing, so 'gu arrives as 'gu'.
	[Theory]
	[InlineData('\'')]
	[InlineData('"')]
	public void CompleteArgument_KeepsTheQuotesOfAString(char quote)
	{
		using RunspaceScope scope = _runspace.Enter();

		Assert.Contains($"{quote}guid{quote}", Complete($"{quote}gu{quote}"));
		Assert.Contains($"{quote}[guid]{quote}", Complete($"{quote}[gu{quote}"));
	}

	// PowerShell splits an argument that isn't in quotes at each comma, so only a quoted argument passes the comma.
	[Theory]
	[InlineData("System.Collections.Generic.List[in", "System.Collections.Generic.List[int]")]
	[InlineData("[System.Collections.Generic.List[in", "[System.Collections.Generic.List[int]]")]
	[InlineData("'[System.Collections.Generic.Dictionary[string, in'", "'[System.Collections.Generic.Dictionary[string, int]]'")]
	public void CompleteArgument_CompletesATypeArgumentAndClosesEveryBracket(string word, string expected)
	{
		using RunspaceScope scope = _runspace.Enter();

		Assert.Contains(expected, Complete(word));
	}

	[Theory]
	[InlineData("[System.Coll", "[System.Collections")]
	[InlineData("'System.Coll'", "'System.Collections")]
	public void CompleteArgument_LeavesANamespaceOpen(string word, string expected)
	{
		using RunspaceScope scope = _runspace.Enter();

		Assert.Equal(expected, Assert.Single(Complete(word)));
	}

	[Theory]
	[InlineData("[System.Collections.Generic.Lis", "[System.Collections.Generic.List")]
	[InlineData("'System.Collections.Generic.Lis'", "'System.Collections.Generic.List")]
	public void CompleteArgument_LeavesAGenericTypeWithoutItsTypeArgumentsOpen(string word, string expected)
	{
		using RunspaceScope scope = _runspace.Enter();

		Assert.Equal(expected, Assert.Single(Complete(word)));
	}

	// A type literal with nothing after its bracket gets no completions either. A null result would make PowerShell offer
	// file names.
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("''")]
	[InlineData("[")]
	[InlineData("[int[]]")]
	public void CompleteArgument_ReturnsNoCompletionsWhenThereIsNoTypeNameToComplete(string? word)
	{
		using RunspaceScope scope = _runspace.Enter();
		IEnumerable<CompletionResult> completions = new TypeNameCompleter().CompleteArgument("New-List", "GenericType", word, commandAst: null, fakeBoundParameters: null);

		Assert.NotNull(completions);
		Assert.Empty(completions);
	}

	// PowerShell's own completion offers the nested types that the compiler generates for the extension block in
	// GeneratedNestedTypes, but it can't parse their names, which contain < and $, as type names.
	[Fact]
	public void CompleteArgument_LeavesOutNamesThatPowerShellCannotReadAsTypeNames()
	{
		using RunspaceScope scope = _runspace.Enter();
		string word = typeof(GeneratedNestedTypes).FullName + "+";

		Assert.NotEmpty(CommandCompletion.CompleteInput("[" + word, word.Length + 1, options: null).CompletionMatches);
		Assert.Empty(Complete(word));
	}

	/// <summary>
	/// Completes the specified argument of <c>New-List -GenericType</c> and returns the text of each completion.
	/// </summary>
	/// <remarks>
	/// The current thread must be in the fixture's runspace, through <see cref="RunspaceFixture.Enter"/>.
	/// </remarks>
	/// <param name="word">The argument as PowerShell passes it to the completer.</param>
	/// <returns>The text of each completion, in the order that the completer returns them.</returns>
	private static string[] Complete(string word)
	{
		var completer = new TypeNameCompleter();

		return [.. completer.CompleteArgument("New-List", "GenericType", word, commandAst: null, fakeBoundParameters: null).Select(completion => completion.CompletionText)];
	}
}
