namespace ListFunctions.Modern;

/// <summary>
/// Defines a method that computes the hash code of an object by running a PowerShell script block.
/// </summary>
internal interface IHashBlock
{
	/// <summary>
	/// Computes the hash code of the specified object by running the script block.
	/// </summary>
	/// <remarks>
	/// Implementations decide how they report a <see langword="null"/> object or a script block that fails.
	/// </remarks>
	/// <param name="obj">The object to compute the hash code of. This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object, or <see langword="null"/> for none.</param>
	/// <returns>The hash code of <paramref name="obj"/>.</returns>
	int GetHashCode([DisallowNull] object obj, IEnumerable<PSVariable>? additionalVariables);
}
