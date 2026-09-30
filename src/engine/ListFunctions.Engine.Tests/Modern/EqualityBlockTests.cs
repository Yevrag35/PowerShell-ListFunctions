using ListFunctions.Modern;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class EqualityBlockTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public EqualityBlockTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Theory]
	[InlineData("$x -eq $y")]
	[InlineData("$left -eq $right")]
	public void Equals_ExposesTheOperandsToTheScript(string equalityScript)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create(equalityScript), new HashBlock(ScriptBlock.Create("0")));

		Assert.True(block.Equals(1, 1));
		Assert.False(block.Equals(1, 2));
	}

	[Fact]
	public void Equals_ReturnsTrueForTheSameReferenceWithoutRunningTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("throw 'The script ran.'"), new HashBlock(ScriptBlock.Create("0")));
		object item = new();

		Assert.True(block.Equals(item, item));
	}

	[Fact]
	public void Equals_ReturnsFalseWhenTheScriptHasNoOutput()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$null = $x, $y"), new HashBlock(ScriptBlock.Create("0")));

		Assert.False(block.Equals(1, 1));
	}

	[Fact]
	public void Equals_PassesAdditionalVariablesToTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		IEnumerable<PSVariable> variables = [new PSVariable("tolerance", 1)];
		var block = new EqualityBlock(ScriptBlock.Create("[Math]::Abs($x - $y) -le $tolerance"), new HashBlock(ScriptBlock.Create("0")), variables);

		Assert.True(block.Equals(1, 2));
		Assert.False(block.Equals(1, 3));
	}

	[Fact]
	public void GetHashCode_PassesAdditionalVariablesToTheHashBlock()
	{
		using RunspaceScope scope = _runspace.Enter();
		IEnumerable<PSVariable> variables = [new PSVariable("offset", 10)];
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_.Length + $offset")), variables);

		Assert.Equal(14, block.GetHashCode("abcd"));
	}

	[Fact]
	public void HashSet_UsesTheScriptsToFindDuplicates()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(
			ScriptBlock.Create("[string]::Equals($x, $y, 'OrdinalIgnoreCase')"),
			new HashBlock(ScriptBlock.Create("$_.ToUpperInvariant().GetHashCode()")));
		var set = new HashSet<object>(block);

		Assert.True(set.Add("abc"));
		Assert.False(set.Add("ABC"));
		Assert.True(set.Add("def"));
	}

	[Fact]
	public void Constructor_ThrowsWhenTheScriptHasNoStatements()
	{
		Assert.Throws<ArgumentException>(() => new EqualityBlock(ScriptBlock.Create("# No statements"), new HashBlock(ScriptBlock.Create("0"))));
	}
}
