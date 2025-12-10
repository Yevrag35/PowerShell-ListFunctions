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
            Guard.NotNull(scriptBlock);

            if (scriptBlock.Ast is not ScriptBlockAst scriptAst)
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
            return statements.Count != 0;
        }

        [return: NotNullIfNotNull(nameof(defaultIfNull))]
        internal static T InvokeWithContext<T>(this ScriptBlock scriptBlock, List<PSVariable> variables, Func<object, T> selectAs, T defaultIfNull = default!) where T : notnull
        {
            Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, _emptyObjs);
            return results.GetFirstValue(selectAs, defaultIfNull);
        }

        internal static bool TryInvokeWithContext(this ScriptBlock scriptBlock, List<PSVariable> variables, [NotNullIfNotNull(nameof(defaultIfNull))] out object? result, out Exception? caughtError, object? defaultIfNull = null)
        {
            try
            {
                Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, _emptyObjs);
                caughtError = null;
                if (results.Count > 0 && results[0] is PSObject o)
                {
                    result = PSObject.AsPSObject(o.BaseObject).ImmediateBaseObject;
                    return result is not null;
                }

                result = defaultIfNull;
                return result is not null;

            }
            catch (Exception e)
            {
                caughtError = e;
                result = defaultIfNull;
                return false;
            }
        }
        internal static bool TryInvokeWithContext<T>(this ScriptBlock scriptBlock, List<PSVariable> variables, Func<object, T> selectAs, [NotNullIfNotNull(nameof(defaultIfNull))] out T result, out Exception? caughtError, T defaultIfNull = default!)
        {
            try
            {
                Collection<PSObject> results = scriptBlock.InvokeWithContext(null, variables, _emptyObjs);
                result = results.GetFirstValue(selectAs, defaultIfNull);

                caughtError = null;
                return result is not null;
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
