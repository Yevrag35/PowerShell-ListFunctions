using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
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
        ActionPreference _errorPref;
        List<object?> _list = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowEmptyCollection]
        [AllowNull]
        public object[] InputObject { get; set; } = null!;

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME)]
        public ScriptBlock Condition { get; set; } = null!;

        protected override void BeginProcessing()
        {
            _errorPref = this.GetErrorPreference();
            _list = new List<object?>();
        }
        protected override void ProcessRecord()
        {
            if (this.InputObject is null)
            {
                _list.Add(null);
            }
            else
            {
                _list.AddRange(this.InputObject);
            }
        }
        protected override void EndProcessing()
        {
            if (_list.Count <= 0)
            {
                this.WriteObject(false);
                return;
            }

            ScriptBlockEquality<object> equality = new ScriptBlockEquality<object>(this.Condition, EnumerateVariables(_errorPref));

            bool hasAll = equality.All(_list!);
            this.WriteObject(hasAll);
        }

        private static IEnumerable<PSVariable> EnumerateVariables(ActionPreference errorPref)
        {
            yield return new PSVariable(ERROR_ACTION_PREFERENCE, errorPref);
        }
    }
}
