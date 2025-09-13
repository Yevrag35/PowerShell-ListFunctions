using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using ZLinq;

#nullable enable

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
        public const string Args = "args";

        private HashSet<int>? _mustContainIndexes;
        private HashSet<string>? _mustContainNames;

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

            HashSet<int>? indexes = null;
            HashSet<string>? names = null;

            foreach (string name in variableNames)
            {
                if (name.StartsWith(Args, StringComparison.OrdinalIgnoreCase) && TryParseIndexFromName(name, out int index))
                {
                    indexes ??= new HashSet<int>();
                    _ = indexes.Add(index);
                }
                else
                {
                    names ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _ = names.Add(name);
                }
            }

            _mustContainIndexes = indexes;
            _mustContainNames = names;
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

            if (!IsAllValid(block, _mustContainNames, _mustContainIndexes))
            {
                throw new ValidationMetadataException($"At least one of the following variables must be included in the script block: ${string.Join(", $", (IEnumerable<string>?)_mustContainNames ?? Array.Empty<string>())}");
            }
            //EnumerateAllVariablesInBlock(block);

            //if (DoesNotContainAny(ref allVars, this.MustContainAny))
            //{
            //}
        }

//        private static bool DoesNotContainAny(ref 
//#if NET5_0_OR_GREATER
//            ValueEnumerable<ZLinq.Linq.Select<ZLinq.Linq.Cast<ZLinq.Linq.FromEnumerable<Ast>, Ast, VariableExpressionAst>, VariableExpressionAst, string>, string>
//#else
//            IEnumerable<string>
//#endif
//               allVars, string[] mustContainAny)
//        {
//            foreach (string foundVarName in allVars)
//            {
//                if (ValidArgsIndex.Args.Equals(foundVarName, StringComparison.OrdinalIgnoreCase))
//                {
//                    return false;
//                }

//                foreach (string mustVarName in mustContainAny)
//                {
//                    if (mustVarName.Equals(foundVarName, StringComparison.OrdinalIgnoreCase))
//                    {
//                        return false;
//                    }
//                }
//            }

//            return true;
//        }

        private static bool IsAllValid(ScriptBlock block, HashSet<string>? mustContainNames, HashSet<int>? mustContainIndexes)
        {
            if (mustContainIndexes is null && mustContainNames is null)
            {
                throw new ArgumentException("At least one of the parameters must be non-null.", nameof(mustContainNames));
            }

            bool allowsArgs = !(mustContainIndexes is null || mustContainIndexes.Count == 0);

            IEnumerable<Ast> asts = allowsArgs
                ? block.Ast.FindAll(searchNestedScriptBlocks: false, predicate: x => IsVariableAst(x) || IsArgsIndexed(x))
                : block.Ast.FindAll(searchNestedScriptBlocks: false, predicate: IsVariableAst);

            return allowsArgs
                ? IsValidWithIndexes(asts, mustContainNames, mustContainIndexes!)
                : IsValidNoIndexes(asts, mustContainNames!);
        }

        private static bool IsVariableAst(Ast ast)
        {
            return ast is VariableExpressionAst varAst
                && !varAst.IsConstantVariable()
                && !varAst.Splatted
                && varAst.VariablePath.IsVariable
                && !Args.Equals(varAst.VariablePath.UserPath, StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsArgsIndexed(Ast ast)
        {
            return ast is IndexExpressionAst indexAst
                && indexAst.Target is VariableExpressionAst varAst
                && varAst.VariablePath.IsVariable
                && Args.Equals(varAst.VariablePath.UserPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidWithIndexes(IEnumerable<Ast> asts, HashSet<string>? anyNames, HashSet<int> orAnyIndexes)
        {
            bool isNotNull = !(anyNames is null || anyNames.Count == 0);
            foreach (Ast ast in asts.AsValueEnumerable())
            {
                if (ast is VariableExpressionAst varAst && isNotNull && anyNames!.Contains(varAst.VariablePath.UserPath))
                {
                    return true;
                }
                else if (ast is IndexExpressionAst indexAst && indexAst.Index is ConstantExpressionAst constAst && constAst.Value is int index
                    &&
                    orAnyIndexes.Contains(index))
                {
                    return true;
                }
            }

            return false;
        }
        private static bool IsValidNoIndexes(IEnumerable<Ast> asts, HashSet<string> anyNames)
        {
            foreach (VariableExpressionAst varAst in asts.AsValueEnumerable())
            {
                if (anyNames.Contains(varAst.VariablePath.UserPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseIndexFromName(string name, out int parsedIndex)
        {
#if NETCOREAPP
            ReadOnlySpan<char> span = name.AsSpan(Args.Length);
            if (span[0] == '[' && span[^1] == ']')
            {
                span = span.Slice(1, span.Length - 2);
            }

            if (int.TryParse(span, out int index) && index >= 0)
            {
                parsedIndex = index;
                return true;
            }
#else
            string trimmed = name[Args.Length] == '[' && name[name.Length - 1] == ']'
                ? name.Substring(Args.Length + 1, name.Length - Args.Length - 2)
                : name.Substring(Args.Length);

            if (int.TryParse(trimmed, out int index) && index >= 0)
            {
                parsedIndex = index;
                return true;
            }
#endif

            parsedIndex = default;
            return false;
        }
    }
}
