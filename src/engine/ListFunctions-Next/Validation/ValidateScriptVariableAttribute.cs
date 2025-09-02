using ListFunctions.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using ZLinq;

namespace ListFunctions.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public sealed class ValidateScriptVariableAttribute : ValidateArgumentsAttribute
    {
        readonly HashSet<string> _names;
        public string[] MustContainAny { get; }

        public ValidateScriptVariableAttribute(params string[] variableNames)
        {
            if (variableNames is null)
            {
                throw new ArgumentNullException(nameof(variableNames));
            }

            _names = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            if (variableNames.Length <= 0)
            {
                throw new ArgumentException("Must contain at least 1 variable name.");
            }

            this.MustContainAny = variableNames;
        }

        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            _names.Clear();
            if (!(arguments is ScriptBlock block))
            {
                return;
            }

            var allVars = EnumerateAllVariablesInBlock(block);

#if NET5_0_OR_GREATER
            _names.UnionWithRef(ref allVars);
#else
            _names.UnionWith(allVars);
#endif

            if (!_names.Overlaps(this.MustContainAny))
            {
                throw new ValidationMetadataException($"At least one of the following variables must be included in the script block: ${string.Join(", $", this.MustContainAny)}");
            }
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
