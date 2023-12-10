using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;

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
            return new HashSet<object>(new ObjectEqualityComparer()
            {
                IgnoreCase = !this.IsCaseSensitive,
            });
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

        private sealed class ObjectEqualityComparer : IEqualityComparer<object?>, IEqualityComparer
        {
            internal bool IgnoreCase { get; set; }

            public new bool Equals(object? x, object? y)
            {
                return LanguagePrimitives.Equals(x, y, this.IgnoreCase);
            }

            public int GetHashCode(object? obj)
            {
                Guard.NotNull(obj, nameof(obj));

                if (obj is string s)
                {
                    return this.IgnoreCase
                        ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(s)
                        : StringComparer.InvariantCulture.GetHashCode(s);
                }
                else if (LanguagePrimitives.TryConvertTo(obj, out string? resStr))
                {
                    return this.IgnoreCase
                        ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(resStr)
                        : StringComparer.InvariantCulture.GetHashCode(resStr);
                }
                else
                {
                    return obj.GetHashCode();
                }
            }
        }
    }
}

