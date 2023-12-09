using ListFunctions.Extensions;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using PSAllowNullAttribute = System.Management.Automation.AllowNullAttribute;

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "List", DefaultParameterSetName = "None")]
    [OutputType(typeof(List<>))]
    public sealed class NewListCmdlet : ListFunctionCmdletBase
    {
        internal static readonly Type ListTypeNoT = typeof(List<>);

        IList _list = null!;

        [Parameter(Position = 1)]
        [PSDefaultValue(Value = 0)]
        public int Capacity { get; set; }

        [Parameter(Position = 0)]
        [Alias("Type")]
        [ArgumentToTypeTransform]
        [PSDefaultValue(Value = typeof(object))]
        public Type GenericType { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "InitialAdd")]
        [AllowEmptyCollection]
        [PSAllowNull]
        public object[] InputObject { get; set; } = null!;

        [Parameter(ParameterSetName = "InitialAdd")]
        public SwitchParameter IncludeNullElements { get; set; }

        protected override void BeginProcessing()
        {
            _list = this.GenericType is null
                ? new List<object>(this.Capacity)
                : CreateNewList(this.Capacity, this.GenericType);
        }

        private static IList CreateNewList(int capacity, Type genericType)
        {
            Type listType = ListTypeNoT.MakeGenericType(genericType);
            object[] args = new object[] { capacity };

            return (IList)Activator.CreateInstance(listType, args)!;
        }

        protected override void ProcessRecord()
        {
            if (this.InputObject is null || this.InputObject.Length <= 0)
            {
                return;
            }

            Type genericType = this.GenericType;

            foreach (object? item in this.InputObject)
            {
                if (item is null && !this.IncludeNullElements)
                {
                    continue;
                }
                else if (this.TryConvertItem(item, genericType, out object? result))
                {
                    _list.Add(result);
                }
            }
        }
        protected override void EndProcessing()
        {
            this.WriteObject(_list, false);
        }
    }
}
