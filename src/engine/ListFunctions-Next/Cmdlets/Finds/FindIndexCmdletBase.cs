using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Modern;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Finds
{
    public abstract class FindIndexCmdletBase : ListFunctionCmdletBase
    {
        protected static readonly Type PredicateType = typeof(Predicate<>);

        bool _wasList;
        Type _genType = null!;
        IList _list = null!;
        MethodInfo _method = null!;

        public abstract ScriptBlock Condition { get; set; }
        public abstract IList InputObject { get; set; }
        public abstract ActionPreference ScriptBlockErrorAction { get; set; }

        protected sealed override void BeginProcessing()
        {
            if (!(this.InputObject is null))
            {
                Type listType = this.InputObject.GetType();
                _genType = listType.GetGenericArguments().First();
                _method = this.GetFindIndexMethod(listType, _genType);
                _list = this.InputObject;
                _wasList = true;
            }
        }
        protected sealed override void ProcessRecord()
        {
            if (_wasList || this.InputObject is null)
            {
                return;
            }

            if (_genType is null)
            {
                _list = this.GetListAndGeneric(this.InputObject);
            }
            else
            {
                foreach (object? item in this.InputObject)
                {
                    _list.Add(item);
                }
            }
        }
        protected sealed override void EndProcessing()
        {
            var filter = ScriptBlockFilter.Create(this.Condition, _genType, this.EnumerateVariables());
            object predicate = ScriptBlockFilter.ToPredicate(filter);

            int index = -1;
            try
            {
                index = (int?)_method.Invoke(_list, new object[] { predicate }) ?? -1;
            }
            catch (Exception e)
            {
                this.WriteError(e.ToRecord(ErrorCategory.InvalidOperation, _list));
            }

            this.WriteObject(index);
        }
        protected abstract MethodInfo GetFindIndexMethod(Type listType, Type genericType);

        private static void AddListToList(ref IList inputObject, ref List<object?> list)
        {
            if (inputObject is PipelineItem pi)
            {
                list.Add(pi.Value);
            }
            else
            {
                foreach (object? item in inputObject)
                {
                    list.Add(item);
                }
            }
        }
        private IEnumerable<PSVariable> EnumerateVariables()
        {
            return new PSVariable[] { new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction) };
        }
        private IList GetListAndGeneric(IList inputObject)
        {
            IList returnList;
            Type listType;
            Type genType;

            if (inputObject.IsFixedSize)
            {
                genType = typeof(object);
                var list = new List<object?>(inputObject.Count);
                AddListToList(ref inputObject, ref list);

                returnList = list;
                listType = list.GetType();
            }
            else
            {
                listType = inputObject.GetType();
                genType = listType.GetGenericArguments().First();
                returnList = inputObject;
            }

            _genType = genType;
            _method = this.GetFindIndexMethod(listType, _genType);
            return returnList;
        }
    }
}

