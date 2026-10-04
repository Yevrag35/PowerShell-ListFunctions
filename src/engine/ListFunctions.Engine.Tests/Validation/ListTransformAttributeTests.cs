using ListFunctions.Internal;
using ListFunctions.Validation;

namespace ListFunctions.Engine.Tests.Validation;

public sealed class ListTransformAttributeTests
{
	[Fact]
	public void Transform_ReturnsAListUnchanged()
	{
		var attribute = new ListTransformAttribute();
		var list = new List<int> { 1, 2 };

		Assert.Same(list, attribute.Transform(engineIntrinsics: null!, list));
	}

	[Fact]
	public void Transform_UnwrapsAPSObjectFirst()
	{
		var attribute = new ListTransformAttribute();
		var list = new List<int> { 1, 2 };

		Assert.Same(list, attribute.Transform(engineIntrinsics: null!, PSObject.AsPSObject(list)));
		Assert.Equal(5, Assert.IsType<PipelineItem>(attribute.Transform(engineIntrinsics: null!, PSObject.AsPSObject(5))).Value);
	}

	[Fact]
	public void Transform_CopiesAnArrayIntoAListOfObject()
	{
		var attribute = new ListTransformAttribute();
		object?[] array = [1, "two", null];

		var list = Assert.IsType<List<object?>>(attribute.Transform(engineIntrinsics: null!, array));
		Assert.Equal(array, list);
	}

	[Fact]
	public void Transform_CopiesAStringIntoAListOfItsCharacters()
	{
		var attribute = new ListTransformAttribute();

		var list = Assert.IsType<List<object?>>(attribute.Transform(engineIntrinsics: null!, "abc"));
		Assert.Equal(new object?[] { 'a', 'b', 'c' }, list);
	}

	[Fact]
	public void Transform_CopiesADictionaryIntoAListOfItsEntries()
	{
		var attribute = new ListTransformAttribute();
		var dictionary = new Dictionary<string, int> { ["a"] = 1 };

		var list = Assert.IsType<List<object?>>(attribute.Transform(engineIntrinsics: null!, dictionary));
		Assert.Equal(new object?[] { new KeyValuePair<string, int>("a", 1) }, list);
	}

	[Fact]
	public void Transform_CopiesAListWhoseTypeDerivesFromList()
	{
		var attribute = new ListTransformAttribute();
		var derived = new DerivedList { 1, 2 };

		var list = Assert.IsType<List<object?>>(attribute.Transform(engineIntrinsics: null!, derived));
		Assert.Equal(new object?[] { 1, 2 }, list);
	}

	[Fact]
	public void Transform_DoesNotReflectLaterChangesToTheArgument()
	{
		var attribute = new ListTransformAttribute();
		var source = new ArrayList { 1 };

		var list = Assert.IsType<List<object?>>(attribute.Transform(engineIntrinsics: null!, source));
		source.Add(2);

		Assert.Equal(new object?[] { 1 }, list);
	}

	[Theory]
	[InlineData(5)]
	[InlineData(null)]
	public void Transform_WrapsAnyOtherArgumentInAPipelineItem(object? argument)
	{
		var attribute = new ListTransformAttribute();

		var item = Assert.IsType<PipelineItem>(attribute.Transform(engineIntrinsics: null!, argument));
		Assert.Equal(argument, item.Value);
	}

	/// <summary>
	/// Represents a list of integers whose type derives from <see cref="List{T}"/> instead of being one.
	/// </summary>
	private sealed class DerivedList : List<int>
	{
	}
}
