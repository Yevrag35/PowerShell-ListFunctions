using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using AllowsNull = System.Diagnostics.CodeAnalysis.AllowNullAttribute;
using PSAllowNull = System.Management.Automation.AllowNullAttribute;

#nullable enable


namespace ListFunctions.Cmdlets.Assertions
{
    [Cmdlet(VerbsLifecycle.Assert, "AnyObject")]
    [Alias("Assert-Any", "Any-Object", "Any")]
    [OutputType(typeof(bool))]
    public sealed class AssertAnyObjectCmdlet : AssertObjectCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowEmptyCollection, PSAllowNull, AllowEmptyString]
        public object?[]? InputObject { get; set; }

        [Parameter(Position = 0)]
        [Alias("ScriptBlock", "FilterScript")]
        [PSAllowNull, AllowEmptyString, MaybeNull, AllowsNull]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME, PSThisVariable.ARGS_FIRST)]
        public override ScriptBlock Condition
        {
            get => base.Condition;
            set => base.Condition = value;
        }
        [Parameter, Alias("ScriptErrorAction")]
        public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override bool Process(ScriptBlockFilter filter)
        {
            return filter.Any(this.InputObject);
        }
        protected override bool ProcessWhenNoCondition()
        {
            if (!(this.InputObject is null))
            {
                foreach (object? item in this.InputObject)
                {
                    if (!(item is null))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        protected override void End(bool scriptResult)
        {
            this.WriteObject(scriptResult);
        }
    }
}
