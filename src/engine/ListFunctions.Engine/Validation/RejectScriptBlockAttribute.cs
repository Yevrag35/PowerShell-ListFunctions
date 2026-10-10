using ListFunctions.Extensions;

namespace ListFunctions.Validation;

/// <summary>
/// Rejects a script block passed to a cmdlet parameter that takes a string, such as a property name, and names the
/// parameter that takes the script block instead.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell converts a script block passed to a <see cref="string"/> parameter to the script's text, so the cmdlet
/// can't tell it from a string. A transformation attribute sees the argument before that conversion. The attribute
/// rejects a <see cref="ScriptBlock"/>, including one wrapped in a <see cref="PSObject"/>, and returns any other
/// argument unchanged for PowerShell to convert.
/// </para>
/// <para>
/// The exception that rejects a script block holds a <see cref="PSInvalidCastException"/>, so PowerShell treats it as a
/// failed conversion. When PowerShell binds an argument by position, it first tries the parameters at that position
/// without converting the argument, and it ignores failed conversions while it does. A script block passed by position
/// then still binds to a <see cref="ScriptBlock"/> parameter at the same position in another parameter set. With any
/// other exception, PowerShell reports the error for the first parameter that it tries, and the binding fails.
/// </para>
/// <para>
/// The attribute doesn't change after construction, so it is thread-safe.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class RejectScriptBlockAttribute : ArgumentTransformationAttribute
{
	/// <summary>
	/// The name of the parameter that takes a script block instead, which the error message names.
	/// </summary>
	private readonly string _scriptBlockParameterName;

	/// <summary>
	/// Initializes a new instance of <see cref="RejectScriptBlockAttribute"/> with the name of the parameter that takes a
	/// script block instead.
	/// </summary>
	/// <param name="scriptBlockParameterName">The name of the parameter that takes a script block instead, without the hyphen, such as <c>KeySelector</c>. The error message names it. This value must not be <see langword="null"/>.</param>
	public RejectScriptBlockAttribute(string scriptBlockParameterName)
	{
		_scriptBlockParameterName = scriptBlockParameterName;
	}

	/// <summary>
	/// Returns the specified argument unchanged, unless it is a script block.
	/// </summary>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. The method doesn't use it.</param>
	/// <param name="inputData">The argument to check. This value can be <see langword="null"/>, and it can be wrapped in a <see cref="PSObject"/>.</param>
	/// <returns><paramref name="inputData"/>, unchanged.</returns>
	/// <exception cref="ArgumentTransformationMetadataException">Thrown when <paramref name="inputData"/> is a script block, or a <see cref="PSObject"/> that wraps one. Its inner exception is a <see cref="PSInvalidCastException"/> with the same message.</exception>
	public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData)
	{
		if (inputData.GetBaseObject() is ScriptBlock)
		{
			string message = $"The parameter doesn't take a script block. Pass the script block to -{_scriptBlockParameterName} instead.";
			throw new ArgumentTransformationMetadataException(message, new PSInvalidCastException(message));
		}

		return inputData;
	}
}
