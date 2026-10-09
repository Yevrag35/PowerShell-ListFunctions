using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class AddMethodInvokerTests
{
	[Fact]
	public void TryInvoke_ReturnsTheExceptionThatAddThrows()
	{
		var constructor = new HashSetCtor(typeof(string), new ThrowingComparer());
		object set = constructor.Construct();
		var invoker = new AddMethodInvoker(constructor);

		Assert.False(invoker.TryInvoke(set, ["a"], addIfNull: false, out Exception? caught));

		// The set's comparer throws, and reflection wraps the exception in a TargetInvocationException.
		Assert.IsType<NotSupportedException>(caught);
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
