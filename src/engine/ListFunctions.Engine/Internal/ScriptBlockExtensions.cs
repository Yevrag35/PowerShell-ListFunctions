using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation.Language;
using System.Management.Automation;
using ListFunctions.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace ListFunctions.Internal
{
    internal static class ScriptBlockExtensions
    {
        static readonly object[] _emptyObjs = Array.Empty<object>();

        internal static bool IsProperScriptBlock(this ScriptBlock scriptBlock)
        {
            Guard.NotNull(scriptBlock, nameof(scriptBlock));

            if (!(scriptBlock.Ast is ScriptBlockAst scriptAst))
            {
                return false;
            }

            if (!(scriptAst.BeginBlock is null || scriptAst.ProcessBlock is null))
            {
                return true;
            }
            else if (scriptAst.EndBlock is null)
            {
                return false;
            }

            ReadOnlyCollection<StatementAst> statements = scriptAst.EndBlock.Statements;
            if (statements.Count <= 0)
            {
                return false;
            }
            else if (statements[0] is PipelineAst firstPipeline)
            {
                return firstPipeline.PipelineElements.Count > 0
                   &&
                   firstPipeline.PipelineElements[0] is CommandExpressionAst;
            }
            else
            {
                return false;
            }
        }

        [return: NotNullIfNotNull(nameof(defaultIfNull))]
        internal static T InvokeWithContext<T>(this ScriptBlock scriptBlock, List<PSVariable> variables, Func<object, T> selectAs, T defaultIfNull = default!) where T : notnull
        {
            Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, _emptyObjs);
            return results.GetFirstValue(selectAs, defaultIfNull);
        }


        internal static bool TryInvokeWithContext<T>(this ScriptBlock scriptBlock, List<PSVariable> variables, Func<object, T> selectAs, [NotNullIfNotNull(nameof(defaultIfNull))] out T result, [NotNullWhen(false)] out Exception? caughtError, T defaultIfNull = default!)
        {
            try
            {
                Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, _emptyObjs);
                result = results.GetFirstValue(selectAs, defaultIfNull);

                caughtError = null;
                return true;
            }
            catch (Exception e)
            {
                caughtError = e;
                result = defaultIfNull;
                return false;
            }
        }
    }
}
