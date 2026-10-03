namespace ListFunctions.Internal;

/// <summary>
/// Holds a pair of stack-only flags that convert to <see langword="true"/> only when both flags are set.
/// </summary>
/// <remarks>
/// <para>
/// Callers set each flag independently as they observe two separate conditions, and then test the instance as a
/// <see cref="bool"/> to learn whether both conditions occurred. For example, a loop can stop as soon as the instance
/// converts to <see langword="true"/>.
/// </para>
/// <para>
/// As a <see langword="ref"/> struct, an instance lives only on the stack and cannot be boxed, captured, or stored in a field
/// of a class.
/// </para>
/// </remarks>
internal ref struct DoubleBool
{
	/// <summary>
	/// The first flag.
	/// </summary>
	internal bool Bool1;
	/// <summary>
	/// The second flag.
	/// </summary>
	internal bool Bool2;

	/// <summary>
	/// Initializes a new <see cref="DoubleBool"/> instance with both flags set to <paramref name="initialize"/>.
	/// </summary>
	/// <param name="initialize">The initial value of <see cref="Bool1"/> and <see cref="Bool2"/>.</param>
	private DoubleBool(bool initialize)
	{
		Bool1 = initialize;
		Bool2 = initialize;
	}

	/// <summary>
	/// Creates a new <see cref="DoubleBool"/> with both flags cleared.
	/// </summary>
	/// <returns>A <see cref="DoubleBool"/> whose <see cref="Bool1"/> and <see cref="Bool2"/> are <see langword="false"/>.</returns>
	internal static DoubleBool InitializeNew() => new DoubleBool(false);

	/// <summary>
	/// Converts a <see cref="DoubleBool"/> to a <see cref="bool"/> by combining both flags with a logical AND.
	/// </summary>
	/// <remarks>
	/// The conversion uses the non-short-circuiting <c>&amp;</c> operator, so it reads both flags without branching.
	/// </remarks>
	/// <param name="dub">The instance to convert.</param>
	/// <returns><see langword="true"/> when both <see cref="Bool1"/> and <see cref="Bool2"/> are set; otherwise, <see langword="false"/>.</returns>
	public static implicit operator bool(DoubleBool dub) => dub.Bool1 & dub.Bool2;
}
