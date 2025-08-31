using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ListFunctions.Modern.Constructors
{
    public sealed class DictionaryCtor : EqualityCollectionCtor<Hashtable>
    {
        public static readonly Type TypeDefinition = typeof(Dictionary<,>);
        public Type KeyType { get; }
        public Type ValueType { get; }

        public DictionaryCtor(IEqualityComparer? comparer, Type? keyType, Type? valueType)
            : base(TypeDefinition, comparer, new Type[] { SetTypeOrObject(ref keyType), SetTypeOrObject(ref valueType) }, null)
        {
            this.KeyType = keyType;
            this.ValueType = valueType;
        }

        protected override Hashtable ConstructTDefault(IEqualityComparer comparer)
        {
            var comp = this.IsCaseSensitive
                ? StringComparer.InvariantCulture
                : StringComparer.InvariantCultureIgnoreCase;

            return new Hashtable(comp);
        }

        protected override Type GetTypeForEquality()
        {
            return this.KeyType;
        }
        private static Type SetTypeOrObject([NotNull] ref Type? type)
        {
            type ??= ObjectType;
            return type;
        }
        protected override bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
        {
            return base.ShouldConstructDefault(comparer, genericTypes)
                   ||
                   (
                        comparer is not IEqualityBlock
                        &&
                        genericTypes.All(x => ObjectType.Equals(x))
                   );
        }
    }
}

