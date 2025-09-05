using ListFunctions.Cmdlets.Construct;
using ListFunctions.Extensions;
using ListFunctions.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;

#nullable enable

namespace ListFunctions.Validation
{
    /// <summary>
    /// Specifies that the target property or field should be transformed into a list representation when used in a
    /// PowerShell pipeline or similar context.
    /// </summary>
    /// <remarks>This attribute is applied to properties or fields to ensure that input data is converted into
    /// a list-like structure. If the input data is already a generic list, it is returned as-is. If the input data is
    /// an enumerable type, its elements are collected into a new list. Otherwise, the input data is wrapped in a <see
    /// cref="PipelineItem"/> object.</remarks>
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

