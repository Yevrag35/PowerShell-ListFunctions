namespace ListFunctions.Modern;

/// <summary>
/// Defines a method that computes the hash code of an object by running a PowerShell script block.
/// </summary>
/// <remarks>
/// <see cref="EqualityBlock"/> uses an implementation of this interface for its
/// <see cref="EqualityBlock.GetHashCode(object)"/> method.
/// </remarks>
public interface IHashBlock
{
	/// <summary>
	/// Computes the hash code of the specified object by running the script block.
	/// </summary>
	/// <remarks>
	/// Implementations decide how they report a <see langword="null"/> object or a script block that fails.
	/// <see cref="HashBlock"/> reports a <see langword="null"/> object, and output that isn't a hash code, as a
	/// <see cref="Exceptions.HashCodeScriptException"/>. It lets an exception that the script block throws reach the caller
	/// unchanged.
	/// </remarks>
	/// <param name="obj">The object to compute the hash code of. This value must not be <see langword="null"/>.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object, or <see langword="null"/> for none.</param>
	/// <returns>The hash code of <paramref name="obj"/>.</returns>
	int GetHashCode([DisallowNull] object obj, IEnumerable<PSVariable>? additionalVariables);
}

/// <summary>
/// Defines a method that computes the hash code of an object of a known type by running a PowerShell script block.
/// </summary>
/// <remarks>
/// <para>
/// TODO: Nothing in this assembly implements or uses this interface. Clarify whether it replaces or duplicates
/// <see cref="IHashBlock"/>.
/// </para>
/// </remarks>
public interface IHashCodeBlock
{
	/// <summary>
	/// Gets the type of the objects that the hash code block hashes.
	/// </summary>
	/// <value>The type of the objects to hash.</value>
	Type HashesType { get; }
	/// <summary>
	/// Computes the hash code of the specified object by running the script block.
	/// </summary>
	/// <param name="obj">The object to compute the hash code of.</param>
	/// <param name="additionalVariables">The variables to define in the script block's scope along with the object.</param>
	/// <returns>The hash code of <paramref name="obj"/>.</returns>
	int GetHashCode(object obj, IEnumerable<PSVariable> additionalVariables);
}
