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
	public void Construct_CreatesAnObjectSetThatComparesOnlyStringsAsStrings()
	{
		var ctor = new HashSetCtor(genericType: null, equalityComparer: null);
		var set = Assert.IsType<HashSet<object>>(ctor.Construct());

		// Elements of different types aren't converted, so none of these equals another.
		Assert.True(set.Add(1));
		Assert.True(set.Add("1"));
		Assert.True(set.Add(1L));
		Assert.True(set.Add(1.0));
		Assert.True(set.Add("abc"));
		Assert.False(set.Add("ABC"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Construct_ComparesStringsInAnObjectSetOrdinally(bool isCaseSensitive)
	{
		var ctor = new HashSetCtor(genericType: null, equalityComparer: null)
		{
			IsCaseSensitive = isCaseSensitive,
		};
		var set = Assert.IsType<HashSet<object>>(ctor.Construct());

		// A culture-sensitive comparison ignores the soft hyphen, and treats the decomposed and the precomposed accented e
		// as equal.
		Assert.True(set.Add("ab"));
		Assert.True(set.Add("a\u00ADb"));
		Assert.True(set.Add("e\u0301"));
		Assert.True(set.Add("\u00E9"));
	}

	[Fact]
	public void Construct_UsesTheEqualityBlock()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x.Length -eq $y.Length"), new HashBlock(ScriptBlock.Create("$_.Length")), additionalVariables: null);
		var set = Assert.IsType<HashSet<object>>(new HashSetCtor(typeof(object), block).Construct());

		Assert.Same(block, set.Comparer);
		Assert.True(set.Add("abc"));
		Assert.False(set.Add("xyz"));
	}

	[Theory]
	[Trait("Category", "Bug02")]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	[InlineData(typeof(string))]
	public void Construct_PassesTheCapacityToTheSet(Type elementType)
	{
		var ctor = new HashSetCtor(elementType, equalityComparer: null)
		{
			Capacity = 1000,
		};

		Assert.InRange(BucketCount.Of(ctor.Construct()), 1000, int.MaxValue);
	}

	[Fact]
	[Trait("Category", "Bug02")]
	public void Construct_PassesTheCapacityToASetWithAnEqualityBlock()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_")), additionalVariables: null);
		var ctor = new HashSetCtor(typeof(object), block)
		{
			Capacity = 1000,
		};
		var set = Assert.IsType<HashSet<object>>(ctor.Construct());

		Assert.InRange(BucketCount.Of(set), 1000, int.MaxValue);
	}
}
