#if NETCOREAPP
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Management.Automation.ValidateNotNullOrWhiteSpaceAttribute))]
#else
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Management.Automation;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Specifies that a parameter must not be null, empty, or consist only of white-space characters.
/// </summary>
/// <remarks>Apply this attribute to parameters to enforce that they contain at least one non-white-space
/// character. This validation is typically used to ensure that required string arguments are provided and are
/// meaningful.</remarks>
public sealed class ValidateNotNullOrWhiteSpaceAttribute : ValidateArgumentsAttribute
{
	private const string NULL_EMPTY_ERROR = "The argument is null or empty. Provide an argument that is not null or empty, and then try the command again.";
	private const string WHITESPACE_ERROR = "The argument is null, empty, or consists of only white-space characters. Provide an argument that contains non white-space characters, and then try the command again.";

	/// <summary>
	/// Validates that the argument is not null, empty, or consists only of white-space characters. If the argument is invalid, throws a <see cref="ValidationMetadataException"/> with an appropriate error message.
	/// </summary>
	/// <param name="arguments">The argument to validate.</param>
	/// <param name="engineIntrinsics">The engine intrinsics.</param>
	/// <exception cref="ValidationMetadataException">Thrown when the argument is null, empty, or consists only of white-space characters.</exception>
	protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
	{
		if (arguments is null)
		{
			throw new ValidationMetadataException(
				NULL_EMPTY_ERROR);
		}

		if (!LanguagePrimitives.TryConvertTo(arguments, out string s) || s is null)
		{
			return;
		}

		if (s.Equals(string.Empty))
		{
			throw new ValidationMetadataException(
				NULL_EMPTY_ERROR);
		}

		foreach (char c in s)
		{
			if (!char.IsWhiteSpace(c))
			{
				return;
			}
		}

		throw new ValidationMetadataException(WHITESPACE_ERROR);
	}
}
#endif