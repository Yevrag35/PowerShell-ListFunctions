namespace ListFunctions.Engine.Tests;

/// <summary>
/// Gets the script block of a function, the way <c>${function:Test-It}</c> returns it.
/// </summary>
/// <remarks>
/// A function's script block has a <see cref="FunctionDefinitionAst"/> as its syntax tree, and its body is a separate
/// <see cref="ScriptBlockAst"/>. A script block that <see cref="ScriptBlock.Create(string)"/> parses from the same text has
/// a <see cref="ScriptBlockAst"/> that holds the definition as a statement, so only a defined function gives the first
/// shape.
/// </remarks>
internal static class FunctionScriptBlock
{
	/// <summary>
	/// Defines a function or filter named <c>Test-It</c> from the specified definition and returns its script block.
	/// </summary>
	/// <remarks>
	/// The definition runs in a new scope of the current thread's default runspace, so call
	/// <see cref="RunspaceFixture.Enter"/> first. The function goes away with the scope, and its script block stays usable.
	/// </remarks>
	/// <param name="definition">The definition of a function or filter named <c>Test-It</c>, such as <c>function Test-It { $_ }</c>.</param>
	/// <returns>The function's script block.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="definition"/> doesn't define a function named Test-It, or outputs something.</exception>
	public static ScriptBlock Create(string definition)
	{
		Collection<PSObject> output = ScriptBlock.Create(definition + "; ${function:Test-It}").Invoke();

		return output.Count == 1 && output[0]?.BaseObject is ScriptBlock function
			? function
			: throw new ArgumentException("The definition doesn't define a function named Test-It, or it outputs something.", nameof(definition));
	}
}
