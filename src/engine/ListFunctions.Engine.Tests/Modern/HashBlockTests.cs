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
	[Trait("Category", "Bug01")]
	[InlineData("$_.Length")]
	[InlineData("$this.Length")]
	[InlineData("$PSItem.Length")]
	public void GetHashCode_ExposesTheObjectToTheScript(string hashCodeScript)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create(hashCodeScript));
		// The cmdlets always pass $ErrorActionPreference, so the test also calls the method with a variable.
		IEnumerable<PSVariable> variables = [new PSVariable("ErrorActionPreference", ActionPreference.Stop)];

		Assert.Equal(4, block.GetHashCode("abcd", additionalVariables: null));
		Assert.Equal(4, block.GetHashCode("abcd", variables));
	}

	[Fact]
	[Trait("Category", "Bug05")]
	public void GetHashCode_PassesTheObjectAsTheFirstArgument()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create("$args[0].Length"));
		// The cmdlets always pass $ErrorActionPreference, so the test also calls the method with a variable.
		IEnumerable<PSVariable> variables = [new PSVariable("ErrorActionPreference", ActionPreference.Stop)];

		Assert.Equal(4, block.GetHashCode("abcd", additionalVariables: null));
		Assert.Equal(4, block.GetHashCode("abcd", variables));
	}

	[Fact]
	[Trait("Category", "Bug01")]
	public void GetHashCode_ConvertsTheOutputToInt()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create("[string]$_.Length"));

		Assert.Equal(4, block.GetHashCode("abcd", additionalVariables: null));
	}

	[Fact]
	public void GetHashCode_RunsAScriptWithOnlyAProcessBlock()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create("process { $_.Length }"));

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
	[Trait("Category", "Bug01")]
	[InlineData("$null = $_")]
	[InlineData("$null")]
	[InlineData("$_.ToUpperInvariant()")]
	public void GetHashCode_ThrowsWhenTheScriptDoesNotReturnAHashCode(string hashCodeScript)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create(hashCodeScript));

		Assert.Throws<HashCodeScriptException>(() => block.GetHashCode("abcd", additionalVariables: null));
	}

	// The cmdlets pass these exceptions on to PowerShell, which decides by their type what each one ends. Wrapped in a
	// HashCodeScriptException, an error that the script writes under Stop would end only the statement instead of the
	// whole script, and break couldn't leave the loop around the command.
	[Theory]
	[InlineData("throw 'No hash code'", typeof(RuntimeException))]
	[InlineData("Write-Error 'No hash code'; $_.Length", typeof(ActionPreferenceStopException))]
	[InlineData("break", typeof(BreakException))]
	public void GetHashCode_LetsTheExceptionsOfTheScriptThrough(string hashCodeScript, Type expected)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new HashBlock(ScriptBlock.Create(hashCodeScript));
		IEnumerable<PSVariable> variables = [new PSVariable("ErrorActionPreference", ActionPreference.Stop)];

		Exception exception = Assert.ThrowsAny<Exception>(() => block.GetHashCode("abcd", variables));
		Assert.Equal(expected, exception.GetType());
	}

	// ScriptBlock.InvokeWithContext, which runs the script, refuses a script block that has a begin block, or both a
	// process block and an end block.
	[Theory]
	[InlineData("begin { } process { $_.Length }")]
	[InlineData("process { $_.Length } end { $_.Length }")]
	public void Constructor_ThrowsWhenInvokeWithContextCannotRunTheScript(string hashCodeScript)
	{
		Assert.Throws<ArgumentException>(() => new HashBlock(ScriptBlock.Create(hashCodeScript)));
	}
}
