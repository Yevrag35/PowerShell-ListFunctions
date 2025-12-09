using ListFunctions.Internal;
using ListFunctions.Modern.Pools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        private readonly ArraySlice<int> _mustContainIndexes;
        private readonly ArraySlice<string> _mustContainNames;

        public ValidateScriptVariableAttribute(params string[] variableNames)
        {
            int indexCount = ParseIndexes(variableNames, out int[]? indexes);
            int nameCount = variableNames.Length - indexCount;

            _mustContainIndexes = indexCount == 0
                ? ArraySlice.Empty<int>()
                : new(indexes!, 0, indexCount);

            _mustContainNames = nameCount == 0
                ? ArraySlice.Empty<string>()
                : new(variableNames, 0, nameCount);
        }

        protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
        {
            if (arguments is string str)
            {
                arguments = ScriptBlock.Create(str);
            }

            if (arguments is not ScriptBlock block)
            {
                return;
            }

            if (!IsAllValid(block, _mustContainNames, _mustContainIndexes))
            {
                const string message = "At least one of the following variables must be included in the script block: ";
                const string sep = ", $";
                string vars = string.Join(
                    separator: sep,
#if NET9_0_OR_GREATER
                    values: _mustContainNames.AsSpan()
#else
                    values: (IEnumerable<string>)_mustContainNames
#endif
                );
                throw new ValidationMetadataException(string.Concat(message, vars));
            }
        }

        private static int ParseIndexes(string[]? variableNames, out int[]? indexes)
        {
            Guard.NotNull(variableNames);
            if (variableNames.Length == 0)
            {
                throw new ArgumentException("Must contain at least 1 variable name.", nameof(variableNames));
            }

            int left = 0;
            int right = variableNames.Length - 1;

            int[]? parsed = null;
            int parsedCount = 0;

            // Invariant:
            // [0, left)     => non-parsable strings
            // (right, end]  => parsable strings (moved to the back)
            // [left, right] => unknown / not yet processed
            while (left <= right)
            {
                string current = variableNames[left];

                if (!TryParseIndexFromName(current, out int value))
                {
                    // Non-parsable: stays in the front partition.
                    left++;
                }
                else
                {
                    // Parsable: goes to the back partition, and we collect the int value.

                    // Lazy allocation with a tighter upper bound:
                    //   - left elements are *known* non-parsable,
                    //   - so at most (buffer.Length - left) elements can ever be parsable.
                    parsed ??= new int[variableNames.Length - left];

                    parsed[parsedCount++] = value;

                    // Swap the current element with the element at 'right'
                    (variableNames[right], variableNames[left]) = (variableNames[left], variableNames[right]);

                    right--;
                    // Do NOT increment 'left' here – the swapped-in element at buffer[left]
                    // still needs to be examined.
                }
            }

            indexes = parsedCount == 0 ? null : parsed;
            return parsedCount;
        }

        private static bool IsAllValid(ScriptBlock block, ArraySlice<string> mustContainNames, ArraySlice<int> mustContainIndexes)
        {
            bool allowsArgs = mustContainIndexes.Length != 0;

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

        private static bool IsValidWithIndexes(IEnumerable<Ast> asts, ArraySlice<string> anyNames, ArraySlice<int> orAnyIndexes)
        {
            bool isNotNull = anyNames.Length > 0;
            foreach (Ast ast in asts.AsValueEnumerable())
            {
                if (ast is VariableExpressionAst varAst && isNotNull && ArraySlice.Contains(anyNames, varAst.VariablePath.UserPath, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (ast is IndexExpressionAst indexAst && indexAst.Index is ConstantExpressionAst constAst && constAst.Value is int index
                    &&
                    ArraySlice.Contains(orAnyIndexes, index))
                {
                    return true;
                }
            }

            return false;
        }
        private static bool IsValidNoIndexes(IEnumerable<Ast> asts, ArraySlice<string> anyNames)
        {
            foreach (VariableExpressionAst varAst in asts.AsValueEnumerable())
            {
                if (ArraySlice.Contains(anyNames, varAst.VariablePath.UserPath, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseIndexFromName(string name, out int parsedIndex)
        {
            if (name.Length <= Args.Length)
            {
                parsedIndex = -1;
                return false;
            }

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
            
            if (!name.StartsWith(Args, StringComparison.OrdinalIgnoreCase))
            {
                parsedIndex = -1;
                return false;
            }

            string trimmed = name[Args.Length] == '[' && name[name.Length - 1] == ']'
                ? name.Substring(Args.Length + 1, name.Length - Args.Length - 2)
                : name.Substring(Args.Length);

            if (int.TryParse(trimmed, out int index) && index >= 0)
            {
                parsedIndex = index;
                return true;
            }
#endif

            parsedIndex = -1;
            return false;
        }
    }
}
