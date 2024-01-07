using ListFunctions.Extensions;
using ListFunctions.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Finds
{
    [Cmdlet(VerbsCommon.Find, "IndexOf")]
    [Alias("Find-Index", "IndexOf")]
    [OutputType(typeof(int))]
    public sealed class FindIndexCmdlet : FindIndexCmdletBase
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("ScriptBlock")]
        public override ScriptBlock Condition { get; set; } = null!;

        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        [Alias("List")]
        [AllowEmptyCollection]
        [ValidateNotNull]
        [ListTransform]
        public override IList InputObject { get; set; } = null!;

        [Parameter]
        public override ActionPreference ScriptBlockErrorAction { get; set; } = ActionPreference.SilentlyContinue;

        static readonly Lazy<Dictionary<Type, MethodInfo>> _indexMethods = 
            new Lazy<Dictionary<Type, MethodInfo>>(BuildMethodCache);

        protected override MethodInfo GetFindIndexMethod(Type listType, Type genericType)
        {
            var list = new List<object>();

            if (_indexMethods.IsValueCreated
                &&
                _indexMethods.Value.TryGetValue(genericType, out MethodInfo? info))
            {
                return info;
            }

            info = GetIndexMethodDefinition(listType, genericType);

            Debug.Assert(!(info is null));
            _indexMethods.Value.TryAdd(genericType, info);

            return info;
        }

        private static MethodInfo GetIndexMethodDefinition(Type listType, Type genericType)
        {
            Type genPred = PredicateType.MakeGenericType(genericType);

            return listType.GetMethod(
                name: nameof(List<object>.FindIndex),
                bindingAttr: BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new Type[] { genPred },
                modifiers: null)!;
        }

        private static Dictionary<Type, MethodInfo> BuildMethodCache()
        {
            return new Dictionary<Type, MethodInfo>(3);
        }
    }
}

