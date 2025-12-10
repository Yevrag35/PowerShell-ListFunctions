using ListFunctions.Internal;
using ListFunctions.Modern.Exceptions;
using ListFunctions.Modern.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Runtime.CompilerServices;

namespace ListFunctions.Modern;

public sealed class HashBlock : ComparingBase
{
    private readonly PSThisVariable _thisVar;
    private readonly List<PSVariable> _varList;

    public HashBlock(ScriptBlock scriptBlock) : base(scriptBlock, preValidated: false)
    {
        _thisVar = new();
        _varList = new(4);
    }
    public HashBlock(ScriptBlock scriptBlock, List<PSVariable>? variables) : base(scriptBlock, preValidated: false)
    {
        _varList = variables ?? new(4);
        _thisVar = new();
    }

    public int GetHashCode([DisallowNull] object obj, IEnumerable<PSVariable>? additionalVariables)
    {
        if (obj is null)
        {
            var argNull = new ArgumentNullException(nameof(obj));
            throw HashCodeScriptException.FromBlockException(argNull, obj);
        }

        object? hashObj = additionalVariables is null || (additionalVariables.TryGetNonEnumeratedCount(out int varCount) && varCount == 0)
            ? this.GetHashObjectAsIs(obj, this.Script)
            : this.GetHashCodeWithContext(obj, this.Script, additionalVariables);

        return obj?.GetHashCode() ?? (int)this.ThrowNullHashCode(obj, additionalVariables);
    }

    private object GetHashObjectAsIs(object obj, ScriptBlock block)
    {
        object? scriptRetValue = block.InvokeReturnAsIs(obj);
        if (scriptRetValue is null)
        {
            return this.ThrowNullHashCode(obj, additionalVariables: null);
        }
        else if (LanguagePrimitives.GetEnumerable(obj) is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item is not null)
                {
                    return item;
                }
            }

            this.ThrowNullHashCode(obj, additionalVariables: null);
        }

        return obj;
    }
    private object? GetHashCodeWithContext(object obj, ScriptBlock block, IEnumerable<PSVariable> additionalVariables)
    {
        var list = this.SetContextVariables(obj, additionalVariables);
        if (!block.TryInvokeWithContext(list, out object? hashObj, out Exception? exception))
        {
            if (exception is null && hashObj is null)
            {
                return this.ThrowNullHashCode(obj, additionalVariables);
            }

            throw HashCodeScriptException.FromBlockException(exception, obj, this.SetContextVariables(obj, additionalVariables));
        }

        return hashObj;
    }

    private List<PSVariable> SetContextVariables(object? obj, IEnumerable<PSVariable>? additionalVariables)
    {
        _varList.Clear();
        _thisVar.SetValue(obj);
        _thisVar.InsertIntoList(_varList);

        if (additionalVariables is not null)
        {
            _varList.AddRange(additionalVariables);
        }

        return _varList;
    }

    [DoesNotReturn]
    private object? ThrowNullHashCode(object? obj, IEnumerable<PSVariable>? additionalVariables)
    {
        var baseEx = new ArgumentOutOfRangeException(nameof(obj), "The hash code script block returned a null value when an non-null one was expected.");

        string? objStr = obj?.ToString();
        var rec = new ErrorRecord(baseEx, "NullObjHashCode", ErrorCategory.InvalidResult, obj);
        rec.CategoryInfo.Activity = "GetHashCode(T obj, IEnumerable<PSVariable> additionalVariables)";
        rec.CategoryInfo.Reason = "A hash code script block should never return a 'null' value.";
        rec.CategoryInfo.TargetName = objStr ?? string.Empty;

        var inner = new RuntimeException($"Unable to retrieve an integer hash code from object, \"{objStr}\", using the supplied script block.", baseEx, rec);

        throw HashCodeScriptException.FromBlockException(inner, in obj,
            this.SetContextVariables(obj, additionalVariables));
    }
}
