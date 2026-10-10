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
/// PowerShell splits an argument that isn't in quotes or parentheses at each comma, so a type literal such as
/// <c>[System.Collections.Generic.KeyValuePair[string, int]]</c> arrives as an array of strings. The attribute joins the
/// strings back together with commas, and it accepts the result only when it's a single type name.
/// </para>
/// <para>
/// A <see langword="null"/> argument converts to <see cref="object"/>. Any other argument that doesn't resolve to a type
/// causes an <see cref="ArgumentException"/>, except when the calling module is PSReadLine, in which case the attribute
/// returns <see cref="object"/>.
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
	/// A <see cref="Type"/> is returned unchanged, and <see langword="null"/> converts to <see cref="object"/>. For a
	/// <see cref="ScriptBlock"/>, the method resolves the first type literal in its syntax tree, outside any nested script
	/// block. A <see cref="string"/> resolves as described in <see cref="ResolveFromName(string, PSModuleInfo)"/>. An
	/// array whose elements are all strings resolves as described in
	/// <see cref="ResolveFromJoinedName(string, PSModuleInfo)"/>, after the method joins the strings with commas. Any
	/// other argument doesn't resolve.
	/// </para>
	/// <para>
	/// The calling module, taken from <paramref name="engineIntrinsics"/>, matters only when the argument doesn't resolve:
	/// when that module is PSReadLine, the method returns <see cref="object"/> instead of throwing.
	/// </para>
	/// </remarks>
	/// <param name="engineIntrinsics">The engine intrinsics of the session that binds the parameter. This value must not be <see langword="null"/>.</param>
	/// <param name="inputData">The argument to convert. This value can be <see langword="null"/>, and it can be wrapped in a <see cref="PSObject"/>.</param>
	/// <returns>
	/// The <see cref="Type"/> that <paramref name="inputData"/> names, or <see cref="object"/> when
	/// <paramref name="inputData"/> is <see langword="null"/>, or when it doesn't name a type and the calling module is
	/// PSReadLine.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="inputData"/> doesn't name a type, and the calling module isn't PSReadLine.</exception>
	public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData)
	{
		object? target = inputData.GetBaseObject();

		switch (target)
		{
			case null:
				return typeof(object);

			case Type type:
				return type;

			case ScriptBlock block:
				return ResolveFromAst(block.Ast, engineIntrinsics.SessionState.Module);

			case string typeName:
				return ResolveFromName(typeName, engineIntrinsics.SessionState.Module);

			case object[] parts when TryJoinParts(parts, out string? joinedName):
				return ResolveFromJoinedName(joinedName, engineIntrinsics.SessionState.Module);

			default:
				return Reject(
					$"Cannot convert a value of type '{target.GetType().GetTypeName()}' to a type. Pass a type, a type name, or a script block that contains a type literal.",
					engineIntrinsics.SessionState.Module);
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
			return Reject($"'{ast.Extent.Text}' is not a valid .NET or custom-defined type.", runningModule, e);
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

				return Reject($"'{typeName}' is not a valid .NET or custom-defined type.", runningModule);
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
	/// Resolves a type name that was joined from the parts of an argument that PowerShell split at its commas.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The name must be a single type literal, such as <c>[System.Collections.Generic.KeyValuePair[string,int]]</c>, or a
	/// single type name without brackets, such as <c>System.Collections.Generic.KeyValuePair[string,int]</c>.
	/// </para>
	/// <para>
	/// Unlike <see cref="ResolveFromName(string, PSModuleInfo)"/>, the method doesn't settle for the first of several
	/// type literals. PowerShell splits a list of types, such as <c>[string],[int]</c>, into an array of strings too, and
	/// that list doesn't resolve to <see cref="string"/>.
	/// </para>
	/// </remarks>
	/// <param name="typeName">The joined type name. This value must not be <see langword="null"/>.</param>
	/// <param name="runningModule">
	/// The calling module, or <see langword="null"/> when the call doesn't come from a module. When it is PSReadLine, the method
	/// returns <see cref="object"/> instead of throwing.
	/// </param>
	/// <returns>
	/// The type that <paramref name="typeName"/> names, or <see cref="object"/> when it doesn't name a single type and
	/// <paramref name="runningModule"/> is PSReadLine.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="typeName"/> doesn't name a single .NET or custom-defined type, and <paramref name="runningModule"/> isn't PSReadLine.</exception>
	private static Type ResolveFromJoinedName(string typeName, PSModuleInfo? runningModule)
	{
		return TryResolveTypeLiteral(typeName, out Type? type) || TryResolveFromBareName(typeName, out type)
			? type
			: Reject($"'{typeName}' is not a valid .NET or custom-defined type.", runningModule);
	}

	/// <summary>
	/// Attempts to join the strings in the specified array into the type name that PowerShell split at its commas.
	/// </summary>
	/// <remarks>
	/// PowerShell splits an argument that isn't in quotes or parentheses at each comma, and it drops the white space after
	/// each comma. For example, <c>[System.Collections.Generic.KeyValuePair[string, int]]</c> arrives as the strings
	/// <c>[System.Collections.Generic.KeyValuePair[string</c> and <c>int]]</c>. The method unwraps each element from its
	/// <see cref="PSObject"/> and joins the strings with commas.
	/// </remarks>
	/// <param name="parts">The array to join. This value must not be <see langword="null"/>.</param>
	/// <param name="typeName">When the method returns <see langword="true"/>, the joined type name; otherwise, <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if <paramref name="parts"/> has at least one element and every element is a string; otherwise, <see langword="false"/>.</returns>
	private static bool TryJoinParts(object?[] parts, [NotNullWhen(true)] out string? typeName)
	{
		typeName = null;
		if (parts.Length == 0)
		{
			return false;
		}

		string[] names = new string[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			if (parts[i].GetBaseObject() is not string name)
			{
				return false;
			}

			names[i] = name;
		}

		typeName = string.Join(",", names);
		return true;
	}

	/// <summary>
	/// Attempts to resolve a type name that is written without the brackets of a type literal, such as <c>string</c> or
	/// <c>System.Collections.Generic.List[int]</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method wraps <paramref name="typeName"/> in brackets and resolves the result with
	/// <see cref="TryResolveTypeLiteral(string, out Type)"/>, so <c>string</c> and <c>[string]</c> always resolve to the
	/// same type.
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
		return TryResolveTypeLiteral($"[{typeName}]", out type);
	}

	/// <summary>
	/// Attempts to resolve text that is a single type literal, such as <c>[string]</c>, with nothing before or after it.
	/// </summary>
	/// <remarks>
	/// The method parses <paramref name="literal"/> but never runs it. The text resolves only when it parses without
	/// errors and its first type literal spans all of it, and it resolves the way that type literal does.
	/// </remarks>
	/// <param name="literal">The text to resolve.</param>
	/// <param name="type">When the method returns <see langword="true"/>, the resolved type; otherwise, <see langword="null"/>.</param>
	/// <returns><see langword="true"/> if <paramref name="literal"/> is a single type literal that resolves to a type; otherwise, <see langword="false"/>.</returns>
	private static bool TryResolveTypeLiteral(string literal, [NotNullWhen(true)] out Type? type)
	{
		Ast ast = Parser.ParseInput(literal, out _, out ParseError[] errors);

		type = errors.Length == 0
			&& ast.Find(x => x is TypeExpressionAst, false) is TypeExpressionAst expression
			&& expression.Extent.Text.Length == literal.Length
				? expression.TypeName.GetReflectionType()
				: null;

		return type is not null;
	}

	/// <summary>
	/// Returns <see cref="object"/> when the calling module is PSReadLine; otherwise, throws an
	/// <see cref="ArgumentException"/> for an argument that doesn't name a type.
	/// </summary>
	/// <param name="message">The message of the exception.</param>
	/// <param name="runningModule">The calling module, or <see langword="null"/> when the call doesn't come from a module.</param>
	/// <param name="innerException">The exception that caused the failure, or <see langword="null"/>.</param>
	/// <returns><see cref="object"/>, when <paramref name="runningModule"/> is PSReadLine.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="runningModule"/> isn't PSReadLine.</exception>
	private static Type Reject(string message, PSModuleInfo? runningModule, Exception? innerException = null)
	{
		return PSREADLINE.Equals(runningModule?.Name, StringComparison.OrdinalIgnoreCase)
			? typeof(object)
			: throw new ArgumentException(message, innerException);
	}
}
