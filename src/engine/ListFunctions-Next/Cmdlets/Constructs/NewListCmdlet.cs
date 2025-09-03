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
using ZLinq;
using AllowsNullAttribute = System.Diagnostics.CodeAnalysis.AllowNullAttribute;
using PSAllowNullAttribute = System.Management.Automation.AllowNullAttribute;
#nullable enable

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "List", DefaultParameterSetName = "None")]
    [OutputType(typeof(List<>))]
    public sealed class NewListCmdlet : ListFunctionCmdletBase
    {
        internal static readonly Type ListTypeNoT = typeof(List<>);
        private static readonly object[] _defaultCapacityArgs = new[] { (object)4 };

        private bool _isObjectType;
        private IList _list = Array.Empty<object>();
        private bool _listIsNull;
        private Type? _genericType;

        [Parameter(Position = 1)]
        [Alias("Size"), PSDefaultValue(Value = 4), ValidateRange(0, int.MaxValue)]
        public int Capacity { get; set; }

        [Parameter(Position = 0)]
        [Alias("Type"), ArgumentToTypeTransform, PSDefaultValue(Value = typeof(object))]
        [AllowsNull, PSAllowNull]
        public Type GenericType
        {
            get => _genericType ??= this.SetToObjectType();
            set => _genericType = this.SetToObjectType(value);
        }

        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "InitialAdd")]
        [AllowEmptyCollection, PSAllowNull, AllowEmptyString]
        public IList? InputObject { get; set; }

        [Parameter(ParameterSetName = "InitialAdd")]
        [Alias("IncludeNulls")]
        public SwitchParameter IncludeNullElements { get; set; }

        protected override void BeginProcessing()
        {
            _list = _genericType is null
                ? new List<object?>(this.Capacity > 0 ? this.Capacity : 4)
                : this.CreateNewList(this.Capacity, this.GenericType, out _listIsNull)!;
        }

        private IList? CreateNewList(int capacity, Type genericType, out bool listIsNull)
        {
            Type listType = ListTypeNoT.MakeGenericType(genericType);

            object[] args = capacity > 0
                ? new[] { (object)capacity }
                : _defaultCapacityArgs;
            
            try
            {
                listIsNull = false;
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
        private Type SetToObjectType()
        {
            _isObjectType = true;
            return typeof(object);
        }
        private Type SetToObjectType(Type? type)
        {
            if (type is null)
                return this.SetToObjectType();

            else if (typeof(object).Equals(type))
                _isObjectType = true;

            else
                _isObjectType = false;

            return type;
        }

        protected override void ProcessRecord()
        {
            if (_listIsNull || this.InputObject is null || this.InputObject.Count == 0)
            {
                return;
            }

            if (_isObjectType)
            {
                this.AddItemsToList(_list);
            }
            else
            {
                this.AddTypedItemsToList(_list);
            }
        }
        private void AddItemsToList(IList list)
        {
            foreach (object? item in list.AsValueEnumerable())
            {
                if (item is null && !this.IncludeNullElements)
                {
                    continue;
                }

                list.Add(item);
            }
        }
        private void AddTypedItemsToList(IList list)
        {
            Type genericType = this.GenericType;
            foreach (object? item in list.AsValueEnumerable())
            {
                if (item is null && !this.IncludeNullElements)
                {
                    continue;
                }
                
                if (this.TryConvertItem(item, genericType, out object? result))
                {
                    list.Add(result);
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
