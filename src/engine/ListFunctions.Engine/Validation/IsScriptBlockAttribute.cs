using ListFunctions.Extensions;

namespace ListFunctions.Validation;

/// <summary>
/// Validates that a cmdlet parameter's <see cref="ScriptBlock"/> argument has a body that can be invoked.
/// </summary>
/// <remarks>
/// <para>
/// A script block passes when PowerShell can invoke it as a single block that contains at least one statement, the way
/// <see cref="ScriptBlock.InvokeWithContext(Dictionary{string, ScriptBlock}, List{PSVariable}, object[])"/> invokes it.
/// That block is the <c>process</c> block when there is one, and otherwise the <c>end</c> block, which holds the
/// statements of a script block without named blocks. An empty script block, such as <c>{ }</c>, fails, and so does a
/// script block that has a <c>begin</c> block, a <c>clean</c> block, or both a <c>process</c> block and an <c>end</c>
/// block.
/// </para>
/// <para>
/// The attribute checks only <see cref="ScriptBlock"/> arguments. An argument of any other type, including
/// <see langword="null"/>, passes, so the attribute can also check a parameter that takes a script block or another
/// value.
/// </para>
/// <para>
/// The cmdlets put the attribute on every parameter that takes a script block, so a script block that they can't run
/// fails parameter binding, before the cmdlet reads any input. The attribute keeps no state, so it is thread-safe.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class IsScriptBlockAttribute : ValidateArgumentsAttribute
{
	/// <summary>
	/// Validates that the specified argument, when it is a <see cref="ScriptBlock"/>, has a body that can be invoked.
	/// </summary>
	/// <remarks>
	/// The error message states the rule that the class remarks describe, so it's the same for every script block that
	/// fails.
	/// </remarks>
	/// <param name="arguments">The argument to validate. Only a <see cref="ScriptBlock"/> is checked.</param>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. The method doesn't use it.</param>
	/// <exception cref="ValidationMetadataException">Thrown when <paramref name="arguments"/> is a script block without a body that can be invoked.</exception>
	protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
	{
		if (arguments is ScriptBlock block && !block.IsProperScriptBlock())
		{
			throw new ValidationMetadataException(ScriptBlockExtensions.ImproperScriptBlockMessage);
		}
	}
}
