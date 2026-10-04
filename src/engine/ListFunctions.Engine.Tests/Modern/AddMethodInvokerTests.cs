using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class AddMethodInvokerTests
{
	[Fact]
	public void TryInvoke_ReturnsTheExceptionThatAddThrows()
	{
		var constructor = new DictionaryCtor(comparer: null, typeof(string), typeof(int));
		object dictionary = constructor.Construct();
		var invoker = new AddMethodInvoker(constructor);
		Assert.True(invoker.TryInvoke(dictionary, ["a", 1], addIfNull: false, out _));

		Assert.False(invoker.TryInvoke(dictionary, ["a", 2], addIfNull: false, out Exception? caught));

		// Add throws an ArgumentException for the duplicate key, and reflection wraps it in a TargetInvocationException.
		Assert.IsType<ArgumentException>(caught);
	}
}
