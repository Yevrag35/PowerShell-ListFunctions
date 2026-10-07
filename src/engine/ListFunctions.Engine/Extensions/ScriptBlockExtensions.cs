namespace ListFunctions.Extensions;

/// <summary>
/// Provides helpers that validate script blocks and invoke them with injected variables.
/// </summary>
/// <remarks>
/// A script block runs only on a thread whose <see cref="System.Management.Automation.Runspaces.Runspace.DefaultRunspace"/> is set. Callers of the invoke methods must
/// make sure a runspace is available on the current thread.
/// </remarks>
internal static class ScriptBlockExtensions
{
	/// <summary>
	/// The message that reports a script block that <see cref="IsProperScriptBlock(ScriptBlock)"/> rejects.
	/// </summary>
	/// <remarks>
	/// The message states the rule instead of the reason that a particular script block breaks it. Windows PowerShell 5.1
	/// has no <c>clean</c> blocks, so only the .NET 10 build's message mentions one.
	/// </remarks>
	internal const string ImproperScriptBlockMessage =
#if NETCOREAPP
		"The script block must contain at least one statement, and it can't have a begin block, a clean block, or both a process block and an end block.";
#else
		"The script block must contain at least one statement, and it can't have a begin block, or both a process block and an end block.";
#endif

	/// <summary>
	/// Determines whether a script block has a body that can be invoked as a comparer or predicate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The module runs these script blocks with
	/// <see cref="ScriptBlock.InvokeWithContext(Dictionary{string, ScriptBlock}, List{PSVariable}, object[])"/>, which runs a
	/// single named block: the <c>process</c> block when there is one, and otherwise the <c>end</c> block, which holds the
	/// statements of a script block without named blocks. It refuses a script block that has a <c>begin</c> block, a
	/// <c>clean</c> block, or both a <c>process</c> block and an <c>end</c> block.
	/// </para>
	/// <para>
	/// The method returns <see langword="true"/> when <c>InvokeWithContext</c> can run the script block and the block that
	/// it runs contains at least one statement. Windows PowerShell 5.1 has no <c>clean</c> blocks, so only the .NET 10 build
	/// looks for one.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block to check. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="scriptBlock"/> has a body that can be invoked; otherwise, <see langword="false"/>,
	/// including when its syntax tree is not a <see cref="ScriptBlockAst"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	internal static bool IsProperScriptBlock(this ScriptBlock scriptBlock)
	{
		ArgumentNullException.ThrowIfNull(scriptBlock);

		if (scriptBlock.Ast is not ScriptBlockAst scriptAst || scriptAst.BeginBlock is not null)
		{
			return false;
		}

#if NETCOREAPP
		if (scriptAst.CleanBlock is not null)
		{
			return false;
		}
#endif

		if (scriptAst.ProcessBlock is not null)
		{
			return scriptAst.EndBlock is null && scriptAst.ProcessBlock.Statements.Count != 0;
		}

		return scriptAst.EndBlock is not null && scriptAst.EndBlock.Statements.Count != 0;
	}

	// PowerShell doesn't copy the args array: a script block without a param block gets that array as $args. Pass a
	// new array on every call to the methods below, because a reused one would change under a script block that keeps
	// $args.
	/// <summary>
	/// Invokes a script block with the specified variables and arguments, and converts its first output object.
	/// </summary>
	/// <remarks>
	/// Exceptions that the script block throws propagate to the caller. Exceptions that <paramref name="selectAs"/> throws
	/// are caught, and the method returns <paramref name="defaultIfNull"/> instead.
	/// </remarks>
	/// <typeparam name="T">The type to convert the first output object to.</typeparam>
	/// <param name="scriptBlock">The script block to invoke.</param>
	/// <param name="variables">The variables to define in the script block's scope, such as <c>$_</c> or <c>$left</c> and <c>$right</c>.</param>
	/// <param name="args">
	/// The arguments to pass to the script block. PowerShell does not copy this array, so pass a new array on every call.
	/// </param>
	/// <param name="selectAs">The function that converts the unwrapped first output object to <typeparamref name="T"/>.</param>
	/// <param name="defaultIfNull">The value to return when the script block outputs nothing or the conversion fails.</param>
	/// <returns>The converted first output object; otherwise, <paramref name="defaultIfNull"/>.</returns>
	/// <exception cref="RuntimeException">Thrown when the script block throws a terminating error.</exception>
	[return: NotNullIfNotNull(nameof(defaultIfNull))]
	internal static T InvokeWithContext<T>(this ScriptBlock scriptBlock, List<PSVariable> variables, object?[] args, Func<object, T> selectAs, T defaultIfNull = default!) where T : notnull
	{
		Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, args);
		return results.GetFirstValue(selectAs, defaultIfNull);
	}

	/// <summary>
	/// Attempts to invoke a script block with the specified variables and arguments, and converts its first output object.
	/// </summary>
	/// <remarks>
	/// The method catches every exception that the invocation throws and returns it through <paramref name="caughtError"/>
	/// instead of throwing. Exceptions that <paramref name="selectAs"/> throws are caught separately: <paramref name="result"/>
	/// receives <paramref name="defaultIfNull"/>, and <paramref name="caughtError"/> is <see langword="null"/>.
	/// </remarks>
	/// <typeparam name="T">The type to convert the first output object to.</typeparam>
	/// <param name="scriptBlock">The script block to invoke.</param>
	/// <param name="variables">The variables to define in the script block's scope, such as <c>$_</c> or <c>$left</c> and <c>$right</c>.</param>
	/// <param name="args">
	/// The arguments to pass to the script block. PowerShell does not copy this array, so pass a new array on every call.
	/// </param>
	/// <param name="selectAs">The function that converts the unwrapped first output object to <typeparamref name="T"/>.</param>
	/// <param name="result">
	/// When this method returns, contains the converted first output object, or <paramref name="defaultIfNull"/> when the script
	/// block outputs nothing, the conversion fails, or the invocation throws.
	/// </param>
	/// <param name="caughtError">
	/// When this method returns, contains the exception that the invocation threw, or <see langword="null"/> when it succeeded.
	/// </param>
	/// <param name="defaultIfNull">The value to return through <paramref name="result"/> when there is no converted value to return.</param>
	/// <returns>
	/// <see langword="true"/> when the invocation succeeds and <paramref name="result"/> is not <see langword="null"/>; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	internal static bool TryInvokeWithContext<T>(this ScriptBlock scriptBlock, List<PSVariable> variables, object?[] args, Func<object, T> selectAs, [NotNullIfNotNull(nameof(defaultIfNull))] out T result, out Exception? caughtError, T defaultIfNull = default!)
	{
		try
		{
			Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, args);
			result = results.GetFirstValue(selectAs, defaultIfNull);

			caughtError = null;
			return result is not null;
		}
		catch (Exception e)
		{
			caughtError = e;
			result = defaultIfNull;
			return false;
		}
	}
}
