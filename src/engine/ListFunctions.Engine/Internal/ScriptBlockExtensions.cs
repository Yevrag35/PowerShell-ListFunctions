using ListFunctions.Extensions;

namespace ListFunctions.Internal;

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
	/// Determines whether a script block has a body that can be invoked as a comparer or predicate.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method returns <see langword="true"/> when the script block has both a <c>begin</c> block and a <c>process</c> block.
	/// Otherwise, it returns <see langword="true"/> only when the <c>end</c> block, which holds the statements of a script
	/// block without named blocks, contains at least one statement.
	/// </para>
	/// <para>
	/// A script block that has only a <c>process</c> block, or only a <c>begin</c> block, has no <c>end</c> block, so the
	/// method returns <see langword="false"/> for it.
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
		Guard.NotNull(scriptBlock);

		if (scriptBlock.Ast is not ScriptBlockAst scriptAst)
		{
			return false;
		}

		if (!(scriptAst.BeginBlock is null || scriptAst.ProcessBlock is null))
		{
			return true;
		}
		else if (scriptAst.EndBlock is null)
		{
			return false;
		}

		ReadOnlyCollection<StatementAst> statements = scriptAst.EndBlock.Statements;
		return statements.Count != 0;
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
	/// Attempts to invoke a script block with the specified variables and arguments, and returns its first output object.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method catches every exception that the invocation throws and returns it through <paramref name="caughtError"/>
	/// instead of throwing.
	/// </para>
	/// <para>
	/// The first output object is unwrapped from its <see cref="PSObject"/> to the underlying base object.
	/// </para>
	/// </remarks>
	/// <param name="scriptBlock">The script block to invoke.</param>
	/// <param name="variables">The variables to define in the script block's scope, such as <c>$_</c> or <c>$left</c> and <c>$right</c>.</param>
	/// <param name="args">
	/// The arguments to pass to the script block. PowerShell does not copy this array, so pass a new array on every call.
	/// </param>
	/// <param name="result">
	/// When this method returns, contains the unwrapped first output object, or <paramref name="defaultIfNull"/> when the script
	/// block outputs nothing or throws.
	/// </param>
	/// <param name="caughtError">
	/// When this method returns, contains the exception that the invocation threw, or <see langword="null"/> when it succeeded.
	/// </param>
	/// <param name="defaultIfNull">The value to return through <paramref name="result"/> when there is no output object to return.</param>
	/// <returns>
	/// <see langword="true"/> when the invocation succeeds and <paramref name="result"/> is not <see langword="null"/>; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	internal static bool TryInvokeWithContext(this ScriptBlock scriptBlock, List<PSVariable> variables, object?[] args, [NotNullIfNotNull(nameof(defaultIfNull))] out object? result, out Exception? caughtError, object? defaultIfNull = null)
	{
		try
		{
			Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, args);
			caughtError = null;
			if (results.Count > 0 && results[0] is PSObject o)
			{
				result = PSObject.AsPSObject(o.BaseObject).ImmediateBaseObject;
				return result is not null;
			}

			result = defaultIfNull;
			return result is not null;

		}
		catch (Exception e)
		{
			caughtError = e;
			result = defaultIfNull;
			return false;
		}
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
