using System;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Language;

namespace ListFunctions.Validation
{
    internal sealed class ArgumentToTypeTransformAttribute : ArgumentTransformationAttribute
    {
        const string PSREADLINE = "PSReadLine";

        public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData)
        {
            object? target = GetBase(inputData);

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


        internal static object? GetBase(object? obj)
        {
            if (!(obj is PSObject mshObj))
            {
                return obj;
            }

            if (mshObj == AutomationNull.Value)
            {
                return null;
            }

            return PSObject.AsPSObject(mshObj.ImmediateBaseObject).ImmediateBaseObject;
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
                if (!(runningModule is null) && PSREADLINE.Equals(runningModule.Name, StringComparison.InvariantCultureIgnoreCase))
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
                if (!(runningModule is null) && PSREADLINE.Equals(runningModule.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return typeof(object);
                }

                throw new ArgumentException($"'{typeName}' is not a valid .NET or custom-defined type.", e);
            }

            return ResolveFromAst(ast, runningModule);
        }
    }
}
