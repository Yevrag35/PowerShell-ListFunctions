using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

#nullable enable

namespace ListFunctions.Cmdlets.Assertions
{
    [Cmdlet(VerbsLifecycle.Assert, "AllObject")]
    [Alias("Assert-AllObjects", "Assert-All", "All", "All-Object", "All-Objects")]
    [OutputType(typeof(bool))]
    public sealed class AssertAllObjectsCmdlet : AssertObjectCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock", "FilterScript")]
        [AllowNull, AllowEmptyString]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME, PSThisVariable.ARGS_FIRST)]
        public override ScriptBlock? Condition
        {
            get => base.Condition;
            set => base.Condition = value;
        }

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowNull, AllowEmptyCollection, AllowEmptyString]
        public object?[]? InputObject { get; set; }

        [Parameter]
        public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override bool Process(ScriptBlockFilter filter)
        {
            return !filter.All(this.InputObject);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0009:Member access should be qualified.", Justification = "Used in nameof()")]
        protected override bool ProcessWhenNoCondition()
        {
            throw new ArgumentException("Asserting an all-true condition requires a condition to be specified.", nameof(Condition));
        }

        protected override void End(bool scriptResult)
        {
            this.WriteObject(!scriptResult);
        }

        //protected override void BeginProcessing()
        //{
        //    _equality = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
        //}
        //protected override void ProcessRecord()
        //{
        //    if (!_stop)
        //    {
        //        _stop = !_equality.All(this.InputObject);
        //    }
        //}
        //protected override void EndProcessing()
        //{
        //    this.WriteObject(_stop);
        //}
    }
}
