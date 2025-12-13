using ListFunctions.Extensions;
using System;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Language;

#nullable enable

namespace ListFunctions.Validation
{
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
        /// <remarks>If the running module is 'PSReadLine', invalid type names are resolved to <see
        /// cref="object"/> instead of throwing an exception.</remarks>
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

            try
            {
                ast = Parser.ParseInput(typeName, out Token[] tokens, out ParseError[] errors);

                if (errors is not null && errors.Length > 0)
                {
                    return PSREADLINE.Equals(runningModule?.Name, StringComparison.OrdinalIgnoreCase)
                        ? typeof(object)
                        : throw new ArgumentException($"'{typeName}' is not a valid .NET or custom-defined type.");
                }
            }
            catch (ParseException e)
            {
                throw new ArgumentException($"'{typeName}' is not a valid .NET or custom-defined type.", e);
            }

            return ResolveFromAst(ast, runningModule);
        }
    }
}
