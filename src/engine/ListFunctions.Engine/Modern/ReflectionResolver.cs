namespace ListFunctions.Modern;

/// <summary>
/// Provides methods that find the <c>Add</c> method of a collection or dictionary type through reflection.
/// </summary>
/// <remarks>
/// For generic collections and dictionaries, the methods return the interface method, such as
/// <see cref="ICollection{T}.Add(T)"/> or <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/>, so an explicit
/// interface implementation can also be called.
/// </remarks>
public static class ReflectionResolver
{
	private static readonly MethodInfo _colAddMethod;
	private static readonly MethodInfo _dictAddMethod;

	/// <summary>
	/// Initializes the cached generic method definitions of <see cref="GetCollectionAdd{TCol, TItem}"/> and
	/// <see cref="GetDictionaryAdd{TDict, TKey, TValue}"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when either method definition can't be found.</exception>
	static ReflectionResolver()
	{
		Type type = typeof(ReflectionResolver);
		_colAddMethod = type.GetMethod(nameof(GetCollectionAdd))
			?? throw new InvalidOperationException("Unable to find method definition for GetCollectionAdd.");
		_dictAddMethod = type.GetMethod(nameof(GetDictionaryAdd))
			?? throw new InvalidOperationException("Unable to find method definition for GetDictionaryAdd.");
	}

	/// <summary>
	/// Returns the <c>Add</c> method of the specified collection type closed over the specified type arguments.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see cref="Hashtable"/> returns <see cref="Hashtable.Add(object, object)"/>, and <paramref name="types"/> is
	/// ignored. A type that implements <see cref="IDictionary"/> needs two type arguments, the key type and the value
	/// type. A type that implements <see cref="ICollection"/>, or a <see cref="HashSet{T}"/>, needs one, the element
	/// type.
	/// </para>
	/// </remarks>
	/// <param name="collectionType">The closed collection type. This value must not be <see langword="null"/>.</param>
	/// <param name="types">The type arguments of <paramref name="collectionType"/>. This value must not be <see langword="null"/> unless <paramref name="collectionType"/> is <see cref="Hashtable"/>.</param>
	/// <returns>The <c>Add</c> method to call on instances of <paramref name="collectionType"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="types"/> is null and <paramref name="collectionType"/> isn't <see cref="Hashtable"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="types"/> doesn't have one or two elements, when <paramref name="collectionType"/> isn't a supported collection or dictionary for that number of type arguments, or when it doesn't implement the matching generic interface.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the <c>Add</c> method can't be resolved.</exception>
	public static MethodInfo GetAddMethod(Type collectionType, Type[] types)
	{
		if (typeof(Hashtable).Equals(collectionType))
		{
			return GetHashtableAdd();
		}

		ArgumentNullException.ThrowIfNull(types);
		if (types.Length <= 0 || types.Length > 2)
		{
			throw new ArgumentException("Wrong number of Type arguments were supplied.");
		}

		MethodInfo getAdd;
		if (typeof(IDictionary).IsAssignableFrom(collectionType) && types.Length == 2)
		{
			getAdd = _dictAddMethod.MakeGenericMethod(collectionType, types[0], types[1]);
		}
		else if ((typeof(ICollection).IsAssignableFrom(collectionType)
			|| (collectionType.IsGenericType &&
				typeof(HashSet<>).Equals(collectionType.GetGenericTypeDefinition())))
			&& types.Length == 1)
		{
			getAdd = _colAddMethod.MakeGenericMethod(collectionType, types[0]);
		}
		else
		{
			throw new ArgumentException("Type argument exception.");
		}

		return getAdd.Invoke(null, null) as MethodInfo ?? throw new InvalidOperationException("Unable to get Add method.");
	}

	/// <summary>
	/// Returns the <see cref="ICollection{T}.Add(T)"/> method for the specified collection and element types.
	/// </summary>
	/// <typeparam name="TCol">The collection type. It must implement <see cref="ICollection{T}"/> of <typeparamref name="TItem"/>.</typeparam>
	/// <typeparam name="TItem">The element type.</typeparam>
	/// <returns>The <see cref="ICollection{T}.Add(T)"/> method of <typeparamref name="TItem"/>.</returns>
	public static MethodInfo GetCollectionAdd<TCol, TItem>() where TCol : ICollection<TItem>
	{
		return GetCollectionAddMethod<TCol>(col => col.Add(default!));
	}

	/// <summary>
	/// Returns the <see cref="Hashtable.Add(object, object)"/> method.
	/// </summary>
	/// <returns>The <see cref="Hashtable.Add(object, object)"/> method.</returns>
	private static MethodInfo GetHashtableAdd()
	{
		Expression<Action<Hashtable>> exp = (x) => x.Add(default!, default!);
		return ((MethodCallExpression)exp.Body).Method;
	}

	/// <summary>
	/// Returns the <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/> method for the specified dictionary, key,
	/// and value types.
	/// </summary>
	/// <remarks>
	/// Because <typeparamref name="TKey"/> is constrained to <see langword="notnull"/>, a nullable key type produces a
	/// nullability warning in code that calls this method directly.
	/// </remarks>
	/// <typeparam name="TDict">The dictionary type. It must implement <see cref="IDictionary{TKey, TValue}"/> of <typeparamref name="TKey"/> and <typeparamref name="TValue"/>.</typeparam>
	/// <typeparam name="TKey">The key type.</typeparam>
	/// <typeparam name="TValue">The value type.</typeparam>
	/// <returns>The <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/> method of <typeparamref name="TKey"/> and <typeparamref name="TValue"/>.</returns>
	public static MethodInfo GetDictionaryAdd<TDict, TKey, TValue>()
		where TDict : IDictionary<TKey, TValue>
		where TKey : notnull
	{
		return GetCollectionAddMethod<TDict>(dict => dict.Add(default!, default!));
	}

	/// <summary>
	/// Returns the method that the specified call expression calls.
	/// </summary>
	/// <typeparam name="TCol">The type of the object that the expression calls the method on.</typeparam>
	/// <param name="callExpression">An expression whose body is a single method call.</param>
	/// <returns>The method that <paramref name="callExpression"/> calls.</returns>
	/// <exception cref="InvalidCastException">Thrown when the body of <paramref name="callExpression"/> isn't a method call.</exception>
	private static MethodInfo GetCollectionAddMethod<TCol>(Expression<Action<TCol>> callExpression)
	{
		return ((MethodCallExpression)callExpression.Body).Method;
	}
}

