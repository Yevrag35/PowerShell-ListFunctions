using ListFunctions.Extensions;
using ListFunctions.Modern.Exceptions;

namespace ListFunctions.Modern.Constructors;

/// <summary>
/// Provides the base class for objects that create instances of a closed generic collection type through
/// reflection.
/// </summary>
/// <remarks>
/// <para>
/// A derived class supplies the generic type definition and type arguments, and the arguments for the collection's
/// constructor. <see cref="Construct"/> calls <see cref="Activator.CreateInstance(Type, object[])"/> on
/// <see cref="ConstructingGenericType"/> with those arguments.
/// </para>
/// <para>
/// The class doesn't change its own state after construction. Whether an instance is thread-safe depends on the derived
/// class's <see cref="GetConstructorArguments(Type[])"/>, which <see cref="Construct"/> calls.
/// </para>
/// </remarks>
internal abstract class GenericCollectionCtor
{
	/// <summary>
	/// Gets the closed generic type of the collection that <see cref="Construct"/> creates.
	/// </summary>
	/// <value>The generic type definition passed to the constructor, closed over <see cref="GenericArgumentTypes"/>.</value>
	public Type ConstructingGenericType { get; }
	/// <summary>
	/// Gets the generic type arguments of the collection.
	/// </summary>
	/// <value>A copy of the type arguments passed to the constructor, or an empty array when none were passed.</value>
	public Type[] GenericArgumentTypes { get; }

	/// <summary>
	/// Initializes a new <see cref="GenericCollectionCtor"/> instance with the specified generic type definition and type
	/// arguments.
	/// </summary>
	/// <remarks>
	/// The constructor copies <paramref name="genericTypes"/>, so later changes to that array don't affect this
	/// instance.
	/// </remarks>
	/// <param name="genericDefinition">
	/// The open generic type definition of the collection. It must be a generic class that isn't abstract, and it
	/// must not be <see langword="null"/>.
	/// </param>
	/// <param name="genericTypes">
	/// The type arguments to close <paramref name="genericDefinition"/> over, or <see langword="null"/> for none.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="genericDefinition"/> is null.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericDefinition"/> isn't a generic type, isn't a class, or is abstract; or when
	/// <paramref name="genericTypes"/> doesn't satisfy the definition's type parameters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="genericDefinition"/> is a closed generic type instead of a generic type definition.
	/// </exception>
	protected GenericCollectionCtor(Type genericDefinition, Type[]? genericTypes)
	{
		ArgumentNullException.ThrowIfNull(genericDefinition);
		GuardDefinition(genericDefinition);

		this.GenericArgumentTypes = SetGenericTypes(genericTypes);
		this.ConstructingGenericType = MakeConstructingType(genericDefinition, this.GenericArgumentTypes);
	}

	/// <summary>
	/// Creates a new, empty collection.
	/// </summary>
	/// <remarks>
	/// The method creates an instance of <see cref="ConstructingGenericType"/> with the arguments from
	/// <see cref="GetConstructorArguments(Type[])"/>.
	/// </remarks>
	/// <returns>The new collection.</returns>
	/// <exception cref="ActivatorCtorException">
	/// Thrown when creating an instance of <see cref="ConstructingGenericType"/> fails or returns null.
	/// </exception>
	public object Construct()
	{
		object?[]? ctorArgs = this.EnumerateCtorArguments(this.GenericArgumentTypes);

		return CallActivator(this.ConstructingGenericType, ctorArgs);
	}

	/// <summary>
	/// Returns the arguments from <see cref="GetConstructorArguments(Type[])"/> as an array.
	/// </summary>
	/// <param name="genericTypes">The generic type arguments of the collection.</param>
	/// <returns>The constructor arguments, or an empty array when the derived class supplies none.</returns>
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

	/// <summary>
	/// Creates an instance of the specified type with the constructor that best matches the specified arguments.
	/// </summary>
	/// <remarks>
	/// Any exception that <see cref="Activator.CreateInstance(Type, object[])"/> throws is wrapped in an
	/// <see cref="ActivatorCtorException"/>.
	/// </remarks>
	/// <param name="constructingType">The type to create an instance of.</param>
	/// <param name="ctorArgs">The arguments to pass to the constructor, or <see langword="null"/> for none.</param>
	/// <returns>The new instance.</returns>
	/// <exception cref="ActivatorCtorException">
	/// Thrown when creating the instance throws an exception or returns null.
	/// </exception>
	private static object CallActivator(Type constructingType, object?[]? ctorArgs)
	{
		object? collection;
		try
		{
			collection = Activator.CreateInstance(constructingType, ctorArgs);
		}
		catch (Exception ex)
		{
			Debug.WriteLine(ex.Message);
			return ThrowBadCtor(constructingType, ex);
		}

		return collection
			?? ThrowBadCtor(constructingType);
	}

	/// <summary>
	/// Returns the arguments to pass to the constructor of <see cref="ConstructingGenericType"/>.
	/// </summary>
	/// <param name="genericTypes">The generic type arguments of the collection.</param>
	/// <returns>
	/// The constructor arguments in order, or <see langword="null"/> to call the parameterless constructor. This
	/// implementation always returns <see langword="null"/>.
	/// </returns>
	protected virtual IEnumerable<object?>? GetConstructorArguments(Type[] genericTypes)
	{
		return null;
	}

	/// <summary>
	/// Verifies that the specified type can serve as the generic type definition of a collection.
	/// </summary>
	/// <param name="genericDefinition">The type to verify.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericDefinition"/> isn't a generic type, isn't a class, or is abstract.
	/// </exception>
	[MethodImpl(MethodImplOptions.NoInlining)]
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
	/// <summary>
	/// Closes the specified generic type definition over the specified type arguments.
	/// </summary>
	/// <param name="genericTypeDefinition">The open generic type definition.</param>
	/// <param name="genericTypes">The type arguments to close <paramref name="genericTypeDefinition"/> over.</param>
	/// <returns>The closed generic type.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <paramref name="genericTypeDefinition"/> isn't a generic type definition.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="genericTypes"/> doesn't match the number of type parameters or doesn't satisfy their
	/// constraints.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Type MakeConstructingType(Type genericTypeDefinition, Type[] genericTypes)
	{
		return genericTypeDefinition.MakeGenericType(genericTypes);
	}
	/// <summary>
	/// Copies the specified type arguments into a new array.
	/// </summary>
	/// <param name="types">The type arguments to copy, or <see langword="null"/> for none.</param>
	/// <returns>
	/// A new array that contains the elements of <paramref name="types"/>, or an empty array when
	/// <paramref name="types"/> is <see langword="null"/> or empty.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Type[] SetGenericTypes(Type[]? types)
	{
		Type[] result = [];
		if (types is { Length: > 0})
		{
			result = new Type[types.Length];
			Array.Copy(types, result, types.Length);
		}

		return result;
	}
	/// <summary>
	/// Throws an <see cref="ActivatorCtorException"/> for the specified type.
	/// </summary>
	/// <param name="badType">The type that couldn't be created.</param>
	/// <returns>This method never returns.</returns>
	/// <exception cref="ActivatorCtorException">Always thrown.</exception>
	[DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
	private static object ThrowBadCtor(Type badType)
	{
		throw new ActivatorCtorException(badType);
	}
	/// <summary>
	/// Throws an <see cref="ActivatorCtorException"/> for the specified type that wraps the specified exception.
	/// </summary>
	/// <param name="badType">The type that couldn't be created.</param>
	/// <param name="caughtException">The exception that creating the instance threw.</param>
	/// <returns>This method never returns.</returns>
	/// <exception cref="ActivatorCtorException">Always thrown.</exception>
	[DoesNotReturn, DebuggerStepThrough, MethodImpl(MethodImplOptions.NoInlining)]
	private static object ThrowBadCtor(Type badType, Exception caughtException)
	{
		throw new ActivatorCtorException(badType, caughtException);
	}
}

