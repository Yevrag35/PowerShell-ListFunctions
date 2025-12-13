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

        /// <summary>
        /// Initializes a new instance of the ValidateScriptVariableAttribute class with the specified variable names
        /// and/or index values to validate.
        /// </summary>
        /// <remarks>Variable names and index values can be mixed in the array. Index values are
        /// interpreted as strings that can be parsed to integers; all other values are treated as variable names. The
        /// order of elements determines how they are categorized.</remarks>
        /// <param name="variableNames">An array of variable names and/or index values that must be present for validation. Index values should be
        /// provided as string representations of integers. Cannot be null.</param>
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

        /// <inheritdoc/>
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
                throw new ValidationMetadataException(string.Concat(message, "$", vars));
            }
        }

        /// <summary>
        /// Parses variable names to extract integer indexes from those that match the expected pattern.
        /// </summary>
        /// <remarks>The method partitions the input array in place, moving variable names with parsable
        /// indexes to the end of the array. Only variable names that match the expected pattern are included in the
        /// output array. The order of parsed indexes in the output array corresponds to their original positions in the
        /// input array.</remarks>
        /// <param name="variableNames">An array of variable names to examine for parsable integer indexes. Must contain at least one element.</param>
        /// <param name="indexes">When this method returns, contains an array of integer indexes parsed from the variable names that match the
        /// expected pattern, or null if no variable names could be parsed. This parameter is passed uninitialized.</param>
        /// <returns>The number of variable names from which an integer index was successfully parsed.</returns>
        /// <exception cref="ArgumentException">Thrown if variableNames is null or contains no elements.</exception>
        private static int ParseIndexes(string[]? variableNames, out int[]? indexes)
        {
            Guard.NotNull(variableNames);
            if (variableNames.Length == 0)
            {
                throw new ArgumentException("Must contain at least 1 variable name.", nameof(variableNames));
            }

            int left = 0;
            int right = variableNames.Length - 1;

            indexes = null;
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
                    int arrayLength = variableNames.Length - left;
                    Debug.Assert(arrayLength > 0, "The array length should be greater than zero here.");
                    indexes ??= new int[arrayLength];

                    indexes[parsedCount++] = value;

                    // Swap the current element with the element at 'right'
                    (variableNames[right], variableNames[left]) = (variableNames[left], variableNames[right]);

                    right--;
                    // Do NOT increment 'left' here – the swapped-in element at buffer[left]
                    // still needs to be examined.
                }
            }

            return parsedCount;
        }

        /// <summary>
        /// Determines whether the specified script block contains all required variable names and, if specified, all
        /// required argument indexes.
        /// </summary>
        /// <param name="block">The script block to validate for required variables and argument indexes.</param>
        /// <param name="mustContainNames">A collection of variable names that must be present in the script block. Cannot be null.</param>
        /// <param name="mustContainIndexes">A collection of argument indexes that must be present in the script block. If empty, only variable names are
        /// validated.</param>
        /// <returns>true if the script block contains all required variable names and, if specified, all required argument
        /// indexes; otherwise, false.</returns>
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

        /// <summary>
        /// Determines whether the specified abstract syntax tree (AST) collection contains a variable or index that
        /// matches any of the provided names or indexes.
        /// </summary>
        /// <param name="asts">The collection of AST nodes to search for matching variables or indexes.</param>
        /// <param name="anyNames">A slice of variable names to match against variable expressions in the ASTs. Matching is case-insensitive.</param>
        /// <param name="orAnyIndexes">A slice of integer indexes to match against constant index expressions in the ASTs.</param>
        /// <returns>true if any AST node is a variable expression with a name in anyNames, or an index expression with a
        /// constant value in orAnyIndexes; otherwise, false.</returns>
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
        /// <summary>
        /// Determines whether any variable in the specified abstract syntax trees matches a name in the provided
        /// collection, using a case-insensitive comparison.
        /// </summary>
        /// <param name="asts">A collection of abstract syntax tree (AST) nodes to search for variable expressions.</param>
        /// <param name="anyNames">A collection of variable names to match against, compared using case-insensitive ordinal comparison.</param>
        /// <returns>true if at least one variable expression in the ASTs matches a name in the collection; otherwise, false.</returns>
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

        /// <summary>
        /// Attempts to extract a zero-based index from the specified argument name string.
        /// </summary>
        /// <remarks>The method expects the argument name to start with the standard argument prefix (such
        /// as "Args"). If the name includes an index, it may be enclosed in square brackets. Parsing fails if the index
        /// is missing, negative, or not a valid integer.</remarks>
        /// <param name="name">The argument name to parse. The name is expected to begin with the standard argument prefix, optionally
        /// followed by an index in square brackets (e.g., "Args[2]").</param>
        /// <param name="parsedIndex">When this method returns, contains the parsed zero-based index if parsing succeeds; otherwise, contains -1.
        /// This parameter is passed uninitialized.</param>
        /// <returns>true if a valid non-negative index is successfully parsed from the argument name; otherwise, false.</returns>
        private static bool TryParseIndexFromName(string name, out int parsedIndex)
        {
            if (name.Length < Args.Length + 3 || !name.StartsWith(Args, StringComparison.OrdinalIgnoreCase))
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
