using ListFunctions.Modern;
using ListFunctions.Modern.Exceptions;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class HashBlockTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public HashBlockTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Theory]
	[InlineData("$_.Length")]
	[InlineData("$this.Length")]
	[InlineData("$PSItem.Length")]
	public void GetHashCode_ExposesTheObjectToTheScript(string hashCodeScript)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create(hashCodeScript));

		Assert.Equal(4, block.GetHashCode("abcd", additionalVariables: null));
	}

	[Fact]
	public void GetHashCode_ConvertsTheOutputToInt()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create("[string]$_.Length"));

		Assert.Equal(4, block.GetHashCode("abcd", additionalVariables: null));
	}

	[Fact]
	public void GetHashCode_PassesAdditionalVariablesToTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create("$_.Length + $offset"));
		IEnumerable<PSVariable> variables = [new PSVariable("offset", 10)];

		Assert.Equal(14, block.GetHashCode("abcd", variables));
	}

	[Theory]
	[InlineData("$null = $_")]
	[InlineData("$null")]
	[InlineData("$_.ToUpperInvariant()")]
	[InlineData("throw 'No hash code'")]
	public void GetHashCode_ThrowsWhenTheScriptDoesNotReturnAHashCode(string hashCodeScript)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create(hashCodeScript));

		Assert.Throws<HashCodeScriptException>(() => block.GetHashCode("abcd", additionalVariables: null));
	}
}
