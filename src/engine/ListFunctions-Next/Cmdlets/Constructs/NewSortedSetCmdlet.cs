using ListFunctions.Modern.Constructors;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Constructs
{
    [Cmdlet(VerbsCommon.New, "SortedSet", DefaultParameterSetName = "None")]
    [OutputType(typeof(SortedSet<>))]
    public sealed class NewSortedSetCmdlet : ListFunctionCmdletBase
    {
        [Parameter(Mandatory = false, Position = 0)]
        [ArgumentToTypeTransform]
        [PSDefaultValue(Value = typeof(object))]
        [Alias("Type")]
        public Type GenericType { get; set; } = null!;

        [Parameter(ValueFromPipeline = true)]
        public object[] InputObject { get; set; } = null!;

        [Parameter(ParameterSetName = WITH_CUSTOM_EQUALITY)]
        [PSDefaultValue(Value = ActionPreference.Stop)]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.Stop;

        protected override void BeginProcessing()
        {
            this.GenericType ??= typeof(object);
            IComparer? comparer = this.GetCustomComparer(this.GenericType);

            var ctor = new SortingCollectorCtor(this.GenericType, comparer);

            object col = ctor.Construct();
            this.WriteObject(col, false);
        }

        private IComparer? GetCustomComparer(Type genericType)
        {
            return null;
        }
    }
}

