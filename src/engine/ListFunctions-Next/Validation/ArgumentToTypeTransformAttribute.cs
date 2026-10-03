using ListFunctions.Extensions;

#nullable enable

namespace ListFunctions.Validation;

/// <summary>
/// Converts a cmdlet parameter's argument to the <see cref="Type"/> that it names.
/// </summary>
/// <remarks>
/// <para>
/// The argument can be a <see cref="Type"/>, a <see cref="ScriptBlock"/> that contains a type literal, such as
/// <c>{ [string] }</c>, or a type name, with or without brackets, such as <c>string</c> or <c>[string]</c>. A
/// <see cref="PSObject"/> argument is unwrapped first.
/// </para>
/// <para>
/// A script block or a type name that doesn't resolve to a type causes an <see cref="ArgumentException"/>, except when
/// the calling module is PSReadLine, in which case the attribute returns <see cref="object"/>. An argument of any other
/// kind, including <see langword="null"/>, converts to <see cref="object"/>.
/// </para>
/// <para>
/// The attribute parses script blocks and type names but never runs them. It keeps no state between calls, so it is
/// thread-safe.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class ArgumentToTypeTransformAttribute : ArgumentTransformationAttribute
{
	/// <summary>
	/// The name of the PSReadLine module, which gets <see cref="object"/> instead of an exception for an argument that
	/// doesn't resolve to a type.
	/// </summary>
	private const string PSREADLINE = "PSReadLine";

	/// <summary>
	/// Converts the specified argument to the <see cref="Type"/> that it names.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see cref="Type"/> is returned unchanged. For a <see cref="ScriptBlock"/>, the method resolves the first type
	/// literal in its syntax tree, outside any nested script block. A <see cref="string"/> resolves as described in
	/// <see cref="ResolveFromName(string, PSModuleInfo)"/>. Any other argument, including <see langword="null"/>,
	/// converts to <see cref="object"/>.
	/// </para>
	/// <para>
	/// The calling module, taken from <paramref name="engineIntrinsics"/>, matters only when the argument doesn't resolve:
	/// when that module is PSReadLine, the method returns <see cref="object"/> instead of throwing.
	/// </para>
	/// </remarks>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. This value must not be <see langword="null"/>.</param>
	/// <param name="inputData">The argument to convert. This value can be <see langword="null"/>, and it can be wrapped in a <see cref="PSObject"/>.</param>
	/// <returns>The <see cref="Type"/> that <paramref name="inputData"/> names, or <see cref="object"/> when <paramref name="inputData"/> isn't a type, script block, or string.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="inputData"/> is a script block or string that doesn't resolve to a type, and the calling module isn't PSReadLine.</exception>
	public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData)
	{
		object? target = inputData.GetBaseObject();

		switch (target)
		{
			case Type type:
				return type;

			case ScriptBlock block:
				return ResolveFromAst(block.Ast, engineIntrinsics.SessionState.Module);

			case string typeName:
				return ResolveFromName(typeName, engineIntrinsics.SessionState.Module);

			default:
				return typeof(object);
		}
	}

	/// <summary>
	/// The attribute's name as it is written on a parameter, <c>[ArgumentToTypeTransform]</c>.
	/// </summary>
	private static readonly string s_name = $"[{nameof(ArgumentToTypeTransformAttribute).Replace("Attribute", "")}]";

	/// <summary>
	/// Returns the attribute's name as it is written on a parameter, without the <c>Attribute</c> suffix.
	/// </summary>
	/// <returns>The string <c>[ArgumentToTypeTransform]</c>.</returns>
	public override string ToString()
	{
		return s_name;
	}

	/// <summary>
	/// Resolves the first type literal in the specified syntax tree to a .NET or custom-defined type.
	/// </summary>
	/// <remarks>
	/// The method searches <paramref name="ast"/> for the first <see cref="TypeExpressionAst"/>, such as <c>[string]</c>,
	/// and skips nested script blocks. A cast, such as <c>[int]$x</c>, is not a type literal and doesn't count.
	/// </remarks>
	/// <param name="ast">The syntax tree to search. This value must not be <see langword="null"/>.</param>
	/// <param name="runningModule">
	/// The calling module, or <see langword="null"/> when the call doesn't come from a module. When it is PSReadLine, the method
	/// returns <see cref="object"/> instead of throwing.
	/// </param>
	/// <returns>
	/// The type that the first type literal in <paramref name="ast"/> names, or <see cref="object"/> when there is no type to
	/// return and <paramref name="runningModule"/> is PSReadLine.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="ast"/> contains no type literal, or its first type literal names an unknown type, and <paramref name="runningModule"/> isn't PSReadLine.</exception>
	private static Type ResolveFromAst(Ast ast, PSModuleInfo? runningModule)
	{
		try
		{
			var first = (TypeExpressionAst?)ast.Find(x => x is TypeExpressionAst, false);

			return first?.TypeName.GetReflectionType() ?? throw new ParseException($"{ast.Extent.Text} is not a type expression.");
		}
		catch (ParseException e)
		{
			if (PSREADLINE.Equals(runningModule?.Name, StringComparison.OrdinalIgnoreCase))
			{
				return typeof(object);
			}

			throw new ArgumentException($"'{ast.Extent.Text}' is not a valid .NET or custom-defined type.", e);
		}
	}

	/// <summary>
	/// Resolves a type name, written with or without brackets, to a .NET or custom-defined type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method parses <paramref name="typeName"/> as a script, without running it, and resolves the first type literal
	/// in it, such as <c>[string]</c>. A type name without brackets, such as <c>string</c> or <c>System.String, mscorlib</c>,
	/// parses as a command name or fails to parse, so it contains no type literal. The method then resolves it as if it
	/// were written in brackets.
	/// </para>
	/// <para>
	/// When the calling module is PSReadLine, a name that doesn't resolve gives <see cref="object"/> instead of an
	/// exception.
	/// </para>
	/// </remarks>
	/// <param name="typeName">
	/// The type name to resolve, such as <c>string</c>, <c>[string]</c>, or an assembly-qualified name. This value must not be
	/// <see langword="null"/>.
	/// </param>
	/// <param name="runningModule">
	/// The calling module, or <see langword="null"/> when the call doesn't come from a module. When it is PSReadLine, the method
	/// returns <see cref="object"/> instead of throwing.
	/// </param>
	/// <returns>
	/// The type that <paramref name="typeName"/> names, or <see cref="object"/> when it doesn't name a type and
	/// <paramref name="runningModule"/> is PSReadLine.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="typeName"/> doesn't name a .NET or custom-defined type, including when it is empty, and <paramref name="runningModule"/> isn't PSReadLine.</exception>
	private static Type ResolveFromName(string typeName, PSModuleInfo? runningModule)
	{
		Ast ast;
		Type? type;

		try
		{
			ast = Parser.ParseInput(typeName, out Token[] tokens, out ParseError[] errors);

			if (errors is not null && errors.Length > 0)
			{
				// A type name that contains a comma, such as 'System.String, mscorlib', fails to parse as a script.
				if (TryResolveFromBareName(typeName, out type))
				{
					return type;
				}

				return PSREADLINE.Equals(runningModule?.Name, StringComparison.OrdinalIgnoreCase)
					? typeof(object)
					: throw new ArgumentException($"'{typeName}' is not a valid .NET or custom-defined type.");
			}
		}
		catch (ParseException e)
		{
			throw new ArgumentException($"'{typeName}' is not a valid .NET or custom-defined type.", e);
		}

		// Any other type name without brackets, such as 'string', parses as a command name.
		if (ast.Find(x => x is TypeExpressionAst, false) is null && TryResolveFromBareName(typeName, out type))
		{
			return type;
		}

		return ResolveFromAst(ast, runningModule);
	}

	/// <summary>
	/// Attempts to resolve a type name that is written without the brackets of a type literal, such as <c>string</c> or
	/// <c>System.Collections.Generic.List[int]</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method wraps <paramref name="typeName"/> in brackets and parses the result, but never runs it. The name
	/// resolves only when the result is a single type literal with nothing before or after it, and it resolves the way
	/// that type literal does, so <c>string</c> and <c>[string]</c> always resolve to the same type.
	/// </para>
	/// <para>
	/// The method doesn't use <see cref="LanguagePrimitives.ConvertTo(object, Type)"/>. In Windows PowerShell 5.1,
	/// that conversion ignores any text that follows a type name, so it converts <c>System.String bad text</c> to
	/// <see cref="string"/>.
	/// </para>
	/// </remarks>
	/// <param name="typeName">The type name to resolve.</param>
	/// <param name="type">When the method returns <see langword="true"/>, the resolved type; otherwise, <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if <paramref name="typeName"/> is a type name that resolves to a type; otherwise, <see langword="false"/>.</returns>
	private static bool TryResolveFromBareName(string typeName, [NotNullWhen(true)] out Type? type)
	{
		string literal = $"[{typeName}]";
		Ast ast = Parser.ParseInput(literal, out _, out ParseError[] errors);

		type = errors.Length == 0
			&& ast.Find(x => x is TypeExpressionAst, false) is TypeExpressionAst expression
			&& expression.Extent.Text.Length == literal.Length
				? expression.TypeName.GetReflectionType()
				: null;

		return type is not null;
	}
}
