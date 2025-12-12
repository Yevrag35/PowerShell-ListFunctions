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

/// <summary>
/// Represents a script-based hash code provider that computes hash codes for objects using a PowerShell script block
/// and optional variable context.
/// </summary>
/// <remarks>Use this class to customize hash code generation for objects by supplying a PowerShell script block
/// that defines the hash logic. The script block can access the target object and additional variables, enabling
/// advanced or domain-specific hash code strategies.</remarks>
public sealed class HashBlock : ComparingBase, IHashBlock
{
    private readonly PSThisVariable _thisVar;
    private readonly List<PSVariable> _varList;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashBlock"/> class using the specified script block.
    /// </summary>
    /// <param name="scriptBlock">The ScriptBlock to associate with this HashBlock. Cannot be null.</param>
    public HashBlock(ScriptBlock scriptBlock) : base(scriptBlock, preValidated: false)
    {
        _thisVar = new();
        _varList = new(4);
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="HashBlock"/> class with the specified script block and optional variable list.
    /// </summary>
    /// <param name="scriptBlock">The script block to be executed by this HashBlock. Cannot be null.</param>
    /// <param name="variables">An optional list of variables to be used within the script block. If null, an empty list is used.</param>
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

    /// <summary>
    /// Returns the result of applying the specified script block to the provided object, or the object itself if no
    /// result is produced.
    /// </summary>
    /// <remarks>If the input object is enumerable and the script block returns null, the method attempts to
    /// return the first non-null item from the enumeration. If no such item exists, an exception may be
    /// thrown.</remarks>
    /// <param name="obj">The object to which the script block is applied.</param>
    /// <param name="block">The script block to invoke with the specified object as input.</param>
    /// <returns>The value returned by the script block if it produces a non-null result; otherwise, the original object.</returns>
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
    /// <summary>
    /// Invokes the specified script block with the provided object and additional variables to compute a hash code in
    /// the given context.
    /// </summary>
    /// <param name="obj">The object to be used as context when invoking the script block.</param>
    /// <param name="block">The script block to execute for computing the hash code. Must not be null.</param>
    /// <param name="additionalVariables">A collection of additional variables to include in the script block's execution context. Can be empty.</param>
    /// <returns>The result of the script block execution, representing the computed hash code for the given context.</returns>
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

    /// <summary>
    /// Prepares and returns a list of context variables for use in PowerShell script execution.
    /// </summary>
    /// <param name="obj">The object to assign as the value of the special context variable. May be null.</param>
    /// <param name="additionalVariables">An optional collection of additional PowerShell variables to include in the context. If null, no additional
    /// variables are added.</param>
    /// <returns>A list of PowerShell variables representing the current script context, including the special context variable
    /// and any additional variables provided.</returns>
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
