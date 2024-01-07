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
using System.Runtime.CompilerServices;
using PSAllowNullAttribute = System.Management.Automation.AllowNullAttribute;

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "List", DefaultParameterSetName = "None")]
    [OutputType(typeof(List<>))]
    public sealed class NewListCmdlet : ListFunctionCmdletBase
    {
        internal static readonly Type ListTypeNoT = typeof(List<>);

        IList _list = null!;
        bool _listIsNull;


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
                : this.CreateNewList(this.Capacity, this.GenericType, out _listIsNull)!;
        }

        private IList? CreateNewList(int capacity, Type genericType, out bool listIsNull)
        {
            Type listType = ListTypeNoT.MakeGenericType(genericType);
            object[] args = new object[] { capacity };
            listIsNull = false;
            
            try
            {
                return (IList)Activator.CreateInstance(listType, args)!;
            }
            catch (Exception e)
            {
                var rec = e.ToRecord(ErrorCategory.InvalidArgument, listType);
                this.WriteError(rec);
                listIsNull = true;
                return null;
            }
        }

        protected override void ProcessRecord()
        {
            if (_listIsNull || this.InputObject is null || this.InputObject.Length <= 0)
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
            if (!_listIsNull)
            {
                this.WriteObject(_list, false);
            }
        }
    }
}
