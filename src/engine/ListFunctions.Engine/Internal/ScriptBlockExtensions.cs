using ListFunctions.Extensions;

namespace ListFunctions.Internal;

internal static class ScriptBlockExtensions
{
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
	[return: NotNullIfNotNull(nameof(defaultIfNull))]
	internal static T InvokeWithContext<T>(this ScriptBlock scriptBlock, List<PSVariable> variables, object?[] args, Func<object, T> selectAs, T defaultIfNull = default!) where T : notnull
	{
		Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, args);
		return results.GetFirstValue(selectAs, defaultIfNull);
	}

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
