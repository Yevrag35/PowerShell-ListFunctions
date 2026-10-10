using ListFunctions.Modern;

namespace ListFunctions.Engine.Tests.Modern;

public sealed class EqualityComparerAdapterTests
{
	[Fact]
	[Trait("Category", "Bug09")]
	public void Equals_CallsTheInnerComparer()
	{
		var adapter = new EqualityComparerAdapter<int>(new LastDigitComparer());

		Assert.True(adapter.Equals(1, 11));
		Assert.False(adapter.Equals(1, 2));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void GetHashCode_CallsTheInnerComparer()
	{
		var adapter = new EqualityComparerAdapter<int>(new LastDigitComparer());

		Assert.Equal(3, adapter.GetHashCode(13));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void NonGenericMembers_CallTheInnerComparer()
	{
		IEqualityComparer adapter = new EqualityComparerAdapter<int>(new LastDigitComparer());

		Assert.True(adapter.Equals(1, 11));
		Assert.Equal(3, adapter.GetHashCode(13));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void HashSet_UsesTheInnerComparerToFindDuplicates()
	{
		var set = new HashSet<int>(new EqualityComparerAdapter<int>(new LastDigitComparer()));

		Assert.True(set.Add(1));
		Assert.False(set.Add(11));
		Assert.True(set.Add(2));
	}

	[Fact]
	[Trait("Category", "Bug09")]
	public void Constructor_ThrowsWhenTheComparerIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => new EqualityComparerAdapter<int>(null!));
	}

	/// <summary>
	/// Compares integers by their last digit, through the non-generic interface only.
	/// </summary>
	private sealed class LastDigitComparer : IEqualityComparer
	{
		public new bool Equals(object? x, object? y)
		{
			return x is int left && y is int right && left % 10 == right % 10;
		}

		public int GetHashCode(object obj)
		{
			return (int)obj % 10;
		}
	}
}
