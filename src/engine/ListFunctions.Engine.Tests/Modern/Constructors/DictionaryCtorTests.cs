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
	[InlineData(typeof(object), typeof(object), typeof(Dictionary<object, object>))]
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

	[Theory]
	[Trait("Category", "Bug20")]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void Construct_ComparesObjectKeysWithoutRegardToCase(Type valueType)
	{
		var dict = Assert.IsAssignableFrom<IDictionary>(new DictionaryCtor(comparer: null, keyType: null, valueType).Construct());

		dict["a"] = 1;
		dict["A"] = 2;

		Assert.Single(dict);
	}

	[Theory]
	[Trait("Category", "Bug20")]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void Construct_ComparesObjectKeysWithRegardToCaseWhenCaseSensitive(Type valueType)
	{
		var ctor = new DictionaryCtor(comparer: null, keyType: null, valueType)
		{
			IsCaseSensitive = true,
		};
		var dict = Assert.IsAssignableFrom<IDictionary>(ctor.Construct());

		dict["a"] = 1;
		dict["A"] = 2;

		Assert.Equal(2, dict.Count);
	}

	[Theory]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void Construct_ComparesObjectKeysOrdinallyWhenCaseSensitive(Type valueType)
	{
		var ctor = new DictionaryCtor(comparer: null, keyType: null, valueType)
		{
			IsCaseSensitive = true,
		};
		var dict = Assert.IsAssignableFrom<IDictionary>(ctor.Construct());

		// A culture-sensitive comparison treats the decomposed and the precomposed accented e as the same key.
		dict["é"] = 1;
		dict["é"] = 2;

		Assert.Equal(2, dict.Count);
	}

	[Theory]
	[Trait("Category", "Bug20")]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void Construct_KeepsObjectKeysOfDifferentTypesApart(Type valueType)
	{
		var dict = Assert.IsAssignableFrom<IDictionary>(new DictionaryCtor(comparer: null, keyType: null, valueType).Construct());

		dict[1] = 1;
		dict["1"] = 2;

		Assert.Equal(2, dict.Count);
	}

	[Fact]
	public void Construct_UsesAStringComparerForObjectKeys()
	{
		// A StringComparer isn't an IEqualityComparer<object>, so the dictionary gets it wrapped. Ordinal compares two
		// strings with regard to case, and any other two keys with their own Equals method.
		var dict = Assert.IsType<Dictionary<object, object>>(new DictionaryCtor(StringComparer.Ordinal, keyType: null, valueType: null).Construct());

		dict["a"] = 1;
		dict["A"] = 2;
		dict[1] = 3;
		dict["1"] = 4;

		Assert.Equal(4, dict.Count);
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
