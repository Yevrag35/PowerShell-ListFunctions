namespace ListFunctions.Modern;

/// <summary>
/// Represents a list of objects that the module creates to collect several values under one entry.
/// </summary>
/// <remarks>
/// <para>
/// Because the list has its own type, code can tell a list that the module collected apart from a value that happens
/// to be a list, such as an <see cref="ArrayList"/> or an <see cref="object"/> array.
/// </para>
/// <para>
/// The list accepts <see langword="null"/> elements. Instances aren't thread-safe.
/// </para>
/// </remarks>
public sealed class ObjectList : List<object?>
{
	/// <summary>
	/// Initializes a new, empty <see cref="ObjectList"/> instance with an initial capacity of 2.
	/// </summary>
	public ObjectList() : base(2) { }
}