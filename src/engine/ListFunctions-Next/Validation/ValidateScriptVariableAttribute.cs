using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using ZLinq;

namespace ListFunctions.Validation
{
    /// <summary>
    /// Specifies that a script block must contain at least one of the specified variable names to be considered valid.
    /// </summary>
    /// <remarks>This attribute is used to validate that a script block contains at least one of the required
    /// variable names.  If the script block does not include any of the specified variables, a <see
    /// cref="ValidationMetadataException"/>  is thrown during validation.</remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public sealed class ValidateScriptVariableAttribute : ValidateArgumentsAttribute
    {
        public string[] MustContainAny { get; }

        public ValidateScriptVariableAttribute(params string[] variableNames)
        {
            if (variableNames is null)
            {
                throw new ArgumentNullException(nameof(variableNames));
            }

            if (variableNames.Length == 0)
            {
                throw new ArgumentException("Must contain at least 1 variable name.");
            }

            this.MustContainAny = variableNames;
        }

        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            if (arguments is string str)
            {
                arguments = ScriptBlock.Create(str);
            }

            if (!(arguments is ScriptBlock block))
            {
                return;
            }

            var allVars = EnumerateAllVariablesInBlock(block);

            if (DoesNotContainAny(ref allVars, this.MustContainAny))
            {
                throw new ValidationMetadataException($"At least one of the following variables must be included in the script block: ${string.Join(", $", this.MustContainAny)}");
            }
        }

        private static bool DoesNotContainAny(ref 
#if NET5_0_OR_GREATER
            ValueEnumerable<ZLinq.Linq.Select<ZLinq.Linq.Cast<ZLinq.Linq.FromEnumerable<Ast>, Ast, VariableExpressionAst>, VariableExpressionAst, string>, string>
#else
            IEnumerable<string>
#endif
               allVars, string[] mustContainAny)
        {
            foreach (string mustVarName in mustContainAny)
            {
                foreach (string foundVarName in allVars)
                {
                    if (mustVarName.Equals(foundVarName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static
#if NET5_0_OR_GREATER
            ValueEnumerable<ZLinq.Linq.Select<ZLinq.Linq.Cast<ZLinq.Linq.FromEnumerable<Ast>, Ast, VariableExpressionAst>, VariableExpressionAst, string>, string>
#else
            IEnumerable<string>
#endif
            EnumerateAllVariablesInBlock(ScriptBlock block)
        {
            return block.Ast.FindAll(x => 
                x is VariableExpressionAst varAst 
                && !varAst.IsConstantVariable() 
                && !varAst.Splatted
                && varAst.VariablePath.IsVariable, searchNestedScriptBlocks: true)
                    .AsValueEnumerable()
                    .Cast<VariableExpressionAst>()
                    .Select(x => x.VariablePath.UserPath);
        }
    }
}
