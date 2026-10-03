#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Linq;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides polyfills for <see cref="IEnumerable{T}"/> extension methods that older target frameworks lack.
/// </summary>
/// <remarks>
/// The class lives in the <c>System.Linq</c> namespace so that callers pick up the polyfills with the same <see langword="using"/>
/// directive as the built-in methods. On targets that already have a method, the polyfill is compiled out and the built-in
/// method is used instead.
/// </remarks>
internal static class EnumerableExtensions
{
#if !NET5_0_OR_GREATER
	/// <summary>
	/// Attempts to get the number of elements in a sequence without enumerating it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Polyfills <c>Enumerable.TryGetNonEnumeratedCount</c> for targets earlier than .NET 5. The method succeeds when
	/// <paramref name="collection"/> is an array, a <see cref="List{T}"/>, a <see cref="HashSet{T}"/>, or implements
	/// <see cref="IReadOnlyCollection{T}"/>, <see cref="ICollection{T}"/>, or <see cref="ICollection"/>. It never enumerates
	/// the sequence.
	/// </para>
	/// <para>
	/// Unlike the built-in method, this one does not throw when <paramref name="collection"/> is <see langword="null"/>; it
	/// returns <see langword="false"/> instead.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">The type of the elements of <paramref name="collection"/>.</typeparam>
	/// <param name="collection">The sequence to count.</param>
	/// <param name="count">
	/// When this method returns, contains the number of elements in <paramref name="collection"/> if the count is available
	/// without enumeration; otherwise, <c>0</c>.
	/// </param>
	/// <returns><see langword="true"/> if the count is available without enumeration; otherwise, <see langword="false"/>.</returns>
	internal static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T> collection, out int count)
	{
		switch (collection)
		{
			case Array array:
				count = array.Length;
				return true;

			case List<T> list:
				count = list.Count;
				return true;

			case HashSet<T> set:
				count = set.Count;
				return true;

			case IReadOnlyCollection<T> roCol:
				count = roCol.Count;
				return true;

			case ICollection<T> icol:
				count = icol.Count;
				return true;

			case ICollection nonGenCol:
				count = nonGenCol.Count;
				return true;

			default:
				count = 0;
				return false;
		}
	}
#endif
}