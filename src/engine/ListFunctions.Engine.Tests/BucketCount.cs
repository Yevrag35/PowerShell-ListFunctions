namespace ListFunctions.Engine.Tests;

/// <summary>
/// Provides the length of a hash-based collection's bucket array, which shows the capacity the collection was created
/// with.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="HashSet{T}"/>, <see cref="Dictionary{TKey, TValue}"/>, or <see cref="Hashtable"/> sizes its bucket array
/// for the capacity it's created with, so the bucket count shows whether a constructor received a capacity. .NET
/// Framework has no public API that reports a collection's capacity, so <see cref="Of(object)"/> reads a private field:
/// <c>_buckets</c> on .NET 10, <c>m_buckets</c> for a .NET Framework <see cref="HashSet{T}"/>, and <c>buckets</c> for a
/// .NET Framework <see cref="Dictionary{TKey, TValue}"/> or <see cref="Hashtable"/>.
/// </para>
/// <para>
/// The Pester tests read the same fields through <c>tests/Get-BucketCount.ps1</c>.
/// </para>
/// </remarks>
internal static class BucketCount
{
	private static readonly string[] s_fieldNames = ["_buckets", "m_buckets", "buckets"];

	/// <summary>
	/// Returns the length of the specified collection's bucket array.
	/// </summary>
	/// <param name="collection">
	/// The <see cref="HashSet{T}"/>, <see cref="Dictionary{TKey, TValue}"/>, or <see cref="Hashtable"/> to inspect. This
	/// value must not be <see langword="null"/>.
	/// </param>
	/// <returns>The length of the bucket array, or 0 when the collection hasn't allocated one yet.</returns>
	/// <exception cref="ArgumentException">Thrown when the type of <paramref name="collection"/> has none of the bucket array fields.</exception>
	public static int Of(object collection)
	{
		Type type = collection.GetType();
		foreach (string name in s_fieldNames)
		{
			FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
			if (field is not null)
			{
				return field.GetValue(collection) is Array buckets ? buckets.Length : 0;
			}
		}

		throw new ArgumentException($"Can't find the bucket array of a {type.FullName}.", nameof(collection));
	}
}
