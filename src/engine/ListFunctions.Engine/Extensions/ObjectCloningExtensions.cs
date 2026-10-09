#nullable enable

namespace ListFunctions.Extensions;

/// <summary>
/// Provides extension methods for copying objects and arrays, including common PowerShell object types.
/// </summary>
/// <remarks>
/// These methods copy objects that implement <see cref="ICloneable"/>, and PowerShell objects such as
/// <see cref="PSObject"/>, <see cref="PSCustomObject"/>, and <see cref="PSMemberInfo"/>. Each object type decides how
/// deep its own copy goes.
/// </remarks>
internal static class ObjectCloningExtensions
{
	/// <summary>
	/// Creates a copy of the specified object if it supports copying; otherwise, returns the original object.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method checks the object's type in this order and uses the first copy mechanism that applies:
	/// <see cref="ICloneable.Clone"/>; <see cref="PSObject.Copy"/>; for a <see cref="PSCustomObject"/>, the same
	/// method on a <see cref="PSObject"/> that wraps it; and <see cref="PSMemberInfo.Copy"/>.
	/// </para>
	/// <para>
	/// The copy is only as deep as the type's own copy mechanism. For example, <see cref="Array.Clone"/> makes a shallow
	/// copy, so the elements of a copied array are the same references as the original's.
	/// </para>
	/// </remarks>
	/// <param name="obj">The object to copy. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// A copy of <paramref name="obj"/> if it implements <see cref="ICloneable"/> or is a <see cref="PSObject"/>,
	/// <see cref="PSCustomObject"/>, or <see cref="PSMemberInfo"/>; otherwise, <paramref name="obj"/> itself.
	/// <see langword="null"/> if <paramref name="obj"/> is <see langword="null"/>.
	/// </returns>
	[return: NotNullIfNotNull(nameof(obj))]
	internal static object? CloneIf(this object? obj)
	{
		return obj switch
		{
			PSObject pso => pso.Copy(),
			PSCustomObject customObj => PSObject.AsPSObject(customObj).Copy(),
			PSMemberInfo member => member.Copy(),
			ICloneable cloneable => cloneable.Clone(),
			_ => obj,
		};
	}
}