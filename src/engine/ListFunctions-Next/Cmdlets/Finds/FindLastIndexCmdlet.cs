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
    [Cmdlet(VerbsCommon.Find, "LastIndexOf")]
    [Alias("Find-LastIndex", "LastIndexOf")]
    [OutputType(typeof(int))]
    public sealed class FindLastIndexCmdlet : ListFunctionCmdletBase
    {
        private ScriptBlockFilter _filter = null!;
        private List<object?> _list = null!;

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME, PSThisVariable.ARGS_FIRST)]
        public ScriptBlock Condition { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [Alias("List")]
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
            if (!(this.InputObject is null))
            {
                _list.AddRange(this.InputObject);
            }

            return true;
        }
        protected override void EndCore(bool wantsToStop)
        {
            if (wantsToStop)
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
            if (!(_filter is null))
            {
                _filter.Dispose();
                _filter = null!;
            }

            if (!(_list is null))
            {
                ListPool<object?>.Return(_list);
                _list = null!;
            }
        }
    }
}

