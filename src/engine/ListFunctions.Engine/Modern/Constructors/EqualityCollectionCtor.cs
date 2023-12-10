using ListFunctions.Extensions;
using ListFunctions.Modern.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace ListFunctions.Modern.Constructors
{
    public abstract class EqualityCollectionCtor : GenericCollectionCtor
    {
        public static readonly Type DefaultComparerTypeDefinition = typeof(EqualityComparer<>);
        static readonly Type _stringType = typeof(string);

        IEqualityComparer? _comparer;

        public bool IsCaseSensitive { get; set; }

        protected EqualityCollectionCtor(Type genericTypeDefinition, IEqualityComparer? comparer, Type[] genericTypes, CreateConstructingType? createConstructingCallback)
            : base(genericTypeDefinition, genericTypes, createConstructingCallback)
        {
            _comparer = comparer;
        }

        protected sealed override object ConstructDefault()
        {
            return this.ConstructDefault(this.GetComparerOrDefault());
        }
        protected abstract object ConstructDefault(IEqualityComparer comparer);
        private IEqualityComparer GetComparerOrDefault()
        {
            if (!(_comparer is null))
            {
                return _comparer;
            }

            Type equalityType = this.GetTypeForEquality();
            if (_stringType.Equals(equalityType))
            {
                return !this.IsCaseSensitive
                    ? StringComparer.InvariantCultureIgnoreCase
                    : StringComparer.InvariantCulture;
            }

            var genStaticType = DefaultComparerTypeDefinition.MakeGenericType(equalityType);

            PropertyInfo? defaultProp = genStaticType.GetProperty(
                nameof(EqualityComparer<object>.Default), BindingFlags.Static | BindingFlags.Public);

            _comparer = (IEqualityComparer)defaultProp?.GetValue(null)!;
            return _comparer;
        }
        protected override IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
        {
            yield return this.GetComparerOrDefault();
        }
        protected abstract Type GetTypeForEquality();

        public static bool IsTypeObjectOrString(Type? type)
        {
            return null != type && (ObjectType.Equals(type) || _stringType.Equals(type));
        }

        protected sealed override bool ShouldConstructDefault(Type[] genericTypes)
        {
            return this.ShouldConstructDefault(_comparer, genericTypes);
        }
        protected virtual bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
        {
            return genericTypes.Length <= 0;
        }
    }

    public abstract class EqualityCollectionCtor<TDefault> : EqualityCollectionCtor where TDefault : class
    {
        protected EqualityCollectionCtor(Type genericTypeDefinition, IEqualityComparer? comparer, Type[] genericTypes, CreateConstructingType? callback)
            : base(genericTypeDefinition, comparer, genericTypes, callback)
        {
        }

        protected sealed override object ConstructDefault(IEqualityComparer comparer)
        {
            return this.ConstructTDefault(comparer);
        }
        protected abstract TDefault ConstructTDefault(IEqualityComparer comparer);
    }
}

