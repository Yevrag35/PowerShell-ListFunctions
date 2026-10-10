using ListFunctions.Internal;
using ListFunctions.Modern;

namespace ListFunctions.Engine.Tests.Internal;

public sealed class SetWrapperTests : IClassFixture<RunspaceFixture>
{
	private readonly RunspaceFixture _runspace;

	public SetWrapperTests(RunspaceFixture runspace)
	{
		_runspace = runspace;
	}

	[Theory]
	[InlineData(false, 1)]
	[InlineData(true, 2)]
	public void CreateHashSet_ComparesStringsWithoutRegardToCaseUnlessCaseSensitive(bool caseSensitive, int expectedCount)
	{
		var set = Assert.IsType<HashSet<string>>(SetWrapper.CreateHashSet(typeof(string), 0, comparer: null, caseSensitive).AsSet());

		set.Add("abc");
		set.Add("ABC");

		Assert.Equal(expectedCount, set.Count);
	}

	[Fact]
	public void CreateHashSet_CreatesAnObjectSetThatComparesOnlyStringsAsStrings()
	{
		var set = Assert.IsType<HashSet<object>>(SetWrapper.CreateHashSet(typeof(object), 0, comparer: null).AsSet());

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
	public void CreateHashSet_ComparesStringsInAnObjectSetOrdinally(bool caseSensitive)
	{
		var set = Assert.IsType<HashSet<object>>(SetWrapper.CreateHashSet(typeof(object), 0, comparer: null, caseSensitive).AsSet());

		// A culture-sensitive comparison ignores the soft hyphen, and treats the decomposed and the precomposed accented e
		// as equal.
		Assert.True(set.Add("ab"));
		Assert.True(set.Add("a\u00ADb"));
		Assert.True(set.Add("e\u0301"));
		Assert.True(set.Add("\u00E9"));
	}

	[Fact]
	public void CreateHashSet_UsesTheEqualityBlockForObjectElements()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x.Length -eq $y.Length"), new HashBlock(ScriptBlock.Create("$_.Length")), additionalVariables: null);
		var set = Assert.IsType<HashSet<object>>(SetWrapper.CreateHashSet(typeof(object), 0, block).AsSet());

		Assert.Same(block, set.Comparer);
		Assert.True(set.Add("abc"));
		Assert.False(set.Add("xyz"));
	}

	[Fact]
	public void CreateHashSet_UsesAnEqualityBlockWithValueTypeElements()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x % 10 -eq $y % 10"), new HashBlock(ScriptBlock.Create("$_ % 10")), additionalVariables: null);
		var set = Assert.IsType<HashSet<int>>(SetWrapper.CreateHashSet(typeof(int), 0, block).AsSet());

		Assert.True(set.Add(1));
		Assert.False(set.Add(11));
		Assert.True(set.Add(2));
	}

	[Theory]
	[Trait("Category", "Bug02")]
	[InlineData(typeof(object), typeof(HashSet<object>))]
	[InlineData(typeof(int), typeof(HashSet<int>))]
	[InlineData(typeof(string), typeof(HashSet<string>))]
	public void CreateHashSet_PassesTheCapacityToTheSet(Type elementType, Type expectedType)
	{
		IEnumerable set = SetWrapper.CreateHashSet(elementType, 1000, comparer: null).AsSet();

		Assert.IsType(expectedType, set);
		Assert.InRange(BucketCount.Of(set), 1000, int.MaxValue);
	}

	[Fact]
	[Trait("Category", "Bug02")]
	public void CreateHashSet_PassesTheCapacityToASetWithAnEqualityBlock()
	{
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("$_")), additionalVariables: null);
		var set = Assert.IsType<HashSet<object>>(SetWrapper.CreateHashSet(typeof(object), 1000, block).AsSet());

		Assert.InRange(BucketCount.Of(set), 1000, int.MaxValue);
	}

	[Fact]
	public void CreateHashSet_ThrowsTheExceptionOfTheSetsConstructor()
	{
		// No array can hold int.MaxValue buckets, so the set's constructor fails before it allocates anything. On .NET 10,
		// Activator.CreateInstance would wrap the exception in a TargetInvocationException. The net48 run tests the
		// netstandard2.0 build, which reaches the constructor through Activator.CreateInstance, and .NET Framework doesn't
		// wrap this exception.
		Assert.Throws<OutOfMemoryException>(() => SetWrapper.CreateHashSet(typeof(int), int.MaxValue, comparer: null));
	}

	[Fact]
	public void AddRange_PassesEachItemBeforeConversionAndItsExceptionToAddFailed()
	{
		SetWrapper wrapper = SetWrapper.CreateHashSet(typeof(string), 0, new ThrowingComparer());
		var failures = new List<(object? Item, Exception Exception)>();
		wrapper.AddFailed = (item, exception) => failures.Add((item, exception));
		object?[] items = [1, 2];

		wrapper.AddRange(items);

		// The wrapper converts 1 to "1" before the set calls the comparer, but the cmdlets' errors target the element as
		// it was in the input.
		Assert.Equal(items, failures.Select(failure => failure.Item));
		Assert.All(failures, failure => Assert.IsType<NotSupportedException>(failure.Exception));
		Assert.Empty(wrapper.AsSet());
	}

	[Fact]
	public void AddRange_LetsAnErrorFromTheComparerReachTheCallerInsteadOfAddFailed()
	{
		using RunspaceScope scope = _runspace.Enter();
		var block = new EqualityBlock(ScriptBlock.Create("$x -eq $y"), new HashBlock(ScriptBlock.Create("throw 'boom'")), additionalVariables: null);
		SetWrapper wrapper = SetWrapper.CreateHashSet(typeof(string), 0, block);
		bool reported = false;
		wrapper.AddFailed = (item, exception) => reported = true;
		object?[] items = ["a"];

		// The cmdlets rely on the error reaching PowerShell as it is, so it can end the script the way it does from
		// ForEach-Object. AddFailed would turn it into a non-terminating error.
		RuntimeException thrown = Assert.ThrowsAny<RuntimeException>(() => wrapper.AddRange(items));

		Assert.Equal("boom", thrown.Message);
		Assert.False(reported);
		Assert.Empty(wrapper.AsSet());
	}

	/// <summary>
	/// Throws a <see cref="NotSupportedException"/> from every comparison, so a set that uses it can't add anything.
	/// </summary>
	private sealed class ThrowingComparer : IEqualityComparer
	{
		public new bool Equals(object? x, object? y)
		{
			throw new NotSupportedException();
		}

		public int GetHashCode(object obj)
		{
			throw new NotSupportedException();
		}
	}
}
