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
using System.Runtime.CompilerServices;

#nullable enable

namespace ListFunctions.Cmdlets.Finds
{
    [Cmdlet(VerbsCommon.Find, "IndexOf")]
    [Alias("Find-Index", "IndexOf")]
    [OutputType(typeof(int))]
    public sealed class FindIndexCmdlet : ListFunctionCmdletBase
    {
        private ScriptBlockFilter _filter = null!;
        private int _currentIndex;

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.Underscore, PSThisVariable.This, PSThisVariable.PSItem, PSThisVariable.FirstArg)]
        public ScriptBlock Condition { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [Alias("List")]
        [AllowEmptyCollection, AllowEmptyString, AllowNull]
        public object?[]? InputObject { get; set; }

        [Parameter, Alias("ScriptErrorAction")]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override void BeginCore()
        {
            _filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
        }
        protected override bool ProcessCore()
        {   
            if (this.InputObject is null || this.InputObject.Length == 0)
                return true;    // keep going

            for (int i = 0; i < this.InputObject.Length; i++)
            {
                if (_filter.IsTrue(this.InputObject[i]))
                {
                    _currentIndex += i;
                    return false;   // stop processing
                }
            }

            _currentIndex += this.InputObject.Length;
            return true;
        }

        protected override void EndCore(CmdletRunState state)
        {
            int index = state.FoundMatch ? _currentIndex : -1;

            this.WriteObject(index);
        }

        protected override void Cleanup()
        {
            if (_filter is not null)
            {
                _filter.Dispose();
                _filter = null!;
            }
        }
    }
}

