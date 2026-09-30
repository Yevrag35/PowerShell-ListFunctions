using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;

namespace ListFunctions.Engine.Tests.Modern.Constructors;

public sealed class HashSetCtorTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public HashSetCtorTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Fact]
	public void Construct_CreatesASetOfTheElementType()
	{
		var ctor = new HashSetCtor(typeof(int), equalityComparer: null);

		Assert.IsType<HashSet<int>>(ctor.Construct());
	}

	[Theory]
	[InlineData(false, 1)]
	[InlineData(true, 2)]
	public void Construct_ComparesStringsWithoutCaseUnlessCaseSensitive(bool isCaseSensitive, int expectedCount)
	{
		var ctor = new HashSetCtor(typeof(string), equalityComparer: null)
		{
			IsCaseSensitive = isCaseSensitive,
		};
		var set = Assert.IsType<HashSet<string>>(ctor.Construct());

		set.Add("abc");
		set.Add("ABC");

		Assert.Equal(expectedCount, set.Count);
	}

	[Fact]
	public void Construct_CreatesAnObjectSetThatComparesLikeTheEqOperator()
	{
		var ctor = new HashSetCtor(genericType: null, equalityComparer: null);
		var set = Assert.IsType<HashSet<object>>(ctor.Construct());

		Assert.True(set.Add(1));
		Assert.False(set.Add("1"));
		Assert.True(set.Add("abc"));
		Assert.False(set.Add("ABC"));
	}

	[Fact]
	public void Construct_UsesTheEqualityBlock()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x.Length -eq $y.Length"), new HashBlock(ScriptBlock.Create("$_.Length")));
		var set = Assert.IsType<HashSet<object>>(new HashSetCtor(typeof(object), block).Construct());

		Assert.Same(block, set.Comparer);
		Assert.True(set.Add("abc"));
		Assert.False(set.Add("xyz"));
	}
}
