using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ListFunctions.Cmdlets.Construct
{
    [Cmdlet(VerbsCommon.New, "List", DefaultParameterSetName = "None")]
    [OutputType(typeof(List<>))]
    public sealed class NewListCmdlet : ListFunctionCmdletBase
    {
        static readonly Type _type = typeof(List<>);

        MethodInfo? _addMethod;
        Type _listType = null!;
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
        [AllowNull]
        public object[] InputObject { get; set; } = null!;

        [Parameter(ParameterSetName = "InitialAdd")]
        public SwitchParameter IncludeNullElements { get; set; }

        protected override void BeginProcessing()
        {
            this.GenericType ??= typeof(object);
            _list = CreateNewList(this.Capacity, this.GenericType, out _listType);
        }

        private static IList CreateNewList(int capacity, Type genericType, out Type listType)
        {
            listType = _type.MakeGenericType(genericType);
            object[] args = new object[] { capacity };

            return (IList)Activator.CreateInstance(listType, args)!;
        }

        protected override void ProcessRecord()
        {
            if (this.InputObject is null)
            {
                return;
            }

            foreach (object? item in this.InputObject)
            {
                if (item is null && !this.IncludeNullElements)
                {
                    continue;
                }

                if (LanguagePrimitives.TryConvertTo(item, this.GenericType, out object? converted))
                {
                    _ = _list.Add(converted);
                }
                else
                {
                    string type = item?.GetType().FullName ?? "null";

                    this.WriteError(new ErrorRecord(
                        new InvalidCastException($"Unable to convert item of type '{type}' to '{this.GenericType.FullName}'."),
                        typeof(InvalidCastException).Name, ErrorCategory.InvalidData,
                        item));
                } 
            }
        }
        protected override void EndProcessing()
        {
            this.WriteObject(_list, false);
        }
    }
}
