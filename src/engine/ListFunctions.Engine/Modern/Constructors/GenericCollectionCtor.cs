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
    public delegate Type CreateConstructingType(Type genericTypeDefinition, Type[] genericTypeArguments);
    public abstract class GenericCollectionCtor
    {
        public static readonly Type ObjectType = typeof(object);

        public Type ConstructingGenericType { get; private set; }
        public Type GenericDefinitionType { get; }
        public Type[] GenericArgumentTypes { get; private set; }
        public bool HasGenerics => this.GenericArgumentTypes.Length > 0;

        protected GenericCollectionCtor(Type genericDefinition, Type[]? genericTypes, CreateConstructingType? makeConstructingTypeCallback)
        {
            Guard.NotNull(genericDefinition, nameof(genericDefinition));
            GuardDefinition(genericDefinition);

            this.GenericDefinitionType = genericDefinition;
            this.GenericArgumentTypes = SetGenericTypes(genericTypes);
            this.ConstructingGenericType = makeConstructingTypeCallback is null
                ? MakeConstructingType(genericDefinition, this.GenericArgumentTypes)
                : makeConstructingTypeCallback(genericDefinition, this.GenericArgumentTypes);
        }

        public object Construct()
        {
            if (this.ShouldConstructDefault(this.GenericArgumentTypes))
            {
                object defCol = this.ConstructDefault();
                this.ConstructingGenericType = defCol.GetType();
                this.GenericArgumentTypes = this.ConstructingGenericType.GetGenericArguments()
                    ?? Array.Empty<Type>();

                return defCol;
            }

            object?[]? ctorArgs = this.EnumerateCtorArguments(this.GenericArgumentTypes);

            return CallActivator(this.ConstructingGenericType, ctorArgs);
        }

        private object?[] EnumerateCtorArguments(Type[] genericTypes)
        {
            object?[]? ctorArgs;
            var enumCtorArgs = this.GetConstructorArguments(genericTypes);
            if (enumCtorArgs is null)
            {
                ctorArgs = Array.Empty<object>();
            }
            else
            {
                ctorArgs = enumCtorArgs.ToArray();
            }

            return ctorArgs;
        }

        /// <exception cref="ActivatorCtorException"/>
        private static object CallActivator(Type constructingType, object?[]? ctorArgs)
        {
            object? collection;
            try
            {
                collection = Activator.CreateInstance(constructingType, ctorArgs);
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
                return ThrowBadCtor(constructingType, ex);
            }

            return collection
                ?? ThrowBadCtor(constructingType);
        }

        protected abstract object ConstructDefault();
        protected virtual IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
        {
            return null;
        }

        /// <exception cref="ArgumentException"></exception>
        private static void GuardDefinition(Type genericDefinition)
        {
            if (!genericDefinition.IsGenericType)
            {
                throw new ArgumentException($"\"{genericDefinition.GetTypeName()}\" is not a valid generic type.");
            }
            else if (!genericDefinition.IsClass)
            {
                throw new ArgumentException($"\"{genericDefinition.GetTypeName()}\" is not a valid generic class.");
            }
            else if (genericDefinition.IsAbstract)
            {
                throw new ArgumentException($"\"{genericDefinition.GetTypeName()}\" cannot be an abstract class.");
            }
        }
        private static Type MakeConstructingType(Type genericTypeDefinition, Type[] genericTypes)
        {
            return genericTypeDefinition.MakeGenericType(genericTypes);
        }
        protected abstract bool ShouldConstructDefault(Type[] genericTypes);
        private static Type[] SetGenericTypes(IReadOnlyList<Type>? types)
        {
            if (types is null || types.Count <= 0)
            {
                return Array.Empty<Type>();
            }

            Type[] copyTo = new Type[types.Count];
            for (int i = 0; i < types.Count; i++)
            {
                copyTo[i] = types[i];
            }

            return copyTo;
        }
        [DoesNotReturn]
        private static object ThrowBadCtor(Type badType)
        {
            throw new ActivatorCtorException(badType);
        }
        [DoesNotReturn]
        private static object ThrowBadCtor(Type badType, Exception caughtException)
        {
            throw new ActivatorCtorException(badType, caughtException);
        }
    }
}

