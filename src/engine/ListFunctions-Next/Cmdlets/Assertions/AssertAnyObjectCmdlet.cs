using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using ListFunctions.Modern;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using PSAllowNullAttribute = System.Management.Automation.AllowNullAttribute;

namespace ListFunctions.Cmdlets.Assertions
{
    [Cmdlet(VerbsLifecycle.Assert, "AnyObject")]
    [Alias("Assert-Any", "Any-Object", "Any")]
    [OutputType(typeof(bool))]
    public sealed class AssertAnyObjectCmdlet : ListFunctionCmdletBase
    {
        ScriptBlock? _condition;
        ActionPreference _errorPref;
        bool _hasCondition;
        bool _hasNonNull;
        ScriptBlockFilter<object> _equality = null!;
        bool _stop;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [AllowEmptyCollection]
        [PSAllowNull]
        public object[] InputObject { get; set; } = null!;

        [Parameter(Position = 0)]
        [Alias("ScriptBlock")]
        [ValidateScriptVariable(PSThisVariable.UNDERSCORE_NAME, PSThisVariable.THIS_NAME, PSThisVariable.PSITEM_NAME)]
        public ScriptBlock? Condition
        {
            get => _condition;
            set
            {
                _condition = value;
                _hasCondition = !(_condition is null);
            }
        }

#if NET5_0_OR_GREATER
        [MemberNotNullWhen(true, nameof(Condition), nameof(_condition))]
#endif
        internal bool HasCondition => _hasCondition;

        protected override void BeginProcessing()
        {
            _errorPref = this.GetErrorPreference();
            if (this.HasCondition)
            {
                _equality = new ScriptBlockFilter<object>(this.Condition!, EnumerateVariables(_errorPref));
            }
        }
        protected override void ProcessRecord()
        {
            if (!this.HasCondition)
            {
                if (!_hasNonNull)
                {
                    ProcessWhenNoCondition(this.InputObject, ref _hasNonNull);
                }

                return;
            }
            else if (!_stop)
            {
                _stop = _equality.Any(this.InputObject);
            }
        }
        private static void ProcessWhenNoCondition(object[]? inputObjects, ref bool hasNonNull)
        {
            if (!(inputObjects is null))
            {
                foreach (object? o in inputObjects)
                {
                    if (!(o is null))
                    {
                        hasNonNull = true;
                        break;
                    }
                }
            }
        }
        protected override void EndProcessing()
        {
            if (!this.HasCondition)
            {
                this.WriteObject(_hasNonNull);
                return;
            }
            else
            {
                this.WriteObject(_stop);
                return;
            }
        }

        private static IEnumerable<PSVariable> EnumerateVariables(ActionPreference errorPref)
        {
            yield return new PSVariable(ERROR_ACTION_PREFERENCE, errorPref);
        }
    }
}
