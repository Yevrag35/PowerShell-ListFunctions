using ListFunctions.Components;
using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Pools;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

#nullable enable

namespace ListFunctions.Cmdlets.Finds
{
    /// <summary>
    /// Implements a cmdlet that finds the zero-based index of the last element in a list that matches a specified
    /// condition.
    /// </summary>
    /// <remarks>This cmdlet evaluates each element in the input list against the provided script block
    /// condition, starting from the end of the list and moving backward. If no element satisfies the condition, the
    /// cmdlet returns -1. The cmdlet supports pipeline input and can be used in PowerShell scripts to efficiently
    /// locate the last matching item in a collection.</remarks>
    [Cmdlet(VerbsCommon.Find, "LastIndexOf")]
    [Alias("Find-LastIndex", "LastIndexOf")]
    [OutputType(typeof(int))]
    public sealed class FindLastIndexCmdlet : ListFunctionCmdletBase
    {
        private ScriptBlockFilter _filter = null!;
        private List<object?> _list = null!;

        [Parameter(Mandatory = true, Position = 0), Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
        public ScriptBlock Condition { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true), Alias("List")]
        [AllowEmptyCollection, AllowNull, AllowEmptyString]
        public object?[]? InputObject { get; set; }

        [Parameter]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override void BeginCore()
        {
            _filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
            _list = ListPool<object?>.Rent();
        }
        protected override bool ProcessCore()
        {
            if (this.InputObject is not null)
            {
                _list.AddRange(this.InputObject);
            }

            return true;
        }
        protected override void EndCore(CmdletRunState state)
        {
            if (state.FoundMatch)
                return;

            for (int i = _list.Count - 1; i >= 0; i--)
            {
                if (_filter.IsTrue(_list[i]))
                {
                    this.WriteObject(i);
                    return;
                }
            }

            this.WriteObject(-1);
        }

        protected override void Cleanup()
        {
            if (_filter is not null)
            {
                _filter.Dispose();
                _filter = null!;
            }

            if (_list is not null)
            {
                ListPool<object?>.Return(_list);
                _list = null!;
            }
        }
    }
}

