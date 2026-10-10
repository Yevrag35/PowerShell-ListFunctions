using ListFunctions.Internal;
using ListFunctions.Modern;

namespace ListFunctions.Engine.Tests.Internal;

public sealed class DictionaryWrapperTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public DictionaryWrapperTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Fact]
	public void CreateTyped_UsesTheEqualityBlockForObjectKeys()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x.Length -eq $y.Length"), new HashBlock(ScriptBlock.Create("$_.Length")), additionalVariables: null);
		var dict = Assert.IsType<Dictionary<object, object>>(DictionaryWrapper.CreateTyped(typeof(object), typeof(object), 0, block).AsDictionary());

		Assert.Same(block, dict.Comparer);
		dict.Add("abc", 1);
		Assert.True(dict.ContainsKey("xyz"));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void CreateTyped_UsesAnEqualityBlockWithValueTypeKeys()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x % 10 -eq $y % 10"), new HashBlock(ScriptBlock.Create("$_ % 10")), additionalVariables: null);
		var dict = Assert.IsType<Dictionary<int, object>>(DictionaryWrapper.CreateTyped(typeof(int), typeof(object), 0, block).AsDictionary());

		dict.Add(1, "a");

		Assert.True(dict.ContainsKey(11));
		Assert.False(dict.ContainsKey(2));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void CreateTyped_UsesAnEqualityBlockWithNullableKeys()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x % 10 -eq $y % 10"), new HashBlock(ScriptBlock.Create("$_ % 10")), additionalVariables: null);
#pragma warning disable CS8714 // Dictionary<int?, TValue> is valid at run time. Its notnull constraint applies only to nullable analysis.
		var dict = Assert.IsType<Dictionary<int?, object>>(DictionaryWrapper.CreateTyped(typeof(int?), typeof(object), 0, block).AsDictionary());
#pragma warning restore CS8714

		dict.Add(1, "a");

		Assert.True(dict.ContainsKey(11));
		Assert.False(dict.ContainsKey(2));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void CreateTyped_WrapsAnEqualityBlockForValueTypeKeys()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_")), additionalVariables: null);
		var dict = Assert.IsType<Dictionary<int, object>>(DictionaryWrapper.CreateTyped(typeof(int), typeof(object), 0, block).AsDictionary());

		Assert.IsType<EqualityComparerAdapter<int>>(dict.Comparer);
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void CreateTyped_PassesAnEqualityBlockForStringKeysAsItIs()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_.Length")), additionalVariables: null);
		var dict = Assert.IsType<Dictionary<string, object>>(DictionaryWrapper.CreateTyped(typeof(string), typeof(object), 0, block).AsDictionary());

		Assert.Same(block, dict.Comparer);
	}

	[Theory]
	[Trait("Category", "Bug02")]
	[InlineData(typeof(object), typeof(object), typeof(Dictionary<object, object>))]
	[InlineData(typeof(string), typeof(int), typeof(Dictionary<string, int>))]
	public void CreateTyped_PassesTheCapacityToTheDictionary(Type keyType, Type valueType, Type expectedType)
	{
		IDictionary dict = DictionaryWrapper.CreateTyped(keyType, valueType, 1000, comparer: null).AsDictionary();

		Assert.IsType(expectedType, dict);
		Assert.InRange(BucketCount.Of(dict), 1000, int.MaxValue);
	}

	[Fact]
	[Trait("Category", "Bug02")]
	public void CreateTyped_PassesTheCapacityToADictionaryWithAnEqualityBlock()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_")), additionalVariables: null);
		var dict = Assert.IsType<Dictionary<object, object>>(DictionaryWrapper.CreateTyped(typeof(object), typeof(object), 1000, block).AsDictionary());

		Assert.InRange(BucketCount.Of(dict), 1000, int.MaxValue);
	}

	[Fact]
	public void CreateTyped_ThrowsTheExceptionOfTheDictionarysConstructor()
	{
		// No array can hold int.MaxValue buckets, so the dictionary's constructor fails before it allocates anything. On
		// .NET 10, Activator.CreateInstance would wrap the exception in a TargetInvocationException. .NET Framework's
		// doesn't wrap this one, so only the net10.0 run can catch a change back to Activator.
		Assert.Throws<OutOfMemoryException>(() => DictionaryWrapper.CreateTyped(typeof(string), typeof(int), int.MaxValue, comparer: null));
	}

	[Theory]
	[Trait("Category", "Bug20")]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void CreateTyped_ComparesObjectKeysWithoutRegardToCase(Type valueType)
	{
		IDictionary dict = DictionaryWrapper.CreateTyped(typeof(object), valueType, 0, comparer: null).AsDictionary();

		dict["a"] = 1;
		dict["A"] = 2;

		Assert.Single(dict);
	}

	[Theory]
	[Trait("Category", "Bug20")]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void CreateTyped_ComparesObjectKeysWithRegardToCaseWhenCaseSensitive(Type valueType)
	{
		IDictionary dict = DictionaryWrapper.CreateTyped(typeof(object), valueType, 0, comparer: null, caseSensitive: true).AsDictionary();

		dict["a"] = 1;
		dict["A"] = 2;

		Assert.Equal(2, dict.Count);
	}

	[Theory]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void CreateTyped_ComparesObjectKeysOrdinallyWhenCaseSensitive(Type valueType)
	{
		IDictionary dict = DictionaryWrapper.CreateTyped(typeof(object), valueType, 0, comparer: null, caseSensitive: true).AsDictionary();

		// A culture-sensitive comparison treats the decomposed and the precomposed accented e as the same key.
		dict["é"] = 1;
		dict["é"] = 2;

		Assert.Equal(2, dict.Count);
	}

	[Theory]
	[Trait("Category", "Bug20")]
	[InlineData(typeof(object))]
	[InlineData(typeof(int))]
	public void CreateTyped_KeepsObjectKeysOfDifferentTypesApart(Type valueType)
	{
		IDictionary dict = DictionaryWrapper.CreateTyped(typeof(object), valueType, 0, comparer: null).AsDictionary();

		dict[1] = 1;
		dict["1"] = 2;

		Assert.Equal(2, dict.Count);
	}

	[Fact]
	public void CreateTyped_UsesAStringComparerForObjectKeys()
	{
		// A StringComparer isn't an IEqualityComparer<object>, so the dictionary gets it wrapped. Ordinal compares two
		// strings with regard to case, and any other two keys with their own Equals method.
		var dict = Assert.IsType<Dictionary<object, object>>(DictionaryWrapper.CreateTyped(typeof(object), typeof(object), 0, StringComparer.Ordinal).AsDictionary());

		dict["a"] = 1;
		dict["A"] = 2;
		dict[1] = 3;
		dict["1"] = 4;

		Assert.Equal(4, dict.Count);
	}

	[Fact]
	public void Add_LetsAnErrorFromTheComparerReachTheCallerInsteadOfAddFailed()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("throw 'boom'")), additionalVariables: null);
		DictionaryWrapper wrapper = DictionaryWrapper.CreateTyped(typeof(string), typeof(object), 0, block);
		bool reported = false;
		wrapper.AddFailed = (key, exception) => reported = true;

		// The cmdlets rely on the error reaching PowerShell as it is, so it can end the script the way it does from
		// ForEach-Object. AddFailed would turn it into a non-terminating error.
		RuntimeException thrown = Assert.ThrowsAny<RuntimeException>(() => wrapper.Add("a", 1));

		Assert.Equal("boom", thrown.Message);
		Assert.False(reported);
		Assert.Empty(wrapper.AsDictionary());
	}
}
