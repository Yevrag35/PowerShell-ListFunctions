using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;

namespace ListFunctions.Engine.Tests.Modern.Constructors;

public sealed class DictionaryCtorTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public DictionaryCtorTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Fact]
	public void Construct_UsesTheEqualityBlockForObjectKeys()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x.Length -eq $y.Length"), new HashBlock(ScriptBlock.Create("$_.Length")));
		var dict = Assert.IsType<Dictionary<object, object>>(new DictionaryCtor(block, keyType: null, valueType: null).Construct());

		Assert.Same(block, dict.Comparer);
		dict.Add("abc", 1);
		Assert.True(dict.ContainsKey("xyz"));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void Construct_UsesAnEqualityBlockWithValueTypeKeys()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x % 10 -eq $y % 10"), new HashBlock(ScriptBlock.Create("$_ % 10")));
		var dict = Assert.IsType<Dictionary<int, object>>(new DictionaryCtor(block, typeof(int), valueType: null).Construct());

		dict.Add(1, "a");

		Assert.True(dict.ContainsKey(11));
		Assert.False(dict.ContainsKey(2));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void Construct_UsesAnEqualityBlockWithNullableKeys()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x % 10 -eq $y % 10"), new HashBlock(ScriptBlock.Create("$_ % 10")));
#pragma warning disable CS8714 // Dictionary<int?, TValue> is valid at run time. Its notnull constraint applies only to nullable analysis.
		var dict = Assert.IsType<Dictionary<int?, object>>(new DictionaryCtor(block, typeof(int?), valueType: null).Construct());
#pragma warning restore CS8714

		dict.Add(1, "a");

		Assert.True(dict.ContainsKey(11));
		Assert.False(dict.ContainsKey(2));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void Construct_WrapsAnEqualityBlockForValueTypeKeys()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_")));
		var dict = Assert.IsType<Dictionary<int, object>>(new DictionaryCtor(block, typeof(int), valueType: null).Construct());

		var adapter = Assert.IsType<EqualityComparerAdapter<int>>(dict.Comparer);
		Assert.Same(block, adapter.InnerComparer);
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void Construct_PassesAnEqualityBlockForStringKeysAsItIs()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_.Length")));
		var dict = Assert.IsType<Dictionary<string, object>>(new DictionaryCtor(block, typeof(string), valueType: null).Construct());

		Assert.Same(block, dict.Comparer);
	}

	[Theory]
	[Trait("Category", "Bug02")]
	[InlineData(typeof(object), typeof(object), typeof(Hashtable))]
	[InlineData(typeof(string), typeof(int), typeof(Dictionary<string, int>))]
	public void Construct_PassesTheCapacityToTheDictionary(Type keyType, Type valueType, Type expectedType)
	{
		var ctor = new DictionaryCtor(comparer: null, keyType, valueType)
		{
			Capacity = 1000,
		};
		object dict = ctor.Construct();

		Assert.IsType(expectedType, dict);
		Assert.InRange(BucketCount.Of(dict), 1000, int.MaxValue);
	}

	[Fact]
	[Trait("Category", "Bug02")]
	public void Construct_PassesTheCapacityToADictionaryWithAnEqualityBlock()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_")));
		var ctor = new DictionaryCtor(block, keyType: null, valueType: null)
		{
			Capacity = 1000,
		};
		var dict = Assert.IsType<Dictionary<object, object>>(ctor.Construct());

		Assert.InRange(BucketCount.Of(dict), 1000, int.MaxValue);
	}
}
