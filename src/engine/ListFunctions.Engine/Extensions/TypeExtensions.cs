namespace ListFunctions.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="Type"/> class.
/// </summary>
public static class TypeExtensions
{
	/// <summary>
	/// Gets the full name of the type, or its simple name if it has no full name.
	/// </summary>
	/// <remarks>
	/// <see cref="Type.FullName"/> is <see langword="null"/> for a generic type parameter, an array or pointer of one,
	/// and a generic type that is constructed from another type's generic parameters. This method returns
	/// <see cref="MemberInfo.Name"/> for those types instead.
	/// </remarks>
	/// <param name="type">The type whose name to get. This value must not be <see langword="null"/>.</param>
	/// <returns>The value of <see cref="Type.FullName"/> if it isn't <see langword="null"/>; otherwise, the value of <see cref="MemberInfo.Name"/>.</returns>
	/// <exception cref="NullReferenceException">Thrown when <paramref name="type"/> is null.</exception>
	[DebuggerStepThrough]
	public static string GetTypeName(this Type type)
	{
		return type.FullName ?? type.Name;
	}
}
