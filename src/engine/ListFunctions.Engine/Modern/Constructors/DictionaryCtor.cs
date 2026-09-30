using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ListFunctions.Modern.Constructors;

public sealed class DictionaryCtor : EqualityCollectionCtor<Hashtable>
{
	public static readonly Type TypeDefinition = typeof(Dictionary<,>);
	public Type KeyType { get; }
	public Type ValueType { get; }

	public DictionaryCtor(IEqualityComparer? comparer, Type? keyType, Type? valueType)
		: base(TypeDefinition, comparer, [SetTypeOrObject(ref keyType), SetTypeOrObject(ref valueType)], null)
	{
		this.KeyType = keyType;
		this.ValueType = valueType;
	}

	/// <summary>
	/// Creates a <see cref="Hashtable"/> whose string keys compare without regard to case unless
	/// <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// String keys compare with <see cref="StringComparer.OrdinalIgnoreCase"/>, or with
	/// <see cref="StringComparer.CurrentCulture"/> when <see cref="EqualityCollectionCtor.IsCaseSensitive"/> is
	/// <see langword="true"/>. The table has room for <see cref="EqualityCollectionCtor.Capacity"/> entries.
	/// </remarks>
	/// <param name="comparer">The comparer that the base class chose. This implementation uses a string comparer instead.</param>
	/// <returns>The new, empty table.</returns>
	protected override Hashtable ConstructTDefault(IEqualityComparer comparer)
	{
		var comp = this.IsCaseSensitive
			? StringComparer.CurrentCulture
			: StringComparer.OrdinalIgnoreCase;

		return new Hashtable(this.Capacity, comp);
	}

	protected override Type GetTypeForEquality()
	{
		return this.KeyType;
	}
	private static Type SetTypeOrObject([NotNull] ref Type? type)
	{
		type ??= typeof(object);
		return type;
	}
	protected override bool ShouldConstructDefault(IEqualityComparer? comparer, Type[] genericTypes)
	{
		return base.ShouldConstructDefault(comparer, genericTypes)
			   ||
			   (
					comparer is not IEqualityBlock
					&&
					genericTypes.All(x => typeof(object).Equals(x))
			   );
	}
}

