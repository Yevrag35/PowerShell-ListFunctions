using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Constructors;
using ListFunctions.Modern.Variables;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

#nullable enable

namespace ListFunctions.Cmdlets.Constructs
{
    [Cmdlet(VerbsCommon.New, "SortedSet", DefaultParameterSetName = "None")]
    [OutputType(typeof(SortedSet<>))]
    public sealed class NewSortedSetCmdlet : ListFunctionCmdletBase
    {
        AddMethodInvoker _addMethod = null!;
        object[] _arr = null!;
        SortingCollectorCtor _ctor = null!;
        object _set = null!;

        [Parameter(Mandatory = false, Position = 0)]
        [ArgumentToTypeTransform]
        [PSDefaultValue(Value = typeof(object))]
        [Alias("Type")]
        public Type GenericType { get; set; } = null!;

        [Parameter(Mandatory = true, ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [ValidateScriptVariable(PSComparingVariable.X, PSComparingVariable.LEFT, PSThisVariable.ARGS_FIRST)]
        [ValidateScriptVariable(PSComparingVariable.Y, PSComparingVariable.RIGHT, PSThisVariable.ARGS_SECOND)]
        public ScriptBlock ComparingScript { get; set; } = null!;

        [Parameter(ValueFromPipeline = true)]
        public object[] InputObject { get; set; } = null!;

        [Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [PSDefaultValue(Value = ActionPreference.Stop)]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

        protected override void BeginCore()
        {
            this.GenericType ??= typeof(object);
            IComparer? comparer = this.GetCustomComparer(this.GenericType);

            _ctor = new SortingCollectorCtor(this.GenericType, comparer);
            _set = _ctor.Construct();
            
        }
        protected override bool ProcessCore()
        {
            bool flag = true;
            if (this.InputObject is null || this.InputObject.Length == 0)
            {
                return flag;
            }

            _addMethod ??= new AddMethodInvoker(_ctor);
            _arr ??= new object[1];

            foreach (object? item in this.InputObject)
            {
                if (item is null || !LanguagePrimitives.TryConvertTo(item, this.GenericType, out object? result))
                {
                    continue;
                }

                _arr[0] = result;
                if (!_addMethod.TryInvoke(_set, _arr, false, out Exception? caught))
                {
                    this.WriteError(caught.ToRecord(ErrorCategory.InvalidType, item));
                }
            }

            return flag;
        }
        protected override void EndCore(bool wantsToStop)
        {
            if (!wantsToStop)
            {
                this.WriteObject(_set);
            }
        }

        private IEnumerable<PSVariable> GetAction()
        {
            yield return new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction);
        }
        private IComparer? GetCustomComparer(Type genericType)
        {
            return this.MyInvocation.BoundParameters.ContainsKey(nameof(this.ComparingScript))
                ? ComparingBlock.Create(this.ComparingScript, genericType, this.GetAction())
                : null;
        }
    }
}

