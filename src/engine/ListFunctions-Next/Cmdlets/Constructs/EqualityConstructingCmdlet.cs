using ListFunctions.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace ListFunctions.Cmdlets.Construct
{
    public abstract class EqualityConstructingCmdlet<T> : ListFunctionCmdletBase
    {
        protected const string CASE_SENSE = "CaseSensitive";
        protected const string WITH_CUSTOM_EQUALITY = "WithCustomEquality";
        static readonly Type _defComparerType = typeof(EqualityComparer<>);
        static readonly Type _stringType = typeof(string);
        static readonly string _addName = nameof(ICollection<object>.Add);

        private MethodInfo? _addMethod;
        private RuntimeDefinedParameter _caseSensitive = null!;
        private T _collection = default!;
        private RuntimeDefinedParameterDictionary _dict = null!;
        private Type[] _genericTypes = null!;

        protected MethodInfo? AddMethod
        {
            get => _addMethod;
            set
            {
                _addMethod = value;
                this.HasAddMethod = !(_addMethod is null);
            }
        }
        protected abstract Type BaseType { get; }
#if NET5_0_OR_GREATER
        [MemberNotNullWhen(true, nameof(_addMethod))]
#endif
        protected bool HasAddMethod { get; set; }
        public virtual int Capacity { get; set; }
        protected bool CaseSensitive => RetrieveCaseSensitiveSetting(_caseSensitive);
        public virtual ActionPreference ScriptBlockErrorAction { get; set; }

        protected virtual RuntimeDefinedParameterDictionary? GetDynamicCaseParam(Type genericType, string parameterSetName)
        {
            RuntimeDefinedParameterDictionary? dict = null;

            if (_stringType.Equals(genericType))
            {
                _caseSensitive ??= new RuntimeDefinedParameter(CASE_SENSE, typeof(SwitchParameter),
                    new Collection<Attribute>()
                    {
                        new ParameterAttribute()
                        {
                            Mandatory = true,
                            ParameterSetName = parameterSetName,
                        }
                    });

                dict = _dict ??= new RuntimeDefinedParameterDictionary();

                dict.TryAdd(CASE_SENSE, _caseSensitive);
            }

            return dict;
        }

        protected void AddToCollection(T collection, object?[]? items)
        {
            if (_addMethod is null || items is null || items.Length < 2 || items[0] is null)
            {
                return;
            }

            try
            {
                _addMethod.Invoke(collection, items);
            }
            catch (Exception e)
            {
                this.WriteError(e.ToRecord(ErrorCategory.InvalidOperation, items));
            }
        }
        protected void AddToCollection(T collection, object?[]? item, bool addIfNull)
        {
            if (_addMethod is null || (item is null && !addIfNull))
            {
                return;
            }
            
            try
            {
                _addMethod.Invoke(collection, item);
            }
            catch (Exception e)
            {
                this.WriteError(e.ToRecord(ErrorCategory.InvalidOperation, item));
            }
        }
        private static MethodInfo? GetAddMethod(Type baseType, Type[] genericTypes)
        {
            try
            {
                return baseType.GetMethod(
                    _addName,
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    genericTypes,
                    null);
            }
            catch (Exception e)
            {
                Debug.Fail(e.Message);
                return null;
            }
        }

        protected sealed override void BeginProcessing()
        {
            Type[]? genericTypes = this.GetGenericTypes();

            if (genericTypes is null)
            {
                IEqualityComparer? comparer = this.GetCustomEqualityComparer(typeof(object));
                _collection = this.ConstructOnTypesMissing(comparer);
            }
            else
            {
                _collection = this.ConstructCollection(genericTypes);
            }

            Guard.NotNull(_collection, "collection");
            this.Begin(_collection, _collection.GetType());
        }

        private T ConstructCollection(Type[] genericTypes)
        {
            Type genericBaseType = this.MakeGenericType(genericTypes);
            Type equalityType = this.GetEqualityForType();
            IEqualityComparer? comparer = this.GetCustomEqualityComparer(equalityType);
            comparer ??= GetDefaultComparer(equalityType, this.CaseSensitive);

            object[]? ctorArgs = this.GetConstructorArguments(genericTypes, comparer);
            try
            {
                return (T)Activator.CreateInstance(genericBaseType, ctorArgs)!;
            }
            catch (Exception e)
            {
                this.ThrowTerminatingError(e.ToRecord(ErrorCategory.InvalidOperation, genericBaseType));
                return default!; // this won't happen.
            }
        }

        protected virtual void Begin(T collection, Type genericBaseType)
        {
            return;
        }

        protected sealed override void ProcessRecord()
        {
            this.Process(_collection);
        }
        protected virtual void Process(T collection)
        {
            return;
        }

        protected sealed override void EndProcessing()
        {
            this.End(_collection);
        }
        protected virtual void End(T collection)
        {
            return;
        }

        protected abstract T ConstructOnTypesMissing(IEqualityComparer? comparer);
        protected abstract object[]? GetConstructorArguments(Type[] genericTypes, IEqualityComparer? comparer);
        protected static IEqualityComparer? GetDefaultComparer(Type genericType, bool caseSensitive)
        {
            if (typeof(string).Equals(genericType))
            {
                return !caseSensitive
                    ? StringComparer.InvariantCultureIgnoreCase
                    : StringComparer.InvariantCulture;
            }

            var genStaticType = _defComparerType.MakeGenericType(genericType);

            PropertyInfo? defaultProp = genStaticType.GetProperty(
                nameof(EqualityComparer<object>.Default), BindingFlags.Static | BindingFlags.Public);

            return (IEqualityComparer?)defaultProp?.GetValue(null);
        }
        
        protected virtual IEqualityComparer? GetCustomEqualityComparer(Type genericType)
        {
            return null;
        }
        protected abstract Type[]? GetGenericTypes();
        protected abstract Type GetEqualityForType();

        /// <exception cref="ArgumentNullException"/>
        private Type MakeGenericType(Type[] genericTypes)
        {
            Guard.NotNull(genericTypes, nameof(genericTypes));

            _genericTypes = genericTypes;
            Type genBaseType = this.BaseType.MakeGenericType(genericTypes);

            _addMethod = GetAddMethod(genBaseType, genericTypes);
            return genBaseType;
        }

        private static bool RetrieveCaseSensitiveSetting(RuntimeDefinedParameter? parameter)
        {
            return !(parameter is null) && parameter.Value is SwitchParameter swParam && swParam.ToBool();
        }
    }
}
