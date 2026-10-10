namespace ListFunctions.Engine.Tests;

/// <summary>
/// Runs a validation attribute's check on an argument, the way PowerShell does when it binds a parameter.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell calls the protected <c>Validate</c> method of each <see cref="ValidateArgumentsAttribute"/> on a parameter
/// that it binds. A test can't call a protected method of a sealed attribute, so
/// <see cref="Validate(ValidateArgumentsAttribute, object)"/> calls it through a delegate that the class creates once
/// with reflection.
/// </para>
/// <para>
/// Unlike <see cref="MethodBase.Invoke(object, object[])"/>, the delegate doesn't wrap exceptions in a
/// <see cref="TargetInvocationException"/>, so a test sees the exception that the attribute throws.
/// </para>
/// </remarks>
internal static class ArgumentValidator
{
	private static readonly Action<ValidateArgumentsAttribute, object?, EngineIntrinsics?> s_validate = CreateValidate();

	/// <summary>
	/// Runs the specified attribute's check on the specified argument.
	/// </summary>
	/// <remarks>
	/// The attribute gets <see langword="null"/> for its engine intrinsics, so use this method only with an attribute that
	/// doesn't read them.
	/// </remarks>
	/// <param name="attribute">The attribute whose check to run. This value must not be <see langword="null"/>.</param>
	/// <param name="argument">The argument to check. This value can be <see langword="null"/>.</param>
	/// <exception cref="ValidationMetadataException">Thrown when <paramref name="argument"/> fails the check.</exception>
	public static void Validate(ValidateArgumentsAttribute attribute, object? argument)
	{
		s_validate(attribute, argument, null);
	}

	/// <summary>
	/// Creates a delegate that calls the protected <c>Validate</c> method of a <see cref="ValidateArgumentsAttribute"/>.
	/// </summary>
	/// <remarks>
	/// The delegate is bound to the abstract method, so it calls the override of the attribute it receives.
	/// </remarks>
	/// <returns>A delegate that takes the attribute, the argument, and the engine intrinsics, in that order.</returns>
	/// <exception cref="MissingMethodException">Thrown when the loaded PowerShell's <see cref="ValidateArgumentsAttribute"/> has no non-public instance method named Validate.</exception>
	private static Action<ValidateArgumentsAttribute, object?, EngineIntrinsics?> CreateValidate()
	{
		MethodInfo method = typeof(ValidateArgumentsAttribute).GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingMethodException(nameof(ValidateArgumentsAttribute), "Validate");

		return (Action<ValidateArgumentsAttribute, object?, EngineIntrinsics?>)Delegate.CreateDelegate(
			typeof(Action<ValidateArgumentsAttribute, object, EngineIntrinsics>),
			method);
	}
}
