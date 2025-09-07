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
    public sealed class AssertAllObjectsCmdlet : ListFunctionCmdletBase
    {
        ScriptBlockFilter _equality = null!;
        bool _stop;

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME)]
        public ScriptBlock Condition { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowNull, AllowEmptyCollection, AllowEmptyString]
        public object?[]? InputObject { get; set; }

        [Parameter]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override void BeginProcessing()
        {
            _equality = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
        }
        protected override void ProcessRecord()
        {
            if (!_stop)
            {
                _stop = !_equality.All(this.InputObject);
            }
        }
        protected override void EndProcessing()
        {
            this.WriteObject(_stop);
        }
    }
}
