namespace ListFunctions.Internal;

/// <summary>
/// Compares <see cref="PSVariable"/> instances by name only, ignoring case.
/// </summary>
/// <remarks>
/// <para>
/// Names are compared with <see cref="StringComparer.OrdinalIgnoreCase"/>, which matches how PowerShell treats
/// variable names. The variables' values, options, and other members are not compared.
/// </para>
/// <para>
/// This type has no state and is thread-safe.
/// </para>
/// </remarks>
internal sealed class PSVariableNameEquality : IEqualityComparer<PSVariable>
{
	/// <summary>
	/// Determines whether two variables have the same name, ignoring case.
	/// </summary>
	/// <param name="x">The first variable to compare. This value can be <see langword="null"/>.</param>
	/// <param name="y">The second variable to compare. This value can be <see langword="null"/>.</param>
	/// <returns>
	/// <see langword="true"/> when both variables are the same instance, both are <see langword="null"/>, or their names are equal
	/// ignoring case; otherwise, <see langword="false"/>. A <see langword="null"/> variable never equals a non-<see langword="null"/> one.
	/// </returns>
	public bool Equals(PSVariable? x, PSVariable? y)
	{
		if (ReferenceEquals(x, y) || (x is null && y is null))
		{
			return true;
		}

		return StringComparer.OrdinalIgnoreCase.Equals(x?.Name, y?.Name);
	}

	/// <summary>
	/// Returns a case-insensitive hash code for the name of the specified variable.
	/// </summary>
	/// <param name="obj">The variable whose name to hash. This value must not be <see langword="null"/>.</param>
	/// <returns>A hash code that is equal for any two variables whose names differ only in case.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is null.</exception>
	public int GetHashCode(PSVariable obj)
	{
		ArgumentNullException.ThrowIfNull(obj);
		return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name);
	}
}
