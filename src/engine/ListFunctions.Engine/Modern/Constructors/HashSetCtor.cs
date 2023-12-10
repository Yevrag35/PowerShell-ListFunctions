using System;
using System.Collections;
using System.Collections.Generic;

namespace ListFunctions.Modern.Constructors
{
    public sealed class HashSetCtor : EqualityCollectionCtor<HashSet<object>>
    {
        public static readonly Type HashSetTypeDefinition = typeof(HashSet<>);
        readonly Type _equalityType;

        public HashSetCtor(Type? genericType, IEqualityComparer? equalityComparer)
            : base(HashSetTypeDefinition, equalityComparer, ToArrayOrEmpty(genericType), null)
        {
            _equalityType = genericType ?? typeof(object);
        }
        private static Type[] ToArrayOrEmpty(Type? genericType)
        {
            return genericType is null
                ? new Type[] { ObjectType }
                : new Type[] { genericType };
        }
        protected override HashSet<object> ConstructTDefault(IEqualityComparer comparer)
        {
            IEqualityComparer<object> useComparer = comparer is IEqualityComparer<object> gc
                ? gc
                : EqualityComparer<object>.Default;

            return new HashSet<object>(useComparer);
        }
        protected sealed override Type GetTypeForEquality()
        {
            return _equalityType;
        }
        protected override bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
        {
            return ObjectType.Equals(_equalityType)
                   ||
                   base.ShouldConstructDefault(comparer, genericTypes);
        }
    }
}

