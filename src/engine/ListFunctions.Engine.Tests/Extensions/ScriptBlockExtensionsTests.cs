using ListFunctions.Extensions;

namespace ListFunctions.Engine.Tests.Extensions;

public sealed class ScriptBlockExtensionsTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public ScriptBlockExtensionsTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	// ScriptBlock.InvokeWithContext, which runs the module's script blocks, runs a single block: the process block when
	// there is one, and otherwise the end block, which holds the statements of a script block without named blocks.
	[Theory]
	[InlineData("$_")]
	[InlineData("end { $_ }")]
	[InlineData("process { $_ }")]
	public void IsProperScriptBlock_ReturnsTrueWhenTheBlockThatRunsHasAStatement(string script)
	{
		Assert.True(ScriptBlock.Create(script).IsProperScriptBlock());
	}

	// InvokeWithContext refuses a script block that has a begin block, or both a process block and an end block.
	[Theory]
	[InlineData("begin { } process { $_ }")]
	[InlineData("begin { } end { $_ }")]
	[InlineData("begin { $_ }")]
	[InlineData("process { $_ } end { $_ }")]
	public void IsProperScriptBlock_ReturnsFalseWhenInvokeWithContextCannotRunTheScriptBlock(string script)
	{
		Assert.False(ScriptBlock.Create(script).IsProperScriptBlock());
	}

	[Theory]
	[InlineData("")]
	[InlineData("# A comment")]
	[InlineData("param($x)")]
	[InlineData("end { }")]
	[InlineData("process { }")]
	public void IsProperScriptBlock_ReturnsFalseWhenTheBlockThatRunsHasNoStatements(string script)
	{
		Assert.False(ScriptBlock.Create(script).IsProperScriptBlock());
	}

	// A function's syntax tree is a FunctionDefinitionAst. InvokeWithContext runs the function's body the way it runs any
	// other script block.
	[Theory]
	[InlineData("function Test-It { $_ }", true)]
	[InlineData("filter Test-It { $_ }", true)]
	[InlineData("function Test-It($x) { $x }", true)]
	[InlineData("function Test-It { begin { } process { $_ } }", false)]
	[InlineData("function Test-It { }", false)]
	public void IsProperScriptBlock_ChecksTheBodyOfAFunction(string definition, bool expected)
	{
		using RunspaceScope scope = _runspace.Enter();
		ScriptBlock function = FunctionScriptBlock.Create(definition);

		Assert.IsType<FunctionDefinitionAst>(function.Ast);
		Assert.Equal(expected, function.IsProperScriptBlock());
	}
}
