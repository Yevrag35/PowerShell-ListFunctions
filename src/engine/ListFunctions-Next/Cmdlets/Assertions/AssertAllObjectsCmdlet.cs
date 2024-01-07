using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace ListFunctions.Cmdlets.Assertions
{
    [Cmdlet(VerbsLifecycle.Assert, "AllObject")]
    [Alias("Assert-AllObjects", "Assert-All", "All", "All-Object", "All-Objects")]
    [OutputType(typeof(bool))]
    public sealed class AssertAllObjectsCmdlet : ListFunctionCmdletBase
    {
        ScriptBlockFilter<object> _equality = null!;
        bool _stop;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowEmptyCollection]
        [AllowNull]
        public object[] InputObject { get; set; } = null!;

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME)]
        public ScriptBlock Condition { get; set; } = null!;

        [Parameter]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override void BeginProcessing()
        {
            _equality = new ScriptBlockFilter<object>(this.Condition, EnumerateVariables(this.ScriptBlockErrorAction));
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

        private static IEnumerable<PSVariable> EnumerateVariables(ActionPreference errorPref)
        {
            yield return new PSVariable(ERROR_ACTION_PREFERENCE, errorPref);
        }
    }
}
