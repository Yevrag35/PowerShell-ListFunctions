using ListFunctions.Extensions;
using ListFunctions.Modern;
using ListFunctions.Modern.Pools;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

#nullable enable

namespace ListFunctions.Cmdlets.Finds
{
    [Cmdlet(VerbsCommon.Find, "IndexOf")]
    [Alias("Find-Index", "IndexOf")]
    [OutputType(typeof(int))]
    public sealed class FindIndexCmdlet : ListFunctionCmdletBase
    {
        private ScriptBlockFilter _filter = null!;
        private int _currentIndex;
        private List<object?> _list = null!;

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        public ScriptBlock Condition { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [Alias("List")]
        [AllowEmptyCollection, AllowEmptyString, AllowNull]
        public object?[]? InputObject { get; set; }

        [Parameter, Alias("ScriptErrorAction")]
        public ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        protected override void BeginCore()
        {
            _filter = new ScriptBlockFilter(this.Condition, new PSVariable(ERROR_ACTION_PREFERENCE, this.ScriptBlockErrorAction));
            _list = ListPool<object?>.Rent();
        }
        protected override bool ProcessCore()
        {
            if (this.InputObject is null || this.InputObject.Length == 0)
                return true;    // keep going

            for (int i = 0; i < this.InputObject.Length; i++)
            {
                if (_filter.IsTrue(this.InputObject[i]))
                {
                    _currentIndex += i;
                    return false;   // stop processing
                }
            }

            _currentIndex += this.InputObject.Length;
            return true;
        }

        protected override void EndCore(bool wantsToStop)
        {
            int index = wantsToStop ? _currentIndex : -1;

            this.WriteObject(index);
        }

        protected override void Cleanup()
        {
            if (!(_filter is null))
            {
                _filter.Dispose();
                _filter = null!;
            }

            if (!(_list is null))
            {
                ListPool<object?>.Return(_list);
                _list = null!;
            }
        }

        //protected override void End(List<object?> list, PSVariable scriptErrorAction)
        //{

        //}

        //static readonly Lazy<Dictionary<Type, MethodInfo>> _indexMethods = 
        //    new Lazy<Dictionary<Type, MethodInfo>>(BuildMethodCache);

        //protected override MethodInfo GetFindIndexMethod(Type listType, Type genericType)
        //{
        //    var list = new List<object>();

        //    if (_indexMethods.IsValueCreated
        //        &&
        //        _indexMethods.Value.TryGetValue(genericType, out MethodInfo? info))
        //    {
        //        return info;
        //    }

        //    info = GetIndexMethodDefinition(listType, genericType);

        //    Debug.Assert(!(info is null));
        //    _indexMethods.Value.TryAdd(genericType, info);

        //    return info;
        //}

        //private static MethodInfo GetIndexMethodDefinition(Type listType, Type genericType)
        //{
        //    Type genPred = PredicateType.MakeGenericType(genericType);

        //    return listType.GetMethod(
        //        name: nameof(List<object>.FindIndex),
        //        bindingAttr: BindingFlags.Instance | BindingFlags.Public,
        //        binder: null,
        //        types: new Type[] { genPred },
        //        modifiers: null)!;
        //}

        //private static Dictionary<Type, MethodInfo> BuildMethodCache()
        //{
        //    return new Dictionary<Type, MethodInfo>(3);
        //}
    }
}

