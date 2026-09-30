using ListFunctions.Modern;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class ScriptBlockFilterTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public ScriptBlockFilterTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Theory]
	[InlineData("$_ -gt 2")]
	[InlineData("$this -gt 2")]
	[InlineData("$PSItem -gt 2")]
	public void IsTrue_ExposesTheObjectToTheScript(string condition)
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create(condition));

		Assert.True(filter.IsTrue(3));
		Assert.False(filter.IsTrue(2));
	}

	[Fact]
	[Trait("Category", "Bug05")]
	public void IsTrue_PassesTheObjectAsTheFirstArgument()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("$args[0] -gt 2"));

		Assert.True(filter.IsTrue(3));
		Assert.False(filter.IsTrue(2));
	}

	[Fact]
	[Trait("Category", "Bug05")]
	public void IsTrue_PassesNullAndArraysAsASingleArgument()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("$args.Count -eq 1"));

		Assert.True(filter.IsTrue(null));
		Assert.True(filter.IsTrue(new object[] { 1, 2 }));
	}

	[Theory]
	[InlineData(1, true)]
	[InlineData(0, false)]
	[InlineData("text", true)]
	[InlineData("", false)]
	[InlineData(null, false)]
	public void IsTrue_ConvertsTheOutputByPowerShellRules(object? value, bool expected)
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("$_"));

		Assert.Equal(expected, filter.IsTrue(value));
	}

	[Fact]
	public void IsTrue_ReturnsFalseWhenTheScriptHasNoOutput()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("$null = $_"));

		Assert.False(filter.IsTrue(1));
	}

	[Fact]
	public void IsTrue_PassesAdditionalVariablesToTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("$_ -gt $limit"), new PSVariable("limit", 5));

		Assert.True(filter.IsTrue(6));
		Assert.False(filter.IsTrue(5));
	}

	[Fact]
	public void Any_StopsAtTheFirstElementThatPasses()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("if ($_ -eq 99) { throw 'The filter ran past the match.' }; $_ -eq 2"));

		Assert.True(filter.Any(new[] { 1, 2, 99 }));
	}

	[Fact]
	public void Any_ReturnsFalseWhenNoElementPasses()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("$_ -eq 2"));

		Assert.False(filter.Any(new[] { 1, 3 }));
	}

	[Fact]
	public void All_StopsAtTheFirstElementThatFails()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("if ($_ -eq 99) { throw 'The filter ran past the failure.' }; $_ -lt 2"));

		Assert.False(filter.All(new[] { 1, 2, 99 }));
	}

	[Fact]
	public void All_ReturnsTrueWhenEveryElementPasses()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("$_ -gt 0"));

		Assert.True(filter.All(new[] { 1, 2, 3 }));
	}
}
