using ListFunctions.Cmdlets.Construct;
using ListFunctions.Extensions;
using ListFunctions.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;

namespace ListFunctions.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ListTransformAttribute : ArgumentTransformationAttribute
    {
        static readonly Type _objArr = typeof(object[]);

        public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData)
        {
            object? target = inputData.GetBaseObject();

            if (!(target is null)
                && 
                !IsObjectArrayType(target, out Type actualType)
                &&
                IsGenericList(actualType))
            {
                return target;
            }
            else if (target is IEnumerable enumerable)
            {
                var list = new List<object?>(2);

                foreach (object? item in enumerable)
                {
                    list.Add(item);
                }

                return list;
            }
            else
            {
                return new PipelineItem(target);
            }
        }

        private static bool IsGenericList(Type actualType)
        {
            return actualType.IsGenericType
                   &&
                   NewListCmdlet.ListTypeNoT.Equals(actualType.GetGenericTypeDefinition());
        }
        private static bool IsObjectArrayType(object target, out Type actualType)
        {
            actualType = target.GetType();
            return _objArr.Equals(actualType);
        }
    }
}

