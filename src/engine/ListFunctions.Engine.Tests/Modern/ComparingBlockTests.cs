using ListFunctions.Modern;
using ListFunctions.Modern.Exceptions;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class ComparingBlockTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public ComparingBlockTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Theory]
	[InlineData("$x.CompareTo($y)")]
	[InlineData("$left.CompareTo($right)")]
	public void Compare_ExposesTheOperandsToTheScript(string comparingScript)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new ComparingBlock<int>(ScriptBlock.Create(comparingScript), additionalVariables: null);

		Assert.Equal(-1, Math.Sign(block.Compare(1, 2)));
		Assert.Equal(0, block.Compare(2, 2));
		Assert.Equal(1, Math.Sign(block.Compare(3, 2)));
	}

	[Fact]
	[Trait("Category", "Bug05")]
	public void Compare_PassesTheOperandsAsArguments()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new ComparingBlock<int>(ScriptBlock.Create("$args[0].CompareTo($args[1])"), additionalVariables: null);

		Assert.Equal(-1, Math.Sign(block.Compare(1, 2)));
		Assert.Equal(0, block.Compare(2, 2));
		Assert.Equal(1, Math.Sign(block.Compare(3, 2)));
	}

	[Fact]
	public void Compare_PassesAdditionalVariablesToTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		IEnumerable<PSVariable> variables = [new PSVariable("direction", -1)];
		var block = new ComparingBlock<int>(ScriptBlock.Create("$x.CompareTo($y) * $direction"), variables);

		Assert.Equal(1, Math.Sign(block.Compare(1, 2)));
	}

	[Fact]
	public void Compare_SortsNullFirstWithoutRunningTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new ComparingBlock<string>(ScriptBlock.Create("throw 'The script ran.'"), additionalVariables: null);

		Assert.Equal(0, block.Compare(null, null));
		Assert.Equal(-1, block.Compare(null, "a"));
		Assert.Equal(1, block.Compare("a", null));
	}

	[Fact]
	[Trait("Category", "Bug10")]
	public void Compare_ConvertsTheOutputToInt()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new ComparingBlock<int>(ScriptBlock.Create("[string]$x.CompareTo($y)"), additionalVariables: null);

		Assert.Equal(-1, block.Compare(1, 2));
		Assert.Equal(1, block.Compare(2, 1));
	}

	[Theory]
	[Trait("Category", "Bug10")]
	[InlineData("'x' + $x + $y")]
	[InlineData("$null = $x, $y")]
	[InlineData("$null = $x, $y; $null")]
	public void Compare_ThrowsWhenTheScriptDoesNotReturnAnInt(string comparingScript)
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new ComparingBlock<int>(ScriptBlock.Create(comparingScript), additionalVariables: null);

		ComparingScriptException exception = Assert.Throws<ComparingScriptException>(() => block.Compare(1, 2));
		Assert.Equal(1, exception.Offender);
		Assert.Equal(2, exception.Variables["y"]);
	}

	[Fact]
	public void Compare_ConvertsObjectsToTheElementType()
	{
		using RunspaceScope scope = _runspace.Enter();
		IComparer block = new ComparingBlock<int>(ScriptBlock.Create("$x.CompareTo($y)"), additionalVariables: null);

		Assert.Equal(-1, Math.Sign(block.Compare("1", 2)));
	}

	[Fact]
	public void SortedSet_OrdersItsElementsWithTheScript()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new ComparingBlock<int>(ScriptBlock.Create("$y.CompareTo($x)"), additionalVariables: null);
		var set = new SortedSet<int>([3, 1, 4, 5], block);

		Assert.Equal(new[] { 5, 4, 3, 1 }, set);
	}

	[Fact]
	public void Create_ReturnsAComparingBlockOfTheSpecifiedType()
	{
		IComparer comparer = ComparingBlock.Create(ScriptBlock.Create("$x.CompareTo($y)"), typeof(string), additionalVariables: null);

		var block = Assert.IsType<ComparingBlock<string>>(comparer);
		Assert.Equal(typeof(string), ((IComparingBlock)block).ChecksType);
	}
}
