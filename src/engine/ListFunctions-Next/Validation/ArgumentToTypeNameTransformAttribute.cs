using ListFunctions.Extensions;

#nullable enable

namespace ListFunctions.Validation;

/// <summary>
/// Provides a mechanism to transform an input argument into a .NET <see cref="Type"/> object.
/// </summary>
/// <remarks>This attribute is used to convert various input formats, such as <see cref="Type"/>,  <see
/// cref="System.Management.Automation.ScriptBlock"/>, or <see cref="string"/>, into a  corresponding <see
/// cref="Type"/> instance. If the input cannot be resolved to a valid type,  the transformation defaults to <see
/// cref="object"/>.</remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class ArgumentToTypeTransformAttribute : ArgumentTransformationAttribute
{
	const string PSREADLINE = "PSReadLine";

	/// <summary>
	/// Resolves a type reference from the specified input data, which may be a Type, ScriptBlock, or type name
	/// string.
	/// </summary>
	/// <remarks>If the input is a ScriptBlock or a string, the method attempts to resolve the type
	/// using the current module's context. If the input cannot be resolved to a type, the method returns
	/// typeof(object).</remarks>
	/// <param name="engineIntrinsics">The engine intrinsics context used to access session state and module information during resolution.</param>
	/// <param name="inputData">The input object to resolve. This can be a Type, a ScriptBlock containing type information, or a string
	/// representing the type name. May be null.</param>
	/// <returns>A Type object representing the resolved type if the input can be resolved; otherwise, the System.Object
	/// type.</returns>
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

	static readonly string _name = $"[{nameof(ArgumentToTypeTransformAttribute).Replace("Attribute", "")}]";
	public override string ToString()
	{
		return _name;
	}

	/// <summary>
	/// Resolves the .NET type represented by the specified abstract syntax tree (AST) node.
	/// </summary>
	/// <remarks>If the running module is identified as 'PSReadLine', the method returns <see
	/// cref="object"/> instead of throwing an exception when the AST does not represent a valid type. This behavior
	/// is intended to provide compatibility with PSReadLine's parsing requirements.</remarks>
	/// <param name="ast">The AST node to analyze for a type expression. Must represent a valid type expression.</param>
	/// <param name="runningModule">The module context in which the resolution occurs, or null if not applicable. Used to determine special
	/// handling for certain modules.</param>
	/// <returns>The resolved .NET type corresponding to the type expression found in the AST.</returns>
	/// <exception cref="ArgumentException">Thrown when the AST does not represent a valid .NET or custom-defined type and the running module is not
	/// handled specially.</exception>
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
	/// Resolves a .NET or custom-defined type from its name, optionally considering the context of a running
	/// PowerShell module.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method parses <paramref name="typeName"/> as a script and resolves the first type expression in it, such as
	/// <c>[string]</c>. A type name without brackets, such as <c>string</c> or <c>System.String, mscorlib</c>, parses as
	/// a command name or fails to parse, so it contains no type expression. The method then resolves it as if it were
	/// written in brackets.
	/// </para>
	/// <para>
	/// If the running module is 'PSReadLine', invalid type names are resolved to <see cref="object"/> instead of
	/// throwing an exception.
	/// </para>
	/// </remarks>
	/// <param name="typeName">The name of the type to resolve. This can be a fully qualified .NET type name or a custom-defined type name.
	/// Cannot be null or empty.</param>
	/// <param name="runningModule">The PowerShell module context to use when resolving custom-defined types, or null to resolve types without
	/// module context.</param>
	/// <returns>The resolved <see cref="Type"/> corresponding to the specified name. Returns <see cref="object"/> if the
	/// type name is invalid and the running module is 'PSReadLine'.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="typeName"/> is not a valid .NET or custom-defined type and the running module is
	/// not 'PSReadLine'.</exception>
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
