using ListFunctions.Internal;
using ListFunctions.Modern.Variables;

#nullable enable

namespace ListFunctions.Modern;

public sealed class ScriptBlockFilter
{
	private readonly PSThisVariable _constants;
	private readonly List<PSVariable> _extraVariables;
	private readonly ScriptBlock _scriptBlock;
	private readonly List<PSVariable> _variables;

	public ScriptBlockFilter(ScriptBlock scriptBlock, params
#if NET9_0_OR_GREATER
				ReadOnlySpan<PSVariable>
#else
			PSVariable[]
#endif
			additionalVariables)
	{
		Guard.NotNull(scriptBlock);
		_scriptBlock = scriptBlock;
		_extraVariables = new();
		_extraVariables.AddRange(additionalVariables
#if !NET9_0_OR_GREATER
			?? Array.Empty<PSVariable>()
#endif
		);
		_variables = new();
		_constants = new();
	}

	private List<PSVariable> InitializeContext(object? value)
	{
		_variables.Clear();
		_constants.SetValue(value);
		_constants.InsertIntoList(_variables);
		_variables.AddRange(_extraVariables);
		return _variables;
	}

	public bool All(ICollection? collection)
	{
		if (collection is null || collection.Count == 0)
			return false;

		foreach (object? item in collection)
		{
			if (!this.IsTrue(item))
				return false;
		}

		return true;
	}
	public bool Any(ICollection? collection)
	{
		if (collection is null || collection.Count == 0)
			return false;

		foreach (object? item in collection)
		{
			if (this.IsTrue(item))
				return true;
		}

		return false;
	}
	public bool IsTrue(object? value)
	{
		List<PSVariable> variables = this.InitializeContext(value);

		return _scriptBlock.InvokeWithContext(
			variables: variables,
			selectAs: LanguagePrimitives.IsTrue);
	}
}