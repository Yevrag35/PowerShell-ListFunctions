namespace ListFunctions.Extensions;

/// <summary>
/// Provides helpers that inspect and validate script blocks, and invoke them with injected variables.
/// </summary>
/// <remarks>
/// A script block runs only on a thread whose <see cref="System.Management.Automation.Runspaces.Runspace.DefaultRunspace"/> is set. Callers of the invoke method must
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
	/// it runs contains at least one statement. It checks a function's script block, such as <c>${function:Test-It}</c>, by
	/// the function's body (see <see cref="TryGetBody(ScriptBlock, out ScriptBlockAst, out ReadOnlyCollection{ParameterAst})"/>).
	/// Windows PowerShell 5.1 has no <c>clean</c> blocks, so only the .NET 10 build looks for one.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block to check. This value must not be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> if <paramref name="scriptBlock"/> has a body that can be invoked; otherwise, <see langword="false"/>,
	/// including when its syntax tree is neither a <see cref="ScriptBlockAst"/> nor a <see cref="FunctionDefinitionAst"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="scriptBlock"/> is null.</exception>
	internal static bool IsProperScriptBlock(this ScriptBlock scriptBlock)
	{
		ArgumentNullException.ThrowIfNull(scriptBlock);

		if (!scriptBlock.TryGetBody(out ScriptBlockAst? scriptAst, out _) || scriptAst.BeginBlock is not null)
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

	/// <summary>
	/// Gets the body of a script block and the parameters that it declares.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Most script blocks have a <see cref="ScriptBlockAst"/> as their syntax tree, which is also their body. A function's
	/// script block, such as <c>${function:Test-It}</c> or the <see cref="FunctionInfo.ScriptBlock"/> of the function, has
	/// a <see cref="FunctionDefinitionAst"/> instead, and its body is a child node. A search of the function's tree that
	/// doesn't search nested script blocks skips that body, so search <paramref name="body"/> instead.
	/// </para>
	/// <para>
	/// A function declares its parameters either in parentheses after its name, as in <c>function Test-It($a)</c>, or in a
	/// <c>param()</c> block in its body, and the method returns them from either place. PowerShell binds the arguments that
	/// <see cref="ScriptBlock.InvokeWithContext(Dictionary{string, ScriptBlock}, List{PSVariable}, object[])"/> passes to
	/// these parameters in the order they're declared, whatever their attributes, and puts the arguments left over in
	/// <c>$args</c>.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block whose body to get. This value must not be <see langword="null"/>.</param>
	/// <param name="body">
	/// When this method returns <see langword="true"/>, contains the body of <paramref name="scriptBlock"/>; otherwise,
	/// <see langword="null"/>.
	/// </param>
	/// <param name="parameters">
	/// When this method returns <see langword="true"/>, contains the parameters that <paramref name="scriptBlock"/> declares, in
	/// the order it declares them, or <see langword="null"/> or an empty collection when it declares none; otherwise,
	/// <see langword="null"/>.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if the syntax tree of <paramref name="scriptBlock"/> is a <see cref="ScriptBlockAst"/> or a
	/// <see cref="FunctionDefinitionAst"/>; otherwise, <see langword="false"/>.
	/// </returns>
	internal static bool TryGetBody(this ScriptBlock scriptBlock, [NotNullWhen(true)] out ScriptBlockAst? body, out ReadOnlyCollection<ParameterAst>? parameters)
	{
		switch (scriptBlock.Ast)
		{
			case ScriptBlockAst scriptAst:
				body = scriptAst;
				parameters = scriptAst.ParamBlock?.Parameters;
				return true;

			case FunctionDefinitionAst functionAst:
				body = functionAst.Body;
				parameters = functionAst.Parameters ?? functionAst.Body.ParamBlock?.Parameters;
				return true;

			default:
				body = null;
				parameters = null;
				return false;
		}
	}

	// PowerShell doesn't copy the args array: a script block without a param block gets that array as $args. Pass a
	// new array on every call to the method below, because a reused one would change under a script block that keeps
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
}
