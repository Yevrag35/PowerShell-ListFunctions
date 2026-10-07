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

	[Fact]
	[Trait("Category", "Bug13")]
	public void All_ReturnsTrueForAnEmptyCollectionWithoutRunningTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("throw 'The filter ran without an element.'"));

		Assert.True(filter.All(Array.Empty<object>()));
	}

	[Fact]
	[Trait("Category", "Bug13")]
	public void All_ReturnsTrueForNullWithoutRunningTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		var filter = new ScriptBlockFilter(ScriptBlock.Create("throw 'The filter ran without an element.'"));

		Assert.True(filter.All(collection: null));
	}

	// The condition cmdlets write these errors as warnings. Under Stop, an error that the script writes reaches the filter
	// as an ActionPreferenceStopException, whose own message starts with "The running command stopped because", so the
	// handler has to get the record of the error that the script wrote.
	[Theory]
	[InlineData("throw 'boom'; $true", "boom")]
	[InlineData("Write-Error 'oops'; $true", "oops")]
	public void IsTrue_ReturnsFalseAndPassesTheErrorToTheHandler(string condition, string expected)
	{
		using RunspaceScope scope = _runspace.Enter();
		List<ErrorRecord> errors = [];
		var filter = new ScriptBlockFilter(ScriptBlock.Create(condition), errors.Add, new PSVariable("ErrorActionPreference", ActionPreference.Stop));

		Assert.False(filter.IsTrue(1));
		ErrorRecord error = Assert.Single(errors);
		Assert.Equal(expected, error.ToString());
	}

	[Fact]
	public void IsTrue_LetsBreakPastTheHandler()
	{
		using RunspaceScope scope = _runspace.Enter();
		List<ErrorRecord> errors = [];
		var filter = new ScriptBlockFilter(ScriptBlock.Create("break"), errors.Add);

		Assert.Throws<BreakException>(() => filter.IsTrue(1));
		Assert.Empty(errors);
	}

	[Fact]
	public void Any_GoesOnAfterAnErrorThatTheHandlerReceives()
	{
		using RunspaceScope scope = _runspace.Enter();
		List<ErrorRecord> errors = [];
		var filter = new ScriptBlockFilter(ScriptBlock.Create("if ($_ -eq 1) { throw 'first' }; $true"), errors.Add);

		Assert.True(filter.Any(new[] { 1, 2 }));
		Assert.Single(errors);
	}

	[Fact]
	public void All_StopsAtAnErrorThatTheHandlerReceives()
	{
		using RunspaceScope scope = _runspace.Enter();
		List<ErrorRecord> errors = [];
		var filter = new ScriptBlockFilter(ScriptBlock.Create("if ($_ -ge 2) { throw \"failed $_\" }; $true"), errors.Add);

		Assert.False(filter.All(new[] { 1, 2, 3 }));
		ErrorRecord error = Assert.Single(errors);
		Assert.Equal("failed 2", error.ToString());
	}
}
