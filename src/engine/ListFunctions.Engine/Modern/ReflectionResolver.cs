namespace ListFunctions.Modern;

/// <summary>
/// Provides methods that find the <c>Add</c> method of a collection type through reflection.
/// </summary>
/// <remarks>
/// For generic collections, the methods return the interface method, <see cref="ICollection{T}.Add(T)"/>, so an explicit
/// interface implementation can also be called.
/// </remarks>
internal static class ReflectionResolver
{
	private static readonly MethodInfo s_colAddMethod;

	/// <summary>
	/// Initializes the cached generic method definition of <see cref="GetCollectionAdd{TCol, TItem}"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the method definition can't be found.</exception>
	static ReflectionResolver()
	{
		Type type = typeof(ReflectionResolver);
		s_colAddMethod = type.GetMethod(nameof(GetCollectionAdd))
			?? throw new InvalidOperationException("Unable to find method definition for GetCollectionAdd.");
	}

	/// <summary>
	/// Returns the <c>Add</c> method of the specified collection type closed over the specified element type.
	/// </summary>
	/// <remarks>
	/// The collection type must implement <see cref="ICollection"/> or be a <see cref="HashSet{T}"/>, and
	/// <paramref name="types"/> holds its one type argument, the element type.
	/// </remarks>
	/// <param name="collectionType">The closed collection type. This value must not be <see langword="null"/>.</param>
	/// <param name="types">The type arguments of <paramref name="collectionType"/>. This value must not be <see langword="null"/>.</param>
	/// <returns>The <c>Add</c> method to call on instances of <paramref name="collectionType"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="types"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="types"/> doesn't have exactly one element, when <paramref name="collectionType"/> isn't a supported collection, or when it doesn't implement <see cref="ICollection{T}"/> of the element type.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the <c>Add</c> method can't be resolved.</exception>
	public static MethodInfo GetAddMethod(Type collectionType, Type[] types)
	{
		ArgumentNullException.ThrowIfNull(types);
		if (types.Length != 1)
		{
			throw new ArgumentException("Wrong number of Type arguments were supplied.");
		}

		if (!typeof(ICollection).IsAssignableFrom(collectionType)
			&& !(collectionType.IsGenericType && typeof(HashSet<>).Equals(collectionType.GetGenericTypeDefinition())))
		{
			throw new ArgumentException("Type argument exception.");
		}

		MethodInfo getAdd = s_colAddMethod.MakeGenericMethod(collectionType, types[0]);
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

