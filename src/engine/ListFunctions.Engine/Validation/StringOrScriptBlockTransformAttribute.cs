using ListFunctions.Extensions;

namespace ListFunctions.Validation;

/// <summary>
/// Converts a cmdlet parameter's argument to a string or a script block, and rejects an argument that is neither.
/// </summary>
/// <remarks>
/// <para>
/// The attribute is for a parameter of type <see cref="object"/> that takes either a name or a script block.
/// PowerShell passes an argument to such a parameter as it is, so a string can arrive wrapped in a
/// <see cref="PSObject"/>, as a line that <c>Get-Content</c> reads does, and fail a type test. The attribute unwraps the
/// argument and returns <see langword="null"/>, a <see cref="string"/>, or a <see cref="ScriptBlock"/>. It rejects any
/// other argument, such as a number or an array of names, which the cmdlet would otherwise have to ignore or reject
/// itself.
/// </para>
/// <para>
/// Like <see cref="RejectScriptBlockAttribute"/>, the attribute rejects an argument with an exception that holds a
/// <see cref="PSInvalidCastException"/>, so PowerShell can still bind an argument passed by position to another
/// parameter at the same position.
/// </para>
/// <para>
/// The attribute keeps no state, so it is thread-safe.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class StringOrScriptBlockTransformAttribute : ArgumentTransformationAttribute
{
	/// <summary>
	/// Converts the specified argument to a string or a script block.
	/// </summary>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. The method doesn't use it.</param>
	/// <param name="inputData">The argument to convert. This value can be <see langword="null"/>, and it can be wrapped in a <see cref="PSObject"/>.</param>
	/// <returns>
	/// <paramref name="inputData"/> unwrapped from its <see cref="PSObject"/>, which is <see langword="null"/>, a
	/// <see cref="string"/>, or a <see cref="ScriptBlock"/>.
	/// </returns>
	/// <exception cref="ArgumentTransformationMetadataException">Thrown when <paramref name="inputData"/>, unwrapped, isn't null, a string, or a script block. Its inner exception is a <see cref="PSInvalidCastException"/> with the same message.</exception>
	public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData)
	{
		object? target = inputData.GetBaseObject();
		if (target is null or string or ScriptBlock)
		{
			return target;
		}

		string message = $"The argument must be a string or a script block, not a value of type '{target.GetType().GetTypeName()}'.";
		throw new ArgumentTransformationMetadataException(message, new PSInvalidCastException(message));
	}
}
