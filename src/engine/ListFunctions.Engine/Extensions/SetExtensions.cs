#if NET5_0_OR_GREATER
namespace ZLinq;

/// <summary>
/// Provides extension methods that add the elements of a ZLinq value enumerable to a <see cref="HashSet{T}"/>.
/// </summary>
/// <remarks>
/// This class exists only in the .NET 5 and later builds.
/// </remarks>
public static class SetExtensions
{
	/// <summary>
	/// Adds every element of the value enumerable to the set, like <see cref="HashSet{T}.UnionWith(IEnumerable{T})"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method takes <paramref name="collection"/> by reference so that the enumerable, which is a struct, isn't copied
	/// and isn't boxed into an <see cref="IEnumerable{T}"/>. Elements that the set already contains, according to its
	/// <see cref="HashSet{T}.Comparer"/>, are skipped.
	/// </para>
	/// <para>
	/// The method enumerates <paramref name="collection"/> once. Treat it as consumed afterward, as with any ZLinq
	/// value enumerable.
	/// </para>
	/// <para>
	/// The <c>struct</c> constraint on <typeparamref name="TEnumerator"/> keeps the enumerator unboxed. On .NET 8 and
	/// later, the <c>allows ref struct</c> anti-constraint also lets <typeparamref name="TEnumerator"/> be a ref struct,
	/// such as an enumerator over a <see cref="Span{T}"/>.
	/// </para>
	/// <para>
	/// The method is not thread-safe, because <see cref="HashSet{T}"/> isn't thread-safe for writes.
	/// </para>
	/// </remarks>
	/// <typeparam name="TEnumerator">The type of the ZLinq enumerator that produces the elements.</typeparam>
	/// <typeparam name="T">The type of the elements in the set and the enumerable.</typeparam>
	/// <param name="set">The set to add the elements to. This value must not be <see langword="null"/>.</param>
	/// <param name="collection">The value enumerable whose elements to add.</param>
	/// <exception cref="NullReferenceException">Thrown when <paramref name="set"/> is null and <paramref name="collection"/> has at least one element.</exception>
	public static void UnionWithRef<TEnumerator, T>(this HashSet<T> set, ref ValueEnumerable<TEnumerator, T> collection)
		where TEnumerator : struct, IValueEnumerator<T>
#if NET8_0_OR_GREATER
			, allows ref struct
#endif
	{
		foreach (T item in collection)
		{
			_ = set.Add(item);
		}
	}
}
#endif