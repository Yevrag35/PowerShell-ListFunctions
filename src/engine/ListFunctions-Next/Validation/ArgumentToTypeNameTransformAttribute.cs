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
    internal sealed class ArgumentToTypeTransformAttribute : ArgumentTransformationAttribute
    {
        const string PSREADLINE = "PSReadLine";

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

        private static Type ResolveFromAst(Ast ast, PSModuleInfo? runningModule)
        {
            try
            {
                var first = (TypeExpressionAst?)ast.Find(x => x is TypeExpressionAst, false);

                return first?.TypeName.GetReflectionType() ?? throw new ParseException($"{ast.Extent.Text} is not a type expression.");
            }
            catch (ParseException e)
            {
                if (!(runningModule is null) && PSREADLINE.Equals(runningModule.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(object);
                }

                throw new ArgumentException($"'{ast.Extent.Text}' is not a valid .NET or custom-defined type.", e);
            }
        }
        private static Type ResolveFromName(string typeName, PSModuleInfo? runningModule)
        {
            Ast ast;

            try
            {
                ast = Parser.ParseInput(typeName, out Token[] tokens, out ParseError[] errors);

                if (!(errors is null) && errors.Length > 0)
                {
                    throw new ParseException(errors);
                }
            }
            catch (ParseException e)
            {
                if (!(runningModule is null) && PSREADLINE.Equals(runningModule.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(object);
                }

                throw new ArgumentException($"'{typeName}' is not a valid .NET or custom-defined type.", e);
            }

            return ResolveFromAst(ast, runningModule);
        }
    }
}
