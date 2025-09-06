using ListFunctions.Extensions;
using ListFunctions.Internal;
using ListFunctions.Modern.Pools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

#nullable enable

namespace ListFunctions.Cmdlets.Finds
{
    public abstract class FindIndexCmdletBase : ListFunctionCmdletBase
    {
        private List<object?> _list = null!;

        public abstract ScriptBlock Condition { get; set; }
        public abstract object?[]? InputObject { get; set; }
        public abstract ActionPreference ScriptBlockErrorAction { get; set; }

        protected sealed override void BeginProcessing()
        {
            _list = ListPool.Rent();

            try
            {
                this.Begin(_list);
            }
            catch
            {
                this.CleanupCore();
                throw;
            }
        }
        protected virtual void Begin(List<object?> list)
        {
        }
        protected sealed override void ProcessRecord()
        {
            if (!(this.InputObject is null || this.InputObject.Length == 0))
            {
                foreach (object? obj in this.InputObject)
                {
                    _list.Add(obj);
                }
            }

            try
            {
                this.Process(_list);
            }
            catch
            {
                this.CleanupCore();
                throw;
            }
        }
        protected virtual void Process(List<object?> list)
        {
        }
        protected sealed override void StopProcessing()
        {
            this.CleanupCore();
            base.StopProcessing();
        }
        protected sealed override void EndProcessing()
        {
            PSVariable errorAction = new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction);
            List<PSVariable> varList = ListPool<PSVariable>.Rent();
            try
            {
                this.End(_list, errorAction, varList);
            }
            catch
            {
                this.CleanupCore();
                ListPool<PSVariable>.Return(varList);
                throw;
            }

            //var filter = ScriptBlockFilter.Create(this.Condition, _genType, this.EnumerateVariables());
            //object predicate = ScriptBlockFilter.ToPredicate(filter);

            //int index = -1;
            //try
            //{
            //    index = (int?)_method.Invoke(_list, new object[] { predicate }) ?? -1;
            //}
            //catch (Exception e)
            //{
            //    this.WriteError(e.ToRecord(ErrorCategory.InvalidOperation, _list));
            //}

            //this.WriteObject(index);
            this.CleanupCore();
            ListPool<PSVariable>.Return(varList);
            base.EndProcessing();
        }
        protected abstract void End(List<object?> list, PSVariable scriptErrorAction, List<PSVariable> varList);

        //protected abstract MethodInfo GetFindIndexMethod(Type listType, Type genericType);

        //private static void AddListToList(IList inputObject, List<object?> list)
        //{
        //    foreach (object? item in inputObject)
        //    {
        //        list.Add(item);
        //    }
        //}
        private void CleanupCore()
        {
            ListPool.Return(_list);
            _list = null!;
            this.Cleanup();
        }
        protected virtual void Cleanup()
        {
        }
        //private IList GetListAndGeneric(IList inputObject)
        //{
        //    IList returnList;
        //    Type listType;
        //    Type genType;

        //    if (inputObject.IsFixedSize)
        //    {
        //        genType = typeof(object);
        //        var list = new List<object?>(inputObject.Count);
        //        AddListToList(ref inputObject, ref list);

        //        returnList = list;
        //        listType = list.GetType();
        //    }
        //    else
        //    {
        //        listType = inputObject.GetType();
        //        genType = listType.GetGenericArguments().First();
        //        returnList = inputObject;
        //    }

        //    _genType = genType;
        //    _method = this.GetFindIndexMethod(listType, _genType);
        //    return returnList;
        //}

        protected static bool TryGetEnumerable(object? obj, [NotNullWhen(true)] out IEnumerable? collection)
        {
            collection = LanguagePrimitives.GetEnumerable(obj);
            return !(collection is null);
        }
    }
}

